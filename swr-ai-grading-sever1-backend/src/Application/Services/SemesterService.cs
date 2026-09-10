using Application.Common;
using Application.DTOs.Semesters;
using Application.Interfaces;
using Domain.Entities;
using System.Text.RegularExpressions;

namespace Application.Services;

public class SemesterService : ISemesterService
{
	private readonly ISemesterRepository _repository;
	private readonly IUnitOfWork _unitOfWork;

	public SemesterService(ISemesterRepository repository, IUnitOfWork unitOfWork)
	{
		_repository = repository;
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

	public async Task<Result<SemesterDTO>> GetByIdAsync(Guid id, CancellationToken ct = default)
	{
		var semester = await _repository.GetByIdAsync(id, ct);
		return semester is null
			? Result<SemesterDTO>.Failure("Semester not found.", "SEMESTER_NOT_FOUND")
			: Result<SemesterDTO>.Success(ToDto(semester));
	}

	public async Task<Result<SemesterDTO>> CreateAsync(CreateSemesterRequest request, CancellationToken ct = default)
	{
		var validation = Validate(request.SemesterCode, request.Name, request.StartDate, request.EndDate);
		if (validation is not null)
			return Result<SemesterDTO>.Failure(validation, "INVALID_SEMESTER");

		var code = request.SemesterCode.Trim();
		if (await _repository.IsCodeExistsAsync(code, ct))
			return Result<SemesterDTO>.Failure("Semester code already exists.", "SEMESTER_CODE_EXISTS");

		var semester = new Semester
		{
			SemesterCode = code,
			Name = request.Name.Trim(),
			StartDate = request.StartDate,
			EndDate = request.EndDate,
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

		if (!string.Equals(code, semester.SemesterCode, StringComparison.OrdinalIgnoreCase)
			&& await _repository.IsCodeExistsAsync(code, ct))
			return Result<SemesterDTO>.Failure("Semester code already exists.", "SEMESTER_CODE_EXISTS");

		semester.SemesterCode = code;
		semester.Name = name;
		semester.StartDate = startDate;
		semester.EndDate = endDate;
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
		Status = semester.Status
	};
}
