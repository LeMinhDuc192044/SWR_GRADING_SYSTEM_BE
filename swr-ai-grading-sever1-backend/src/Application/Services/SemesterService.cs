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

            var now = DateTime.UtcNow;
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
		semester.UpdatedDay = DateTime.UtcNow;
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
		semester.UpdatedDay = DateTime.UtcNow;
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
		var semester = await _repository.GetByIdAsync(semesterId, ct);
		if (semester is null)
			return Result<StudentSemesterImportResultDto>.Failure("Semester not found.", "SEMESTER_NOT_FOUND");
		if (requests is null || requests.Count == 0)
			return Result<StudentSemesterImportResultDto>.Failure("At least one student is required.", "INVALID_STUDENT_SEMESTER");

		var result = new StudentSemesterImportResultDto { TotalRows = requests.Count };
		var seenStudentCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		for (var index = 0; index < requests.Count; index++)
		{
			var request = requests[index];
			var studentCode = request.StudentCode?.Trim() ?? string.Empty;
			var lecturerName = request.LecturerName?.Trim() ?? string.Empty;
			var lecturerCode = request.LecturerCode?.Trim() ?? string.Empty;
			var classCode = request.ClassCode?.Trim() ?? string.Empty;
			var itemNumber = index + 1;

			var missingFields = new List<string>();
			if (string.IsNullOrWhiteSpace(studentCode)) missingFields.Add("Student code");
			if (string.IsNullOrWhiteSpace(lecturerName)) missingFields.Add("Lecturer name");
			if (string.IsNullOrWhiteSpace(lecturerCode)) missingFields.Add("Lecturer code");
			if (string.IsNullOrWhiteSpace(classCode)) missingFields.Add("Class code");

			if (missingFields.Count > 0)
			{
				result.FailureCount++;
				result.Errors.Add(new StudentSemesterImportErrorDto { RowIndex = itemNumber, StudentCode = studentCode, Reason = $"Missing required field(s): {string.Join(", ", missingFields)}." });
				continue;
			}

			if (!seenStudentCodes.Add(studentCode))
			{
				result.FailureCount++;
				result.Errors.Add(new StudentSemesterImportErrorDto { RowIndex = itemNumber, StudentCode = studentCode, Reason = $"Student code '{studentCode}' is duplicated in the request." });
				continue;
			}

			var student = await _userRepository.GetByStudentCodeAsync(studentCode, ct) as Student;
			if (student is null || student.IsDeleted)
			{
				result.FailureCount++;
				result.Errors.Add(new StudentSemesterImportErrorDto { RowIndex = itemNumber, StudentCode = studentCode, Reason = $"Student code '{studentCode}' was not found." });
				continue;
			}

			var exists = await _dbContext.StudentSemesters.AnyAsync(
				x => x.StudentId == student.Id && x.SemesterId == semesterId, ct);
			if (exists)
			{
				result.FailureCount++;
				result.Errors.Add(new StudentSemesterImportErrorDto { RowIndex = itemNumber, StudentCode = studentCode, Reason = $"Student code '{studentCode}' is already assigned to this semester." });
				continue;
			}

			var enrollment = new StudentSemester
			{
				StudentId = student.Id,
				SemesterId = semesterId,
				LecturerName = lecturerName,
				LecturerCode = lecturerCode,
				ClassCode = classCode
			};

			_dbContext.StudentSemesters.Add(enrollment);
			result.SuccessCount++;
			result.ImportedStudents.Add(ToStudentSemesterDto(enrollment, student));
		}

		await _unitOfWork.SaveChangesAsync(ct);
		return Result<StudentSemesterImportResultDto>.Success(result);
	}

	public async Task<Result<StudentSemesterImportResultDto>> ImportStudentsFromExcelAsync(Guid semesterId, Stream fileStream, CancellationToken ct = default)
	{
		if (await _repository.GetByIdAsync(semesterId, ct) is null)
			return Result<StudentSemesterImportResultDto>.Failure("Semester not found.", "SEMESTER_NOT_FOUND");

		var result = new StudentSemesterImportResultDto();

		try
		{
			using var workbook = new XLWorkbook(fileStream);
			var worksheet = workbook.Worksheets.FirstOrDefault();
			if (worksheet is null)
				return Result<StudentSemesterImportResultDto>.Failure("Excel file has no worksheet.", "EXCEL_EMPTY");

			var headers = GetHeaderMap(worksheet);
			foreach (var required in new[] { "studentCode", "lecturerName", "lecturerCode", "classCode" })
			{
				if (!headers.ContainsKey(required))
					return Result<StudentSemesterImportResultDto>.Failure(
						"Invalid Excel template. Required columns: student code, lecturer name, lecturer code, class code.",
						"INVALID_EXCEL_TEMPLATE");
			}

			var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
			var seenStudentCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			for (var row = 2; row <= lastRow; row++)
			{
				var studentCode = worksheet.Cell(row, headers["studentCode"]).GetString().Trim();
				var lecturerName = worksheet.Cell(row, headers["lecturerName"]).GetString().Trim();
				var lecturerCode = worksheet.Cell(row, headers["lecturerCode"]).GetString().Trim();
				var classCode = worksheet.Cell(row, headers["classCode"]).GetString().Trim();

				if (string.IsNullOrWhiteSpace(studentCode) && string.IsNullOrWhiteSpace(lecturerName) &&
					string.IsNullOrWhiteSpace(lecturerCode) && string.IsNullOrWhiteSpace(classCode))
					continue;

				result.TotalRows++;

				var missingFields = new List<string>();
				if (string.IsNullOrWhiteSpace(studentCode)) missingFields.Add("Student code");
				if (string.IsNullOrWhiteSpace(lecturerName)) missingFields.Add("Lecturer name");
				if (string.IsNullOrWhiteSpace(lecturerCode)) missingFields.Add("Lecturer code");
				if (string.IsNullOrWhiteSpace(classCode)) missingFields.Add("Class code");

				if (missingFields.Count > 0)
				{
					AddImportError(
						result,
						row,
						studentCode,
						$"Missing required field(s): {string.Join(", ", missingFields)}.");
					continue;
				}

				if (!seenStudentCodes.Add(studentCode))
				{
					AddImportError(result, row, studentCode, $"Student code '{studentCode}' is duplicated in the file.");
					continue;
				}

				var student = await _userRepository.GetByStudentCodeAsync(studentCode, ct) as Student;
				if (student is null || student.IsDeleted)
				{
					AddImportError(result, row, studentCode, $"Student code '{studentCode}' was not found.");
					continue;
				}

				if (await _dbContext.StudentSemesters.AnyAsync(x => x.StudentId == student.Id && x.SemesterId == semesterId, ct))
				{
					AddImportError(result, row, studentCode, $"Student code '{studentCode}' is already assigned to this semester.");
					continue;
				}

				_dbContext.StudentSemesters.Add(new StudentSemester
				{
					StudentId = student.Id,
					SemesterId = semesterId,
					LecturerName = lecturerName,
					LecturerCode = lecturerCode,
					ClassCode = classCode
				});
				result.SuccessCount++;
				result.ImportedStudents.Add(new StudentSemesterDTO
				{
					StudentId = student.Id,
					SemesterId = semesterId,
					StudentCode = student.StudentCode,
					FullName = student.FullName,
					LecturerName = lecturerName,
					LecturerCode = lecturerCode,
					ClassCode = classCode
				});
			}

			await _unitOfWork.SaveChangesAsync(ct);
			return Result<StudentSemesterImportResultDto>.Success(result);
		}
		catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
		{
			return Result<StudentSemesterImportResultDto>.Failure("The uploaded file is not a valid Excel workbook.", "INVALID_EXCEL_FILE");
		}
	}

	public byte[] GenerateStudentImportTemplate()
	{
		using var workbook = new XLWorkbook();
		var worksheet = workbook.Worksheets.Add("Students");

		worksheet.Cell(1, 1).Value = "Student Code (*)";
		worksheet.Cell(1, 2).Value = "Lecturer Name (*)";
		worksheet.Cell(1, 3).Value = "Lecturer Code (*)";
		worksheet.Cell(1, 4).Value = "Class Code (*)";

		var headerRange = worksheet.Range(1, 1, 1, 4);
		headerRange.Style.Font.Bold = true;
		headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(220, 230, 241);
		headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

		worksheet.Cell(2, 1).Value = "SE160001";
		worksheet.Cell(2, 2).Value = "Nguyen Van A";
		worksheet.Cell(2, 3).Value = "GV001";
		worksheet.Cell(2, 4).Value = "SE1601";

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
			else if (normalized.Contains("lecturer_name") || normalized.Contains("lecturername") || normalized.Contains("giang_vien")) map.TryAdd("lecturerName", column);
			else if (normalized.Contains("lecturer_code") || normalized.Contains("lecturercode") || normalized.Contains("ma_gv") || normalized.Contains("mã_gv")) map.TryAdd("lecturerCode", column);
			else if (normalized.Contains("class_code") || normalized.Contains("classcode") || normalized.Contains("ma_lop") || normalized.Contains("mã_lớp")) map.TryAdd("classCode", column);
		}
		return map;
	}

	private static void AddImportError(StudentSemesterImportResultDto result, int row, string? studentCode, string reason)
	{
		result.FailureCount++;
		result.Errors.Add(new StudentSemesterImportErrorDto { RowIndex = row, StudentCode = studentCode, Reason = reason });
	}

	private static StudentSemesterDTO ToStudentSemesterDto(StudentSemester enrollment, Student student) => new()
	{
		StudentId = student.Id,
		SemesterId = enrollment.SemesterId,
		StudentCode = student.StudentCode,
		FullName = student.FullName,
		LecturerName = enrollment.LecturerName,
		LecturerCode = enrollment.LecturerCode,
		ClassCode = enrollment.ClassCode
	};

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
