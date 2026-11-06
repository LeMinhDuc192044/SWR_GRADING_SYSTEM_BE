using Application.Common;
using Application.DTOs.Examinations;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services;

public class ExaminationService : IExaminationService
{
    private const string DefaultCourseCode = "SWR302";
    private readonly IExaminationRepository _repository;
    private readonly ISemesterRepository _semesterRepository;
    private readonly IPaperSetRepository _paperSetRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ExaminationService(
        IExaminationRepository repository,
        ISemesterRepository semesterRepository,
        IPaperSetRepository paperSetRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _semesterRepository = semesterRepository;
        _paperSetRepository = paperSetRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<ExaminationDTO>> GetPagedAsync(PagedRequest request, CancellationToken ct = default)
    {
        var examinations = await _repository.GetAllAsync(ct);
        var items = await Task.WhenAll(examinations
            .OrderByDescending(e => e.StartDate)
            // .ThenBy(e => e.StartTime)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(ToDtoAsync));

        return new PagedResult<ExaminationDTO>
        {
            Items = items,
            TotalCount = examinations.Count,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<Result<ExaminationDTO>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var examination = await _repository.GetByIdAsync(id, ct);
        return examination is null
            ? Result<ExaminationDTO>.Failure("Examination not found.", "EXAMINATION_NOT_FOUND")
            : Result<ExaminationDTO>.Success(await ToDtoAsync(examination));
    }

    public async Task<Result<ExaminationDTO>> CreateAsync(CreateExaminationRequest request, CancellationToken ct = default)
    {
        var validation = Validate(request.Name, request.ExaminationType, request.DurationMinutes, request.BeforeTimeMinutes);
        if (validation is not null)
            return Result<ExaminationDTO>.Failure(validation, "INVALID_EXAMINATION");

        var semester = await _semesterRepository.GetByIdAsync(request.SemesterId, ct);
        if (semester is null)
            return Result<ExaminationDTO>.Failure("Semester not found.", "SEMESTER_NOT_FOUND");

        if (request.StartDate < semester.StartDate || request.StartDate > semester.EndDate)
            return Result<ExaminationDTO>.Failure(
                "Examination start date must be within the semester dates.",
                "EXAMINATION_DATE_OUT_OF_SEMESTER");

        var requestedPaperSetIds = GetRequestedPaperSetIds(request.PaperSetIds, null);

        var materials = new List<PaperSet>();
        foreach (var paperSetId in requestedPaperSetIds)
        {
            var material = await _paperSetRepository.GetByIdAsync(paperSetId, ct);
            var materialValidation = ValidateMaterial(material, request.SemesterId);
            if (materialValidation is not null)
                return Result<ExaminationDTO>.Failure(materialValidation.Value.Message, materialValidation.Value.Code);
            materials.Add(material!);
        }

        var endDate = request.EndDate == default ? request.StartDate : request.EndDate;
        if (endDate < request.StartDate)
            return Result<ExaminationDTO>.Failure("Examination end date must be on or after start date.", "INVALID_EXAMINATION");
        if (endDate < semester.StartDate || endDate > semester.EndDate)
            return Result<ExaminationDTO>.Failure("Examination end date must be within the semester dates.", "EXAMINATION_DATE_OUT_OF_SEMESTER");

        var now = VietnamTime.Now;
        var examination = new Examination
        {
            Name = request.Name.Trim(),
            ExaminationType = request.ExaminationType,
            StartDate = request.StartDate,
            EndDate = endDate,
            CreatedDay = now,
            UpdatedDay = now,
            Status = request.Status,
            SemesterId = request.SemesterId
        };

        foreach (var material in materials)
        {
            material.ExaminationId = examination.ExaminationId;
            material.Status = PaperSetStatus.Used;
            material.UpdatedDate = VietnamTime.Now;
            _paperSetRepository.Update(material);
        }

        await _repository.AddAsync(examination, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<ExaminationDTO>.Success(await ToDtoAsync(examination));
    }

    public async Task<Result<ExaminationDTO>> UpdateAsync(Guid id, UpdateExaminationRequest request, CancellationToken ct = default)
    {
        var examination = await _repository.GetByIdAsync(id, ct);
        if (examination is null)
            return Result<ExaminationDTO>.Failure("Examination not found.", "EXAMINATION_NOT_FOUND");

        var name = request.Name?.Trim() ?? examination.Name;
        var type = request.ExaminationType ?? examination.ExaminationType;
 

        var semesterId = request.SemesterId ?? examination.SemesterId;
        var semester = await _semesterRepository.GetByIdAsync(semesterId, ct);
        if (semester is null)
            return Result<ExaminationDTO>.Failure("Semester not found.", "SEMESTER_NOT_FOUND");

        var startDate = request.StartDate ?? examination.StartDate;
        var endDate = request.EndDate ?? (examination.EndDate == default ? startDate : examination.EndDate);
        if (endDate < startDate)
            return Result<ExaminationDTO>.Failure("Examination end date must be on or after start date.", "INVALID_EXAMINATION");
        if (startDate < semester.StartDate || startDate > semester.EndDate ||
            endDate < semester.StartDate || endDate > semester.EndDate)
            return Result<ExaminationDTO>.Failure(
                "Examination dates must be within the semester dates.",
                "EXAMINATION_DATE_OUT_OF_SEMESTER");

        var linkedMaterials = await _paperSetRepository.FindAsync(
            material => material.ExaminationId == examination.ExaminationId && !material.IsDeleted,
            ct);

        var requestedPaperSetIds = request.PaperSetIds is not null || request.PaperSetId.HasValue
            ? GetRequestedPaperSetIds(request.PaperSetIds, request.PaperSetId)
            : null;

        if (requestedPaperSetIds is not null)
        {
            if (requestedPaperSetIds.Count == 0)
                return Result<ExaminationDTO>.Failure("At least one paper set is required.", "PAPER_SET_NOT_FOUND");
            var selectedMaterials = new List<PaperSet>();
            foreach (var paperSetId in requestedPaperSetIds)
            {
                var material = await _paperSetRepository.GetByIdAsync(paperSetId, ct);
                var materialValidation = ValidateMaterial(material, semesterId);
                if (materialValidation is not null)
                    return Result<ExaminationDTO>.Failure(materialValidation.Value.Message, materialValidation.Value.Code);
                selectedMaterials.Add(material!);
            }

            var selectedIds = selectedMaterials.Select(material => material.PaperSetId).ToHashSet();
            foreach (var linkedMaterial in linkedMaterials.Where(linked => !selectedIds.Contains(linked.PaperSetId)))
            {
                linkedMaterial.ExaminationId = null;
                linkedMaterial.Status = PaperSetStatus.Ready;
                linkedMaterial.UpdatedDate = VietnamTime.Now;
                _paperSetRepository.Update(linkedMaterial);
            }

            foreach (var material in selectedMaterials)
            {
                material.ExaminationId = examination.ExaminationId;
                material.Status = PaperSetStatus.Used;
                material.UpdatedDate = VietnamTime.Now;
                _paperSetRepository.Update(material);
            }
        }

        examination.Name = name;
        examination.ExaminationType = type;
        examination.StartDate = startDate;
        examination.EndDate = endDate;

        examination.UpdatedDay = VietnamTime.Now;
        if (request.Status.HasValue)
            examination.Status = request.Status.Value;
        examination.SemesterId = semesterId;

        _repository.Update(examination);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<ExaminationDTO>.Success(await ToDtoAsync(examination));
    }

    public async Task<Result<ExaminationDTO>> AddPaperSetsAsync(Guid id, AddPaperSetsToExaminationRequest request, CancellationToken ct = default)
    {
        var examination = await _repository.GetByIdAsync(id, ct);
        if (examination is null)
            return Result<ExaminationDTO>.Failure("Examination not found.", "EXAMINATION_NOT_FOUND");

        var requestedPaperSetIds = GetRequestedPaperSetIds(request.PaperSetIds, null);
        if (requestedPaperSetIds.Count == 0)
            return Result<ExaminationDTO>.Failure("At least one paper set is required.", "PAPER_SET_NOT_FOUND");

        foreach (var paperSetId in requestedPaperSetIds)
        {
            var paperSet = await _paperSetRepository.GetByIdAsync(paperSetId, ct);
            var validation = ValidateMaterial(paperSet, examination.SemesterId);
            if (validation is not null)
                return Result<ExaminationDTO>.Failure(validation.Value.Message, validation.Value.Code);

            if (paperSet!.ExaminationId != examination.ExaminationId)
            {
                paperSet.ExaminationId = examination.ExaminationId;
                paperSet.Status = PaperSetStatus.Used;
                paperSet.UpdatedDate = VietnamTime.Now;
                _paperSetRepository.Update(paperSet);
            }
        }

        examination.UpdatedDay = VietnamTime.Now;
        _repository.Update(examination);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<ExaminationDTO>.Success(await ToDtoAsync(examination));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var examination = await _repository.GetByIdAsync(id, ct);
        if (examination is null)
            return Result.Failure("Examination not found.", "EXAMINATION_NOT_FOUND");

        if (await _repository.HasPaperSetsAsync(id, ct))
            return Result.Failure("Cannot delete an examination that has paper sets.", "EXAMINATION_HAS_MATERIALS");

        _repository.Remove(examination);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<string> GenerateCodeAsync(string semesterCode, ExaminationType type, CancellationToken ct)
    {
        var typeCode = type switch
        {
            ExaminationType.RE => "RE",
            ExaminationType.PE => "PE",
            ExaminationType.ThreeW => "3W",
            ExaminationType.RE_3W => "RE_3W",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown examination type.")
        };

        do
        {
            var suffix = Random.Shared.Next(0, 1_000_000).ToString("D6");
            var code = $"{DefaultCourseCode}_{semesterCode}_{typeCode}_{suffix}";
            if (!await _repository.IsCodeExistsAsync(code, ct))
                return code;
        } while (true);
    }

    private static string? Validate(string name, ExaminationType type, int duration, int beforeTime)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Examination name is required.";
        if (!Enum.IsDefined(type))
            return "Invalid examination type.";
        if (duration <= 0)
            return "Duration must be greater than 0.";
        if (beforeTime < 0)
            return "Before-time minutes cannot be negative.";
        return null;
    }

    private async Task<ExaminationDTO> ToDtoAsync(Examination examination)
    {
        var materials = await _paperSetRepository.FindAsync(m => m.ExaminationId == examination.ExaminationId && !m.IsDeleted);
        var paperSetIds = materials.Select(material => material.PaperSetId).ToList();
        return new ExaminationDTO
        {
            ExaminationId = examination.ExaminationId,
            Name = examination.Name,
            ExaminationType = examination.ExaminationType,
            StartDate = examination.StartDate,
            EndDate = examination.EndDate,
            CreatedDay = examination.CreatedDay,
            UpdatedDay = examination.UpdatedDay,
            Status = examination.Status,
            SemesterId = examination.SemesterId,
            PaperSetIds = paperSetIds
        };
    }

    private static (string Message, string Code)? ValidateMaterial(PaperSet? material, Guid semesterId)
    {
        if (material is null || material.IsDeleted)
            return ("Paper set not found.", "PAPER_SET_NOT_FOUND");
        return null;
    }

    private static List<Guid> GetRequestedPaperSetIds(IEnumerable<Guid>? paperSetIds, Guid? legacyPaperSetId)
    {
        var ids = paperSetIds
            ?.Where(id => id != Guid.Empty)
            .ToList() ?? [];

        if (legacyPaperSetId.HasValue && legacyPaperSetId.Value != Guid.Empty)
            ids.Add(legacyPaperSetId.Value);

        return ids.Distinct().ToList();
    }
}