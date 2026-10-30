using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AI_Assisted_SWR_Grading_System.Domain.Enums;
using Application.Common;
using Application.DTOs.Students;
using Application.Interfaces;
using ClosedXML.Excel;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public sealed class StudentService : IStudentService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _uow;
    private readonly IEmailService _emailService;
    private readonly ILogger<StudentService> _logger;

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public StudentService(
        IUserRepository userRepository,
        IUnitOfWork uow,
        IEmailService emailService,
        ILogger<StudentService> logger)
    {
        _userRepository = userRepository;
        _uow = uow;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<StudentImportResultDto>> ImportStudentsFromExcelAsync(Stream fileStream, CancellationToken ct = default)
    {
        var result = new StudentImportResultDto();

        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet is null)
        {
            return Result<StudentImportResultDto>.Failure("File Excel không có worksheet nào.", "EXCEL_EMPTY");
        }

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow < 2)
        {
            return Result<StudentImportResultDto>.Failure("File Excel không có dữ liệu học sinh (dòng bắt đầu từ dòng 2).", "NO_DATA");
        }

        var seenStudentCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var studentsToInsert = new List<(Student student, string rawPassword, int rowIndex)>();

        for (int row = 2; row <= lastRow; row++)
        {
            var studentCode = worksheet.Cell(row, 1).GetString().Trim();
            var fullName = worksheet.Cell(row, 2).GetString().Trim();
            var email = worksheet.Cell(row, 3).GetString().Trim().ToLowerInvariant();
            var major = worksheet.Cell(row, 4).GetString().Trim();
            var birthdayRaw = worksheet.Cell(row, 5).GetString().Trim();
            var cccd = worksheet.Cell(row, 6).GetString().Trim();

            // Nếu cả dòng đều trống thì bỏ qua
            if (string.IsNullOrWhiteSpace(studentCode) &&
                string.IsNullOrWhiteSpace(fullName) &&
                string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            result.TotalRows++;

            // 1. Validate bắt buộc
            if (string.IsNullOrWhiteSpace(studentCode))
            {
                result.FailureCount++;
                result.Errors.Add(new StudentImportErrorDto { RowIndex = row, Reason = "Mã sinh viên không được để trống." });
                continue;
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                result.FailureCount++;
                result.Errors.Add(new StudentImportErrorDto { RowIndex = row, StudentCode = studentCode, Reason = "Họ và tên không được để trống." });
                continue;
            }

            if (string.IsNullOrWhiteSpace(email) || !EmailRegex.IsMatch(email))
            {
                result.FailureCount++;
                result.Errors.Add(new StudentImportErrorDto { RowIndex = row, StudentCode = studentCode, Email = email, Reason = "Email không hợp lệ hoặc để trống." });
                continue;
            }

            if (string.IsNullOrWhiteSpace(major))
            {
                result.FailureCount++;
                result.Errors.Add(new StudentImportErrorDto { RowIndex = row, StudentCode = studentCode, Email = email, Reason = "Chuyên ngành (Major) không được để trống." });
                continue;
            }

            if (string.IsNullOrWhiteSpace(cccd))
            {
                result.FailureCount++;
                result.Errors.Add(new StudentImportErrorDto { RowIndex = row, StudentCode = studentCode, Email = email, Reason = "CCCD không được để trống." });
                continue;
            }

            // 2. Parse ngày sinh
            DateOnly birthday = default;
            var cell5 = worksheet.Cell(row, 5);
            if (cell5.DataType == XLDataType.DateTime)
            {
                var dt = cell5.GetDateTime();
                birthday = DateOnly.FromDateTime(dt);
            }
            else
            {
                var dateFormats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy" };
                if (!DateOnly.TryParseExact(birthdayRaw, dateFormats, null, System.Globalization.DateTimeStyles.None, out birthday))
                {
                    if (!DateOnly.TryParse(birthdayRaw, out birthday))
                    {
                        result.FailureCount++;
                        result.Errors.Add(new StudentImportErrorDto { RowIndex = row, StudentCode = studentCode, Email = email, Reason = "Ngày sinh không đúng định dạng (VD: yyyy-MM-dd hoặc dd/MM/yyyy)." });
                        continue;
                    }
                }
            }

            // 3. Check trùng lặp ngay trong file excel
            if (!seenStudentCodes.Add(studentCode))
            {
                result.FailureCount++;
                result.Errors.Add(new StudentImportErrorDto { RowIndex = row, StudentCode = studentCode, Email = email, Reason = $"Mã sinh viên '{studentCode}' bị trùng lặp trong file." });
                continue;
            }

            if (!seenEmails.Add(email))
            {
                result.FailureCount++;
                result.Errors.Add(new StudentImportErrorDto { RowIndex = row, StudentCode = studentCode, Email = email, Reason = $"Email '{email}' bị trùng lặp trong file." });
                continue;
            }

            // 4. Check tồn tại trong Database
            if (await _userRepository.IsStudentCodeExistsAsync(studentCode, ct))
            {
                result.FailureCount++;
                result.Errors.Add(new StudentImportErrorDto { RowIndex = row, StudentCode = studentCode, Email = email, Reason = $"Mã sinh viên '{studentCode}' đã tồn tại trong hệ thống." });
                continue;
            }

            if (await _userRepository.IsEmailExistsAsync(email, ct))
            {
                result.FailureCount++;
                result.Errors.Add(new StudentImportErrorDto { RowIndex = row, StudentCode = studentCode, Email = email, Reason = $"Email '{email}' đã tồn tại trong hệ thống." });
                continue;
            }

            // 5. Sinh mật khẩu ngẫu nhiên
            var rawPassword = GenerateSecurePassword(10);
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(rawPassword);

            var student = new Student
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                Email = email,
                Birthday = birthday,
                Cccd = cccd,
                StudentCode = studentCode,
                Major = major,
                PasswordHash = passwordHash,
                Role = UserRole.Student,
                IsActive = true,
                IsDeleted = false
            };

            studentsToInsert.Add((student, rawPassword, row));
        }

        // Lưu vào DB
        foreach (var item in studentsToInsert)
        {
            await _userRepository.AddAsync(item.student, ct);
        }

        if (studentsToInsert.Count > 0)
        {
            await _uow.SaveChangesAsync(ct);
        }

        // Gửi email cho từng học sinh
        foreach (var item in studentsToInsert)
        {
            bool emailSent = false;
            try
            {
                var emailBody = BuildStudentAccountEmail(item.student.FullName, item.student.StudentCode, item.student.Email, item.rawPassword);
                await _emailService.SendEmailAsync(
                    item.student.Email,
                    "[SWR Grading System] Thông tin tài khoản sinh viên",
                    emailBody,
                    ct);
                emailSent = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi email thông tin tài khoản cho sinh viên {Email}", item.student.Email);
            }

            result.SuccessCount++;
            result.ImportedStudents.Add(new StudentImportSuccessDto
            {
                RowIndex = item.rowIndex,
                StudentCode = item.student.StudentCode,
                FullName = item.student.FullName,
                Email = item.student.Email,
                Major = item.student.Major,
                EmailSent = emailSent
            });
        }

        return Result<StudentImportResultDto>.Success(result);
    }

    public byte[] GenerateStudentImportTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Students");

        // Headers
        worksheet.Cell(1, 1).Value = "Mã SV (*)";
        worksheet.Cell(1, 2).Value = "Họ và Tên (*)";
        worksheet.Cell(1, 3).Value = "Email (*)";
        worksheet.Cell(1, 4).Value = "Chuyên ngành (*)";
        worksheet.Cell(1, 5).Value = "Ngày sinh (yyyy-MM-dd) (*)";
        worksheet.Cell(1, 6).Value = "CCCD (*)";

        // Style header
        var headerRange = worksheet.Range(1, 1, 1, 6);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(220, 230, 241);
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Sample row
        worksheet.Cell(2, 1).Value = "SE160001";
        worksheet.Cell(2, 2).Value = "Nguyễn Văn A";
        worksheet.Cell(2, 3).Value = "nguyenvana@gmail.com";
        worksheet.Cell(2, 4).Value = "Kỹ thuật phần mềm";
        worksheet.Cell(2, 5).Value = "2002-05-15";
        worksheet.Cell(2, 6).Value = "012345678901";

        worksheet.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
    }

    private static string GenerateSecurePassword(int length = 10)
    {
        const string upper = "ABCDEFGHJKLMNOPQRSTUVWXYZ";
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string special = "@#$%&*";
        const string all = upper + lower + digits + special;

        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);

        var chars = new char[length];
        // Ensure at least one of each category
        chars[0] = upper[bytes[0] % upper.Length];
        chars[1] = lower[bytes[1] % lower.Length];
        chars[2] = digits[bytes[2] % digits.Length];
        chars[3] = special[bytes[3] % special.Length];

        for (int i = 4; i < length; i++)
        {
            chars[i] = all[bytes[i] % all.Length];
        }

        // Shuffle
        var shuffleBytes = new byte[length];
        RandomNumberGenerator.Fill(shuffleBytes);
        for (int i = length - 1; i > 0; i--)
        {
            int j = shuffleBytes[i] % (i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    private static string BuildStudentAccountEmail(string fullName, string studentCode, string email, string password)
    {
        return $@"
        <div style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; border: 1px solid #e1e4e8; border-radius: 8px; overflow: hidden;"">
            <div style=""background-color: #2b6cb0; padding: 20px; text-align: center; color: white;"">
                <h2 style=""margin: 0;"">Hệ Thống Chấm Điểm Tự Động SWR</h2>
            </div>
            <div style=""padding: 24px;"">
                <p>Xin chào <strong>{fullName}</strong>,</p>
                <p>Tài khoản học sinh của bạn trên hệ thống <strong>SWR Grading System</strong> đã được tạo thành công. Dưới đây là thông tin đăng nhập của bạn:</p>
                <div style=""background-color: #f7fafc; border-left: 4px solid #2b6cb0; padding: 15px; margin: 20px 0;"">
                    <p style=""margin: 5px 0;""><strong>Mã sinh viên:</strong> {studentCode}</p>
                    <p style=""margin: 5px 0;""><strong>Tài khoản đăng nhập (Email):</strong> {email}</p>
                    <p style=""margin: 5px 0;""><strong>Mật khẩu ban đầu:</strong> <code style=""background: #edf2f7; padding: 2px 6px; border-radius: 4px; font-weight: bold; color: #c53030;"">{password}</code></p>
                </div>
                <div style=""background-color: #fffaf0; border: 1px solid #feebc8; border-radius: 6px; padding: 12px; margin: 20px 0; color: #7b341e;"">
                    <p style=""margin: 0;"">⚠️ <strong>Lưu ý quan trọng:</strong> Vì lý do an toàn, vui lòng đăng nhập vào hệ thống và thực hiện <strong>đổi mật khẩu ngay trong lần đầu tiên</strong> sử dụng.</p>
                </div>
                <p>Nếu bạn không thực hiện yêu cầu này hoặc cần hỗ trợ, vui lòng liên hệ với quản trị viên nhà trường.</p>
                <br/>
                <p>Trân trọng,<br/><strong>Ban Quản Trị Hệ Thống SWR</strong></p>
            </div>
            <div style=""background-color: #edf2f7; padding: 12px; text-align: center; font-size: 12px; color: #718096;"">
                Đây là email tự động. Vui lòng không trả lời trực tiếp email này.
            </div>
        </div>";
    }
}

