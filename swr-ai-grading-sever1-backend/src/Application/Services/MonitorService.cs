using System.Text.RegularExpressions;
using Application.Common;
using Application.DTOs.AuthDTOs;
using Application.DTOs.Monitors;
using Application.Interfaces;
using ClosedXML.Excel;
using Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public sealed class MonitorService : IMonitorService
{
    private readonly IUserRepository _userRepository;
    private readonly IAuthService _authService;
    private readonly ILogger<MonitorService> _logger;

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public MonitorService(
        IUserRepository userRepository,
        IAuthService authService,
        ILogger<MonitorService> logger)
    {
        _userRepository = userRepository;
        _authService = authService;
        _logger = logger;
    }

    public async Task<Result<MonitorImportResultDto>> ImportMonitorsFromExcelAsync(Stream fileStream, CancellationToken ct = default)
    {
        var result = new MonitorImportResultDto();

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(fileStream);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể đọc file Excel tải lên.");
            return Result<MonitorImportResultDto>.Failure("File Excel bị hỏng hoặc không đúng định dạng.", "INVALID_EXCEL_FILE");
        }
        using var _ = workbook;

        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet is null)
            return Result<MonitorImportResultDto>.Failure("File Excel không có worksheet nào.", "EXCEL_EMPTY");

        var expectedHeaders = new[]
        {
            "Mã GV (*)",
            "Họ và Tên (*)",
            "Email (*)",
            "Ngày sinh (yyyy-MM-dd) (*)"
        };

        for (var column = 1; column <= expectedHeaders.Length; column++)
        {
            var header = worksheet.Cell(1, column).GetString().Trim();
            if (!string.Equals(header, expectedHeaders[column - 1], StringComparison.OrdinalIgnoreCase))
            {
                return Result<MonitorImportResultDto>.Failure(
                    "File Excel không đúng định dạng mẫu. Hãy tải template và sử dụng đúng các tiêu đề dòng 1.",
                    "INVALID_EXCEL_TEMPLATE");
            }
        }

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow < 2)
            return Result<MonitorImportResultDto>.Failure("File Excel không có dữ liệu monitor (dòng bắt đầu từ dòng 2).", "NO_DATA");

        var seenStaffCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var row = 2; row <= lastRow; row++)
        {
            var staffCode = worksheet.Cell(row, 1).GetString().Trim();
            var fullName = worksheet.Cell(row, 2).GetString().Trim();
            var email = worksheet.Cell(row, 3).GetString().Trim().ToLowerInvariant();
            var birthdayRaw = worksheet.Cell(row, 4).GetString().Trim();

            if (string.IsNullOrWhiteSpace(staffCode) &&
                string.IsNullOrWhiteSpace(fullName) &&
                string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            result.TotalRows++;

            if (string.IsNullOrWhiteSpace(staffCode))
            {
                AddError(result, row, staffCode, email, "Mã monitor không được để trống.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                AddError(result, row, staffCode, email, "Họ và tên không được để trống.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(email) || !EmailRegex.IsMatch(email))
            {
                AddError(result, row, staffCode, email, "Email không hợp lệ hoặc để trống.");
                continue;
            }

            if (staffCode.Length > 20)
            {
                AddError(result, row, staffCode, email, "Mã monitor không được vượt quá 20 ký tự.");
                continue;
            }

            if (fullName.Length > 200)
            {
                AddError(result, row, staffCode, email, "Họ và tên không được vượt quá 200 ký tự.");
                continue;
            }

            if (email.Length > 200)
            {
                AddError(result, row, staffCode, email, "Email không được vượt quá 200 ký tự.");
                continue;
            }

            if (!TryReadBirthday(worksheet.Cell(row, 4), birthdayRaw, out var birthday))
            {
                AddError(result, row, staffCode, email, "Ngày sinh không đúng định dạng (VD: yyyy-MM-dd hoặc dd/MM/yyyy).");
                continue;
            }

            if (!seenStaffCodes.Add(staffCode))
            {
                AddError(result, row, staffCode, email, $"Mã monitor '{staffCode}' bị trùng lặp trong file.");
                continue;
            }

            if (!seenEmails.Add(email))
            {
                AddError(result, row, staffCode, email, $"Email '{email}' bị trùng lặp trong file.");
                continue;
            }

            if (await _userRepository.IsStaffCodeExistsAsync(staffCode, ct))
            {
                AddError(result, row, staffCode, email, $"Mã monitor '{staffCode}' đã tồn tại trong hệ thống.");
                continue;
            }

            if (await _userRepository.IsEmailExistsAsync(email, ct))
            {
                AddError(result, row, staffCode, email, $"Email '{email}' đã tồn tại trong hệ thống.");
                continue;
            }

            var rawPassword = SecurePasswordGenerator.Generate(10);
            var registrationResult = await _authService.RegisterAsync(new RegisterRequest(
                fullName,
                email,
                rawPassword,
                birthday,
                (int)UserRole.Monitor,
                StudentCode: null,
                Major: null,
                StaffCode: staffCode), ct);

            if (!registrationResult.IsSuccess)
            {
                AddError(result, row, staffCode, email, registrationResult.Error ?? "Không thể tạo tài khoản monitor.");
                continue;
            }

            result.SuccessCount++;
            result.ImportedMonitors.Add(new MonitorImportSuccessDto
            {
                RowIndex = row,
                StaffCode = staffCode,
                FullName = fullName,
                Email = email,
                Birthday = birthday,
                Password = rawPassword
            });
        }

        return Result<MonitorImportResultDto>.Success(result);
    }

    public byte[] GenerateMonitorImportTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Monitors");

        worksheet.Cell(1, 1).Value = "Mã GV (*)";
        worksheet.Cell(1, 2).Value = "Họ và Tên (*)";
        worksheet.Cell(1, 3).Value = "Email (*)";
        worksheet.Cell(1, 4).Value = "Ngày sinh (yyyy-MM-dd) (*)";

        var headerRange = worksheet.Range(1, 1, 1, 4);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(220, 230, 241);
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        worksheet.Cell(2, 1).Value = "MON001";
        worksheet.Cell(2, 2).Value = "Nguyễn Thị B";
        worksheet.Cell(2, 3).Value = "monitor@example.com";
        worksheet.Cell(2, 4).Value = "1995-06-20";

        worksheet.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
    }

    private static bool TryReadBirthday(IXLCell cell, string raw, out DateOnly birthday)
    {
        if (cell.DataType == XLDataType.DateTime)
        {
            birthday = DateOnly.FromDateTime(cell.GetDateTime());
            return true;
        }

        if (cell.DataType == XLDataType.Number && cell.TryGetValue<double>(out var serialDate))
        {
            birthday = DateOnly.FromDateTime(DateTime.FromOADate(serialDate));
            return true;
        }

        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy" };
        if (DateOnly.TryParseExact(raw, formats, null, System.Globalization.DateTimeStyles.None, out birthday))
            return true;

        return birthday != default || DateOnly.TryParse(raw, out birthday);
    }

    private static void AddError(MonitorImportResultDto result, int row, string? staffCode, string? email, string reason)
    {
        result.FailureCount++;
        result.Errors.Add(new MonitorImportErrorDto
        {
            RowIndex = row,
            StaffCode = staffCode,
            Email = email,
            Reason = reason
        });
    }
}
