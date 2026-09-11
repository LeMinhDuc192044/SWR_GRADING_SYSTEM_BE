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
    private readonly IExamMaterialRepository _examMaterialRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ExaminationService(
        IExaminationRepository repository,
        ISemesterRepository semesterRepository,
        IExamMaterialRepository examMaterialRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _semesterRepository = semesterRepository;
        _examMaterialRepository = examMaterialRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<ExaminationDTO>> GetPagedAsync(PagedRequest request, CancellationToken ct = default)
    {
        var examinations = await _repository.GetAllAsync(ct);
        var items = await Task.WhenAll(examinations
            .OrderByDescending(e => e.StartDate)
            .ThenBy(e => e.StartTime)
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

        var material = await _examMaterialRepository.GetByIdAsync(request.ExamMaterialId, ct);
        var materialValidation = ValidateMaterial(material, request.SemesterId);
        if (materialValidation is not null)
            return Result<ExaminationDTO>.Failure(materialValidation.Value.Message, materialValidation.Value.Code);

        var code = await GenerateCodeAsync(semester.SemesterCode, request.ExaminationType, ct);
        var examination = new Examination
        {
            ExaminationCode = code,
            Name = request.Name.Trim(),
            ExaminationType = request.ExaminationType,
            StartDate = request.StartDate,
            StartTime = request.StartTime,
            DurationMinutes = request.DurationMinutes,
            BeforeTimeMinutes = request.BeforeTimeMinutes,
            Note = request.Note?.Trim(),
            Status = request.Status,
            SemesterId = request.SemesterId
        };

        material!.ExaminationId = examination.ExaminationId;
        material.Status = ExamMaterialStatus.InUse;
        material.UpdatedDate = DateTime.UtcNow;
        _examMaterialRepository.Update(material);

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
        var duration = request.DurationMinutes ?? examination.DurationMinutes;
        var beforeTime = request.BeforeTimeMinutes ?? examination.BeforeTimeMinutes;
        var validation = Validate(name, type, duration, beforeTime);
        if (validation is not null)
            return Result<ExaminationDTO>.Failure(validation, "INVALID_EXAMINATION");

        var semesterId = request.SemesterId ?? examination.SemesterId;
        var semester = await _semesterRepository.GetByIdAsync(semesterId, ct);
        if (semester is null)
            return Result<ExaminationDTO>.Failure("Semester not found.", "SEMESTER_NOT_FOUND");

        var linkedMaterials = await _examMaterialRepository.FindAsync(
            material => material.ExaminationId == examination.ExaminationId && !material.IsDeleted,
            ct);
        if (linkedMaterials.Any(material => material.SemesterId != semesterId))
            return Result<ExaminationDTO>.Failure(
                "Examination semester must match the semester of its exam material.",
                "EXAM_MATERIAL_SEMESTER_MISMATCH");

        var materialId = request.ExamMaterialId;
        if (materialId.HasValue)
        {
            var material = await _examMaterialRepository.GetByIdAsync(materialId.Value, ct);
            var materialValidation = ValidateMaterial(material, semesterId);
            if (materialValidation is not null)
                return Result<ExaminationDTO>.Failure(materialValidation.Value.Message, materialValidation.Value.Code);

            material!.ExaminationId = examination.ExaminationId;
            material.Status = ExamMaterialStatus.InUse;
            material.UpdatedDate = DateTime.UtcNow;
            _examMaterialRepository.Update(material);
        }

        if (semesterId != examination.SemesterId || type != examination.ExaminationType)
            examination.ExaminationCode = await GenerateCodeAsync(semester.SemesterCode, type, ct);

        examination.Name = name;
        examination.ExaminationType = type;
        examination.StartDate = request.StartDate ?? examination.StartDate;
        examination.StartTime = request.StartTime ?? examination.StartTime;
        examination.DurationMinutes = duration;
        examination.BeforeTimeMinutes = beforeTime;
        if (request.Note is not null)
            examination.Note = request.Note.Trim();
        if (request.Status.HasValue)
            examination.Status = request.Status.Value;
        examination.SemesterId = semesterId;

        _repository.Update(examination);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<ExaminationDTO>.Success(await ToDtoAsync(examination));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var examination = await _repository.GetByIdAsync(id, ct);
        if (examination is null)
            return Result.Failure("Examination not found.", "EXAMINATION_NOT_FOUND");

        if (await _repository.HasExamMaterialsAsync(id, ct))
            return Result.Failure("Cannot delete an examination that has materials.", "EXAMINATION_HAS_MATERIALS");

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
        var material = (await _examMaterialRepository.FindAsync(m => m.ExaminationId == examination.ExaminationId && !m.IsDeleted)).FirstOrDefault();
        return new ExaminationDTO
        {
        ExaminationId = examination.ExaminationId,
        ExaminationCode = examination.ExaminationCode,
        Name = examination.Name,
        ExaminationType = examination.ExaminationType,
        StartDate = examination.StartDate,
        StartTime = examination.StartTime,
        DurationMinutes = examination.DurationMinutes,
        BeforeTimeMinutes = examination.BeforeTimeMinutes,
        Note = examination.Note,
        Status = examination.Status,
        SemesterId = examination.SemesterId,
        ExamMaterialId = material?.ExamMaterialId
        };
    }

    private static (string Message, string Code)? ValidateMaterial(ExamMaterial? material, Guid semesterId)
    {
        if (material is null || material.IsDeleted)
            return ("Exam material not found.", "EXAM_MATERIAL_NOT_FOUND");
        if (material.SemesterId != semesterId)
            return ("Exam material does not belong to the selected semester.", "EXAM_MATERIAL_SEMESTER_MISMATCH");
        return null;
    }
}