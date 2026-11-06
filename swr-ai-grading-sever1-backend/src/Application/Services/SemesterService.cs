using System.Text.RegularExpressions;
using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Semesters;
using Application.Interfaces;
using ClosedXML.Excel;
using Domain.Entities;
using Domain.Enums;
using Application.DTOs.AuthDTOs;
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
	private readonly IAuthService _authService;

	public SemesterService(
		ISemesterRepository repository,
		IExaminationRepository examinationRepository,
		IPaperSetRepository paperSetRepository,
		IUserRepository userRepository,
		IApplicationDbContext dbContext,
		IUnitOfWork unitOfWork,
		IAuthService authService)
	{
		_repository = repository;
		_examinationRepository = examinationRepository;
		_paperSetRepository = paperSetRepository;
		_userRepository = userRepository;
		_dbContext = dbContext;
		_unitOfWork = unitOfWork;
		_authService = authService;
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
		if (await _repository.GetByIdAsync(semesterId, ct) is null)
			return Result<StudentSemesterImportResultDto>.Failure("Semester not found.", "SEMESTER_NOT_FOUND");
		if (requests is null || requests.Count == 0)
			return Result<StudentSemesterImportResultDto>.Failure("At least one student is required.", "INVALID_STUDENT_SEMESTER");

		var result = new StudentSemesterImportResultDto { TotalRows = requests.Count };
		var seenStudentCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (request, index) in requests.Select((value, position) => (value, position)))
		{
			var row = index + 1;
			var missing = new List<string>();
			if (string.IsNullOrWhiteSpace(request.StudentCode)) missing.Add("Student code");
			if (string.IsNullOrWhiteSpace(request.StudentName)) missing.Add("Student name");
			if (string.IsNullOrWhiteSpace(request.StudentEmail)) missing.Add("Student email");
			if (request.StudentBirthday == default) missing.Add("Student birthday");
			if (string.IsNullOrWhiteSpace(request.Major)) missing.Add("Major");
			if (string.IsNullOrWhiteSpace(request.MonitorCode)) missing.Add("Monitor code");
			if (string.IsNullOrWhiteSpace(request.MonitorName)) missing.Add("Monitor name");
			if (string.IsNullOrWhiteSpace(request.MonitorEmail)) missing.Add("Monitor email");
			if (request.MonitorBirthday == default) missing.Add("Monitor birthday");
			if (missing.Count > 0)
			{
				AddImportError(result, row, request.StudentCode?.Trim(), $"Missing required field(s): {string.Join(", ", missing)}.");
				continue;
			}
			if (!seenStudentCodes.Add(request.StudentCode.Trim()))
			{
				AddImportError(result, row, request.StudentCode.Trim(), $"Student code '{request.StudentCode.Trim()}' is duplicated in the request.");
				continue;
			}

			var enrollResult = await ResolveAndEnrollAsync(semesterId, request, row, result, ct);
			if (!enrollResult) continue;
		}
		await _unitOfWork.SaveChangesAsync(ct);
		return Result<StudentSemesterImportResultDto>.Success(result);
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
			var required = new[] { "studentCode", "studentName", "studentEmail", "studentBirthday", "major", "monitorCode", "monitorName", "monitorEmail", "monitorBirthday" };
			foreach (var key in required)
				if (!headers.ContainsKey(key))
					return Result<StudentSemesterImportResultDto>.Failure("Invalid Excel template. Download and use the semester student import template.", "INVALID_EXCEL_TEMPLATE");

			var requests = new List<AddStudentToSemesterRequest>();
			var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
			var result = new StudentSemesterImportResultDto { TotalRows = Math.Max(0, lastRow - 1) };
			for (var row = 2; row <= lastRow; row++)
			{
				var studentCode = worksheet.Cell(row, headers["studentCode"]).GetString().Trim();
				var studentName = worksheet.Cell(row, headers["studentName"]).GetString().Trim();
				var studentEmail = worksheet.Cell(row, headers["studentEmail"]).GetString().Trim().ToLowerInvariant();
				var monitorCode = worksheet.Cell(row, headers["monitorCode"]).GetString().Trim();
				var monitorName = worksheet.Cell(row, headers["monitorName"]).GetString().Trim();
				var monitorEmail = worksheet.Cell(row, headers["monitorEmail"]).GetString().Trim().ToLowerInvariant();
				var major = worksheet.Cell(row, headers["major"]).GetString().Trim();

				if (string.IsNullOrWhiteSpace(studentCode) && string.IsNullOrWhiteSpace(studentName) && string.IsNullOrWhiteSpace(monitorCode)) continue;

				if (!TryReadDate(worksheet.Cell(row, headers["studentBirthday"]), out var studentBirthday) || !TryReadDate(worksheet.Cell(row, headers["monitorBirthday"]), out var monitorBirthday))
				{
					AddImportError(result, row, studentCode, "Birthday must use yyyy-MM-dd format.");
					continue;
				}

				requests.Add(new AddStudentToSemesterRequest
				{
					StudentCode = studentCode,
					StudentName = studentName,
					StudentEmail = studentEmail,
					StudentBirthday = studentBirthday,
					Major = major,
					MonitorCode = monitorCode,
					MonitorName = monitorName,
					MonitorEmail = monitorEmail,
					MonitorBirthday = monitorBirthday
				});
			}

			var importResult = await AddStudentsAsync(semesterId, requests, ct);
			if (!importResult.IsSuccess) return importResult;

			var enrollmentResult = importResult.Data!;
			result.SuccessCount += enrollmentResult.SuccessCount;
			result.FailureCount += enrollmentResult.FailureCount;
			result.Errors.AddRange(enrollmentResult.Errors);
			result.ImportedStudents.AddRange(enrollmentResult.ImportedStudents);
			return Result<StudentSemesterImportResultDto>.Success(result);
		}
		catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
		{
			return Result<StudentSemesterImportResultDto>.Failure("The uploaded file is not a valid Excel workbook.", "INVALID_EXCEL_FILE");
		}
	}

	private async Task<bool> ResolveAndEnrollAsync(Guid semesterId, AddStudentToSemesterRequest request, int row, StudentSemesterImportResultDto result, CancellationToken ct)
	{
		var studentCode = request.StudentCode.Trim();
		var monitorCode = request.MonitorCode.Trim();
		var student = await _dbContext.Students.FirstOrDefaultAsync(s => s.StudentCode == studentCode && !s.IsDeleted, ct);

		if (student is null)
		{
			var registration = await _authService.RegisterAsync(new RegisterRequest(
				request.StudentName.Trim(), request.StudentEmail.Trim().ToLowerInvariant(), "Semester!2026", request.StudentBirthday,
				(int)UserRole.Student, studentCode, request.Major.Trim()), ct);
			if (!registration.IsSuccess)
			{
				AddImportError(result, row, studentCode, registration.Error ?? "Could not create the student account.");
				return false;
			}
			student = await _dbContext.Students.FirstOrDefaultAsync(s => s.StudentCode == studentCode && !s.IsDeleted, ct);
		}
		if (student is null)
		{
			AddImportError(result, row, studentCode, "Student account was not found or could not be created.");
			return false;
		}

		var monitor = await _dbContext.Staff.FirstOrDefaultAsync(s => s.StaffCode == monitorCode && s.Role == UserRole.Monitor && !s.IsDeleted, ct);
		if (monitor is null)
		{
			var registration = await _authService.RegisterAsync(new RegisterRequest(
				request.MonitorName.Trim(), request.MonitorEmail.Trim().ToLowerInvariant(), "Semester!2026", request.MonitorBirthday,
				(int)UserRole.Monitor, null, null, monitorCode), ct);
			if (!registration.IsSuccess)
			{
				AddImportError(result, row, studentCode, registration.Error ?? "Could not create the monitor account.");
				return false;
			}
			monitor = await _dbContext.Staff.FirstOrDefaultAsync(s => s.StaffCode == monitorCode && s.Role == UserRole.Monitor && !s.IsDeleted, ct);
		}
		if (monitor is null)
		{
			AddImportError(result, row, studentCode, "Monitor account was not found or could not be created.");
			return false;
		}

		if (await _dbContext.StudentSemesters.AnyAsync(x => x.StudentId == student.Id && x.SemesterId == semesterId, ct))
		{
			AddImportError(result, row, studentCode, $"Student code '{studentCode}' is already assigned to this semester.");
			return false;
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
		return true;
	}

	private static bool TryReadDate(IXLCell cell, out DateOnly date)
	{
		date = default;
		if (cell.DataType == XLDataType.DateTime)
		{
			date = DateOnly.FromDateTime(cell.GetDateTime());
			return true;
		}
		return DateOnly.TryParseExact(cell.GetString().Trim(), new[] { "yyyy-MM-dd", "dd/MM/yyyy" }, null, System.Globalization.DateTimeStyles.None, out date);
	}

	public byte[] GenerateStudentImportTemplate()
	{
		using var workbook = new XLWorkbook();
		var worksheet = workbook.Worksheets.Add("Students");

		var headers = new[]
		{
			"Student Code (*)", "Student Name (*)", "Student Email (*)", "Student Birthday (yyyy-MM-dd) (*)", "Major (*)",
			"Monitor Code (*)", "Monitor Name (*)", "Monitor Email (*)", "Monitor Birthday (yyyy-MM-dd) (*)"
		};
		for (var column = 0; column < headers.Length; column++) worksheet.Cell(1, column + 1).Value = headers[column];

		var headerRange = worksheet.Range(1, 1, 1, headers.Length);
		headerRange.Style.Font.Bold = true;
		headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(220, 230, 241);
		headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

		var sample = new object[] { "SE160001", "Nguyen Van A", "student@example.com", "2003-01-15", "Software Engineering", "MON001", "Nguyen Thi B", "monitor@example.com", "1995-06-20" };
		for (var column = 0; column < sample.Length; column++) worksheet.Cell(2, column + 1).Value = sample[column]?.ToString() ?? string.Empty;

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
			if (normalized.Contains("student_code") || normalized.Contains("studentcode") || normalized.Contains("ma_sv") || normalized.Contains("mã_sv") || normalized == "mã_sinh_viên") map.TryAdd("studentCode", column);
			else if (normalized.Contains("student_name") || normalized.Contains("studentname") || normalized.Contains("ho_va_ten")) map.TryAdd("studentName", column);
			else if (normalized.Contains("student_email") || normalized.Contains("studentemail")) map.TryAdd("studentEmail", column);
			else if (normalized.Contains("student_birthday") || normalized.Contains("ngay_sinh_sinh_vien")) map.TryAdd("studentBirthday", column);
			else if (normalized == "major" || normalized.Contains("chuyen_nganh")) map.TryAdd("major", column);
			else if (normalized.Contains("monitor_code") || normalized.Contains("monitorcode") || normalized.Contains("ma_monitor")) map.TryAdd("monitorCode", column);
			else if (normalized.Contains("monitor_name") || normalized.Contains("monitorname") || normalized.Contains("ten_monitor")) map.TryAdd("monitorName", column);
			else if (normalized.Contains("monitor_email") || normalized.Contains("monitoremail") || normalized.Contains("email_monitor")) map.TryAdd("monitorEmail", column);
			else if (normalized.Contains("monitor_birthday") || normalized.Contains("ngay_sinh_monitor")) map.TryAdd("monitorBirthday", column);
		}
		return map;
	}

	private static void AddImportError(StudentSemesterImportResultDto result, int row, string? studentCode, string reason)
	{
		result.FailureCount++;
		result.Errors.Add(new StudentSemesterImportErrorDto { RowIndex = row, StudentCode = studentCode, Reason = reason });
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

	private async Task<SemesterDetailDTO> ToDetailDtoAsync(Semester semester, CancellationToken ct)
	{
		var examinations = await _examinationRepository.FindAsync(e => e.SemesterId == semester.SemesterId, ct);
		var materials = await _paperSetRepository.FindAsync(m => m.SemesterId == semester.SemesterId && !m.IsDeleted, ct);

		return new SemesterDetailDTO
		{
			SemesterId = semester.SemesterId,
			SemesterCode = semester.SemesterCode,
			Name = semester.Name,
			StartDate = semester.StartDate,
			EndDate = semester.EndDate,
			CreatedDay = semester.CreatedDay,
			UpdatedDay = semester.UpdatedDay,
			Status = semester.Status,

			PaperSets = materials.OrderByDescending(m => m.CreatedDate).Select(m => new SemesterPaperSetDTO
			{
				PaperSetId = m.PaperSetId,
				PaperSetCode = m.PaperSetCode,
				Description = m.Description,
				TotalQuestions = m.TotalQuestions,
				Status = m.Status,
				ExaminationId = m.ExaminationId,
				CreatedDate = m.CreatedDate,
				UpdatedDate = m.UpdatedDate,
				FileQuestionDocs = m.FileQuestionDocs,
				FileAnswerRubric = m.FileAnswerRubric,
				FileAnswerTemplate = m.FileAnswerTemplate
			}).ToList()
		};
	}
}
