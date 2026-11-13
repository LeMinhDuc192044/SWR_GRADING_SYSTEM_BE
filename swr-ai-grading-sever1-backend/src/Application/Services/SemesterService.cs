using System.Text.RegularExpressions;
using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Semesters;
using Application.Interfaces;
using ClosedXML.Excel;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class SemesterService : ISemesterService
{
	private readonly ISemesterRepository _repository;
	private readonly IExaminationRepository _examinationRepository;
	private readonly IPaperSetRepository _paperSetRepository;
	private readonly IUserRepository _userRepository;
	private readonly IApplicationDbContext _dbContext;
	private readonly IUnitOfWork _unitOfWork;

	public SemesterService(
		ISemesterRepository repository,
		IExaminationRepository examinationRepository,
		IPaperSetRepository paperSetRepository,
		IUserRepository userRepository,
		IApplicationDbContext dbContext,
		IUnitOfWork unitOfWork)
	{
		_repository = repository;
		_examinationRepository = examinationRepository;
		_paperSetRepository = paperSetRepository;
		_userRepository = userRepository;
		_dbContext = dbContext;
		_unitOfWork = unitOfWork;
	}

	public async Task<PagedResult<SemesterDTO>> GetPagedAsync(PagedRequest request, CancellationToken ct = default)
	{
		var semesters = await _repository.GetAllAsync(ct);
		var totalCount = semesters.Count;
		var items = semesters
			.OrderByDescending(s => s.StartDate)
			.Skip((request.Page - 1) * request.PageSize)
			.Take(request.PageSize)
			.Select(ToDto)
			.ToList();

		return new PagedResult<SemesterDTO>
		{
			Items = items,
			TotalCount = totalCount,
			Page = request.Page,
			PageSize = request.PageSize
		};
	}

	public async Task<Result<SemesterDetailDTO>> GetByIdAsync(Guid id, CancellationToken ct = default)
	{
		var semester = await _repository.GetByIdAsync(id, ct);
		return semester is null
			? Result<SemesterDetailDTO>.Failure("Semester not found.", "SEMESTER_NOT_FOUND")
			: Result<SemesterDetailDTO>.Success(await ToDetailDtoAsync(semester, ct));
	}

	public async Task<Result<SemesterDTO>> CreateAsync(CreateSemesterRequest request, CancellationToken ct = default)
	{
		var validation = Validate(request.SemesterCode, request.Name, request.StartDate, request.EndDate);
		if (validation is not null)
			return Result<SemesterDTO>.Failure(validation, "INVALID_SEMESTER");

		if (await _repository.IsDateRangeOverlappingAsync(request.StartDate, request.EndDate, ct: ct))
			return Result<SemesterDTO>.Failure("Semester dates must not overlap another semester.", "SEMESTER_DATE_RANGE_OVERLAP");

		var code = request.SemesterCode.Trim();
		if (await _repository.IsCodeExistsAsync(code, ct))
			return Result<SemesterDTO>.Failure("Semester code already exists.", "SEMESTER_CODE_EXISTS");

        if (request.Status == SemesterStatus.Closed)
			return Result<SemesterDTO>.Failure("Cannot create a semester with status 'Closed'.", "INVALID_SEMESTER_STATUS");

            var now = VietnamTime.Now;
		var semester = new Semester
		{
			SemesterCode = code,
			Name = request.Name.Trim(),
			StartDate = request.StartDate,
			EndDate = request.EndDate,
			CreatedDay = now,
			UpdatedDay = now,
			Status = request.Status
		};

		await _repository.AddAsync(semester, ct);
		await _unitOfWork.SaveChangesAsync(ct);
		return Result<SemesterDTO>.Success(ToDto(semester));
	}

	public async Task<Result<SemesterDTO>> UpdateAsync(Guid id, UpdateSemesterRequest request, CancellationToken ct = default)
	{
		var semester = await _repository.GetByIdAsync(id, ct);
		if (semester is null)
			return Result<SemesterDTO>.Failure("Semester not found.", "SEMESTER_NOT_FOUND");

		var code = request.SemesterCode?.Trim() ?? semester.SemesterCode;
		var name = request.Name?.Trim() ?? semester.Name;
		var startDate = request.StartDate ?? semester.StartDate;
		var endDate = request.EndDate ?? semester.EndDate;
		var validation = Validate(code, name, startDate, endDate);
		if (validation is not null)
			return Result<SemesterDTO>.Failure(validation, "INVALID_SEMESTER");

		if (await _repository.IsDateRangeOverlappingAsync(startDate, endDate, id, ct))
			return Result<SemesterDTO>.Failure("Semester dates must not overlap another semester.", "SEMESTER_DATE_RANGE_OVERLAP");

		if (!string.Equals(code, semester.SemesterCode, StringComparison.OrdinalIgnoreCase)
			&& await _repository.IsCodeExistsAsync(code, ct))
			return Result<SemesterDTO>.Failure("Semester code already exists.", "SEMESTER_CODE_EXISTS");

		semester.SemesterCode = code;
		semester.Name = name;
		semester.StartDate = startDate;
		semester.EndDate = endDate;
		semester.UpdatedDay = VietnamTime.Now;
		if (request.Status.HasValue)
			semester.Status = request.Status.Value;

		_repository.Update(semester);
		await _unitOfWork.SaveChangesAsync(ct);
		return Result<SemesterDTO>.Success(ToDto(semester));
	}

	public async Task<Result<SemesterDTO>> UpdateStatusAsync(Guid id, UpdateSemesterStatusRequest request, CancellationToken ct = default)
	{
		if (!Enum.IsDefined(request.Status))
			return Result<SemesterDTO>.Failure("Invalid semester status.", "INVALID_SEMESTER_STATUS");

		var semester = await _repository.GetByIdAsync(id, ct);
		if (semester is null)
			return Result<SemesterDTO>.Failure("Semester not found.", "SEMESTER_NOT_FOUND");

		semester.Status = request.Status;
		semester.UpdatedDay = VietnamTime.Now;
		_repository.Update(semester);
		await _unitOfWork.SaveChangesAsync(ct);
		return Result<SemesterDTO>.Success(ToDto(semester));
	}

	public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
	{
		var semester = await _repository.GetByIdAsync(id, ct);
		if (semester is null)
			return Result.Failure("Semester not found.", "SEMESTER_NOT_FOUND");

		if (await _repository.HasExaminationsAsync(id, ct))
			return Result.Failure("Cannot delete a semester that has examinations.", "SEMESTER_HAS_EXAMINATIONS");

		_repository.Remove(semester);
		await _unitOfWork.SaveChangesAsync(ct);
		return Result.Success();
	}

	public async Task<Result<StudentSemesterImportResultDto>> AddStudentsAsync(Guid semesterId, IReadOnlyList<AddStudentToSemesterRequest> requests, CancellationToken ct = default)
	{
		var rows = (requests ?? [])
			.Select((request, index) => (Row: index + 1, Request: request))
			.ToList();

		return await EnrollAsync(semesterId, rows, ct);
	}

	public async Task<Result<StudentSemesterImportResultDto>> ImportStudentsFromExcelAsync(Guid semesterId, Stream fileStream, CancellationToken ct = default)
	{
		if (await _repository.GetByIdAsync(semesterId, ct) is null)
			return Result<StudentSemesterImportResultDto>.Failure("Semester not found.", "SEMESTER_NOT_FOUND");

		try
		{
			using var workbook = new XLWorkbook(fileStream);
			var worksheet = workbook.Worksheets.FirstOrDefault();
			if (worksheet is null)
				return Result<StudentSemesterImportResultDto>.Failure("Excel file has no worksheet.", "EXCEL_EMPTY");

			var headers = GetHeaderMap(worksheet);
			if (!headers.ContainsKey("studentCode") || !headers.ContainsKey("staffCode"))
				return Result<StudentSemesterImportResultDto>.Failure(
					"Invalid Excel template. Download and use the semester import template with Student Code and Staff Code columns.",
					"INVALID_EXCEL_TEMPLATE");

			var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
			var result = new StudentSemesterImportResultDto { TotalRows = Math.Max(0, lastRow - 1) };

			var rows = new List<(int Row, AddStudentToSemesterRequest Request)>();
			for (var row = 2; row <= lastRow; row++)
			{
				var studentCode = worksheet.Cell(row, headers["studentCode"]).GetString().Trim();
				var staffCode = worksheet.Cell(row, headers["staffCode"]).GetString().Trim();

				if (string.IsNullOrWhiteSpace(studentCode) && string.IsNullOrWhiteSpace(staffCode)) continue;

				rows.Add((row, new AddStudentToSemesterRequest
				{
					StudentCode = studentCode,
					MonitorCode = staffCode
				}));
			}

			var importResult = await EnrollAsync(semesterId, rows, ct, result);
			if (!importResult.IsSuccess) return importResult;

			return Result<StudentSemesterImportResultDto>.Success(importResult.Data!);
		}
		catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
		{
			return Result<StudentSemesterImportResultDto>.Failure("The uploaded file is not a valid Excel workbook.", "INVALID_EXCEL_FILE");
		}
	}

	private async Task<Result<StudentSemesterImportResultDto>> EnrollAsync(
		Guid semesterId,
		IReadOnlyList<(int Row, AddStudentToSemesterRequest Request)> rows,
		CancellationToken ct,
		StudentSemesterImportResultDto? result = null)
	{
		if (await _repository.GetByIdAsync(semesterId, ct) is null)
			return Result<StudentSemesterImportResultDto>.Failure("Semester not found.", "SEMESTER_NOT_FOUND");
		if (rows is null || rows.Count == 0)
			return Result<StudentSemesterImportResultDto>.Failure("At least one student is required.", "INVALID_STUDENT_SEMESTER");

		result ??= new StudentSemesterImportResultDto { TotalRows = rows.Count };

		var seenStudentCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (row, request) in rows)
		{
			var studentCode = request.StudentCode?.Trim() ?? string.Empty;
			var staffCode = request.MonitorCode?.Trim() ?? string.Empty;

			if (string.IsNullOrWhiteSpace(studentCode))
			{
				AddImportError(result, row, studentCode, staffCode, "Student code is required.");
				continue;
			}

			if (string.IsNullOrWhiteSpace(staffCode))
			{
				AddImportError(result, row, studentCode, staffCode, "Staff code (monitor) is required.");
				continue;
			}

			if (!seenStudentCodes.Add(studentCode))
			{
				AddImportError(result, row, studentCode, staffCode, $"Student code '{studentCode}' appears more than once in the file.");
				continue;
			}

			await ResolveAndEnrollAsync(semesterId, studentCode, staffCode, row, result, ct);
		}

		await _unitOfWork.SaveChangesAsync(ct);
		return Result<StudentSemesterImportResultDto>.Success(result);
	}

	// Existing records only: this links a student to the semester with a monitor, nothing is created.
	private async Task ResolveAndEnrollAsync(Guid semesterId, string studentCode, string staffCode, int row, StudentSemesterImportResultDto result, CancellationToken ct)
	{
		var student = await _dbContext.Students.FirstOrDefaultAsync(s => s.StudentCode == studentCode && !s.IsDeleted, ct);
		if (student is null)
		{
			AddImportError(result, row, studentCode, staffCode, $"Student code '{studentCode}' was not found.");
			return;
		}

		var monitor = await _dbContext.Staff.FirstOrDefaultAsync(s =>
			s.StaffCode == staffCode && s.Role == UserRole.Monitor && !s.IsDeleted, ct);
		if (monitor is null)
		{
			AddImportError(result, row, studentCode, staffCode, $"Staff code '{staffCode}' was not found as a monitor.");
			return;
		}

		if (await _dbContext.StudentSemesters.AnyAsync(x => x.StudentId == student.Id && x.SemesterId == semesterId, ct))
		{
			AddImportError(result, row, studentCode, staffCode, $"Student code '{studentCode}' is already assigned to this semester.");
			return;
		}

		_dbContext.StudentSemesters.Add(new StudentSemester { StudentId = student.Id, SemesterId = semesterId, StaffId = monitor.Id });
		result.SuccessCount++;
		result.ImportedStudents.Add(new StudentSemesterDTO
		{
			StudentId = student.Id,
			SemesterId = semesterId,
			StudentCode = student.StudentCode,
			FullName = student.FullName,
			LecturerName = monitor.FullName,
			LecturerCode = monitor.StaffCode
		});
	}

	public byte[] GenerateStudentImportTemplate()
	{
		using var workbook = new XLWorkbook();
		var worksheet = workbook.Worksheets.Add("Students");

		var headers = new[] { "Student Code (*)", "Staff Code (*)" };
		for (var column = 0; column < headers.Length; column++) worksheet.Cell(1, column + 1).Value = headers[column];

		var headerRange = worksheet.Range(1, 1, 1, headers.Length);
		headerRange.Style.Font.Bold = true;
		headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(220, 230, 241);
		headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

		worksheet.Cell(2, 1).Value = "SE160001";
		worksheet.Cell(2, 2).Value = "MON001";

		worksheet.Columns().AdjustToContents();

		using var memoryStream = new MemoryStream();
		workbook.SaveAs(memoryStream);
		return memoryStream.ToArray();
	}

	private static Dictionary<string, int> GetHeaderMap(IXLWorksheet worksheet)
	{
		var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
		for (var column = 1; column <= lastColumn; column++)
		{
			var header = worksheet.Cell(1, column).GetString().Trim().ToLowerInvariant();
			var normalized = header.Replace(" ", "_").Replace("(*)", "").Trim('_');
			if (normalized.Contains("student_code") || normalized.Contains("studentcode") || normalized.Contains("ma_sv") || normalized.Contains("mã_sv")) map.TryAdd("studentCode", column);
			else if (normalized.Contains("staff_code") || normalized.Contains("staffcode")
				|| normalized.Contains("monitor_code") || normalized.Contains("monitorcode")) map.TryAdd("staffCode", column);
		}
		return map;
	}

	private static void AddImportError(StudentSemesterImportResultDto result, int row, string? studentCode, string? staffCode, string reason)
	{
		result.FailureCount++;
		result.Errors.Add(new StudentSemesterImportErrorDto { RowIndex = row, StudentCode = studentCode, StaffCode = staffCode, Reason = reason });
	}

	private Task<SemesterDetailDTO> ToDetailDtoAsync(Semester semester, CancellationToken ct)
	{
		return Task.FromResult(new SemesterDetailDTO
		{
			SemesterId = semester.SemesterId,
			SemesterCode = semester.SemesterCode,
			Name = semester.Name,
			StartDate = semester.StartDate,
			EndDate = semester.EndDate,
			CreatedDay = semester.CreatedDay,
			UpdatedDay = semester.UpdatedDay,
			Status = semester.Status,
			Examinations = [],
			PaperSets = []
		});
	}

	private static string? Validate(string code, string name, DateOnly startDate, DateOnly endDate)
	{
		if (string.IsNullOrWhiteSpace(code))
			return "Semester code is required.";
		if (!Regex.IsMatch(code.Trim(), "^(SP|SU|FA)\\d{2}$", RegexOptions.CultureInvariant))
			return "Semester code must use the format SP##, SU##, or FA##.";
		if (!int.TryParse(code[^2..], out var codeYear) || codeYear != startDate.Year % 100)
			return "Semester code year must match the semester start date year.";
		if (string.IsNullOrWhiteSpace(name))
			return "Semester name is required.";
		if (endDate < startDate)
			return "End date must be on or after start date.";
		return null;
	}

	private static SemesterDTO ToDto(Semester semester) => new()
	{
		SemesterId = semester.SemesterId,
		SemesterCode = semester.SemesterCode,
		Name = semester.Name,
		StartDate = semester.StartDate,
		EndDate = semester.EndDate,
		CreatedDay = semester.CreatedDay,
		UpdatedDay = semester.UpdatedDay,
		Status = semester.Status
	};
}
