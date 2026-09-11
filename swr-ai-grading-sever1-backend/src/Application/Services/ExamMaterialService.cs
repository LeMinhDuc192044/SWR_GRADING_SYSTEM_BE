using Application.Common;
using Application.DTOs.ExamMaterials;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services;

public sealed class ExamMaterialService : IExamMaterialService
{
    private readonly IExamMaterialRepository _repository;
    private readonly IExaminationRepository _examinationRepository;
    private readonly ISemesterRepository _semesterRepository;
    private readonly ISupabaseStorage _storage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserRepository _userRepository;

    public ExamMaterialService(IExamMaterialRepository repository, IExaminationRepository examinationRepository, ISemesterRepository semesterRepository, ISupabaseStorage storage, IUnitOfWork unitOfWork, IUserRepository userRepository)
    { _repository = repository; _examinationRepository = examinationRepository; _semesterRepository = semesterRepository; _storage = storage; _unitOfWork = unitOfWork; _userRepository = userRepository; }

    public async Task<PagedResult<ExamMaterialMetadataDTO>> GetPagedAsync(PagedRequest request, CancellationToken ct = default)
    {
        var materials = (await _repository.GetAllAsync(ct)).Where(m => !m.IsDeleted).OrderByDescending(m => m.CreatedDate).ToList();
        var page = materials.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        var items = await Task.WhenAll(page.Select(ToMetadataAsync));
        return new PagedResult<ExamMaterialMetadataDTO> { Items = items, TotalCount = materials.Count, Page = request.Page, PageSize = request.PageSize };
    }

    public async Task<Result<ExamMaterialDetailDTO>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var material = await FindAsync(id, ct);
        return material is null ? Result<ExamMaterialDetailDTO>.Failure("Exam material not found.", "EXAM_MATERIAL_NOT_FOUND") : Result<ExamMaterialDetailDTO>.Success(await ToDetailAsync(material));
    }

    public async Task<Result<IReadOnlyList<ExamMaterialMetadataDTO>>> CreateAsync(Guid semesterId, Guid? examinationId, string description, int totalQuestions, IReadOnlyList<MaterialFileUpload> files, Guid createdById, CancellationToken ct = default)
    {
        if (files.Count == 0) return Result<IReadOnlyList<ExamMaterialMetadataDTO>>.Failure("At least one file is required.", "FILES_REQUIRED");
        var relationshipResult = await ValidateRelationshipAsync(semesterId, examinationId, ct);
        if (!relationshipResult.IsSuccess) return Result<IReadOnlyList<ExamMaterialMetadataDTO>>.Failure(relationshipResult.Error!, relationshipResult.ErrorCode);
        if (await _userRepository.GetLecturerByIdAsync(createdById, ct) is null) return Result<IReadOnlyList<ExamMaterialMetadataDTO>>.Failure("Only a lecturer can create exam materials.", "LECTURER_REQUIRED");
        if (totalQuestions < 0) return Result<IReadOnlyList<ExamMaterialMetadataDTO>>.Failure("Total questions cannot be negative.", "INVALID_TOTAL_QUESTIONS");
        var materialResult = await CreateMaterialAsync(semesterId, examinationId, description, totalQuestions, files, createdById, ct);
        if (!materialResult.IsSuccess) return Result<IReadOnlyList<ExamMaterialMetadataDTO>>.Failure(materialResult.Error!, materialResult.ErrorCode);
        await _repository.AddAsync(materialResult.Data!, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<IReadOnlyList<ExamMaterialMetadataDTO>>.Success(new[] { await ToMetadataAsync(materialResult.Data!) });
    }

    public async Task<Result<IReadOnlyList<ExamMaterialMetadataDTO>>> CreateManyAsync(
        Guid semesterId,
        Guid? examinationId,
        IReadOnlyList<CreateExamMaterialInput> materials,
        Guid createdById,
        CancellationToken ct = default)
    {
        if (materials.Count == 0) return Result<IReadOnlyList<ExamMaterialMetadataDTO>>.Failure("At least one material is required.", "MATERIALS_REQUIRED");
        if (materials.Any(material => material.Files.Count == 0)) return Result<IReadOnlyList<ExamMaterialMetadataDTO>>.Failure("Each material must contain at least one file.", "FILES_REQUIRED");
        if (materials.Any(material => material.TotalQuestions < 0)) return Result<IReadOnlyList<ExamMaterialMetadataDTO>>.Failure("Total questions cannot be negative.", "INVALID_TOTAL_QUESTIONS");
        var relationshipResult = await ValidateRelationshipAsync(semesterId, examinationId, ct);
        if (!relationshipResult.IsSuccess) return Result<IReadOnlyList<ExamMaterialMetadataDTO>>.Failure(relationshipResult.Error!, relationshipResult.ErrorCode);
        if (await _userRepository.GetLecturerByIdAsync(createdById, ct) is null) return Result<IReadOnlyList<ExamMaterialMetadataDTO>>.Failure("Only a lecturer can create exam materials.", "LECTURER_REQUIRED");

        var created = new List<ExamMaterial>();
        try
        {
            foreach (var materialInput in materials)
            {
                var materialResult = await CreateMaterialAsync(semesterId, examinationId, materialInput.Description, materialInput.TotalQuestions, materialInput.Files, createdById, ct);
                if (!materialResult.IsSuccess)
                    return Result<IReadOnlyList<ExamMaterialMetadataDTO>>.Failure(materialResult.Error!, materialResult.ErrorCode);

                created.Add(materialResult.Data!);
                await _repository.AddAsync(materialResult.Data!, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            return Result<IReadOnlyList<ExamMaterialMetadataDTO>>.Success(
                await Task.WhenAll(created.Select(ToMetadataAsync)));
        }
        catch
        {
            foreach (var material in created)
            {
                foreach (var path in GetPaths(material).Values)
                {
                    try { await _storage.DeleteAsync(path, CancellationToken.None); } catch { }
                }
            }

            throw;
        }
    }

    private async Task<Result<ExamMaterial>> CreateMaterialAsync(
        Guid semesterId,
        Guid? examinationId,
        string description,
        int totalQuestions,
        IReadOnlyList<MaterialFileUpload> files,
        Guid createdById,
        CancellationToken ct)
    {
        var material = new ExamMaterial { ExamMaterialCode = await GenerateCodeAsync(ct), Description = description.Trim(), TotalQuestions = totalQuestions, CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow, Status = ExamMaterialStatus.Ready, ExaminationId = examinationId, SemesterId = semesterId, CreateById = createdById };
        var uploadResult = await UploadFilesAsync(material, files, ct);
        if (!uploadResult.IsSuccess) return Result<ExamMaterial>.Failure(uploadResult.Error!, uploadResult.ErrorCode);
        return Result<ExamMaterial>.Success(material);
    }

    public async Task<Result<ExamMaterialDetailDTO>> AddFilesAsync(Guid id, IReadOnlyList<MaterialFileUpload> files, CancellationToken ct = default)
    {
        var material = await FindAsync(id, ct);
        if (material is null) return Result<ExamMaterialDetailDTO>.Failure("Exam material not found.", "EXAM_MATERIAL_NOT_FOUND");
        if (files.Count == 0) return Result<ExamMaterialDetailDTO>.Failure("At least one file is required.", "FILES_REQUIRED");
        var uploadResult = await UploadFilesAsync(material, files, ct);
        if (!uploadResult.IsSuccess) return Result<ExamMaterialDetailDTO>.Failure(uploadResult.Error!, uploadResult.ErrorCode);
        material.UpdatedDate = DateTime.UtcNow;
        _repository.Update(material);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<ExamMaterialDetailDTO>.Success(await ToDetailAsync(material));
    }

    public async Task<Result<ExamMaterialDetailDTO>> UpdateAsync(Guid id, UpdateExamMaterialRequest request, CancellationToken ct = default)
    {
        var material = await FindAsync(id, ct);
        if (material is null) return Result<ExamMaterialDetailDTO>.Failure("Exam material not found.", "EXAM_MATERIAL_NOT_FOUND");
        if (request.Status.HasValue && !Enum.IsDefined(request.Status.Value)) return Result<ExamMaterialDetailDTO>.Failure("Invalid exam material status.", "INVALID_STATUS");
        if (request.TotalQuestions is < 0) return Result<ExamMaterialDetailDTO>.Failure("Total questions cannot be negative.", "INVALID_TOTAL_QUESTIONS");
        var semesterId = request.SemesterId ?? material.SemesterId;
        var examinationId = request.ExaminationId ?? material.ExaminationId;
        var relationshipResult = await ValidateRelationshipAsync(semesterId, examinationId, ct);
        if (!relationshipResult.IsSuccess) return Result<ExamMaterialDetailDTO>.Failure(relationshipResult.Error!, relationshipResult.ErrorCode);
        material.SemesterId = semesterId;
        material.ExaminationId = examinationId;
        if (request.Description is not null) material.Description = request.Description.Trim();
        if (request.TotalQuestions.HasValue) material.TotalQuestions = request.TotalQuestions.Value;
        if (request.Status.HasValue) material.Status = request.Status.Value;
        material.UpdatedDate = DateTime.UtcNow;
        _repository.Update(material);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<ExamMaterialDetailDTO>.Success(await ToDetailAsync(material));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var material = await FindAsync(id, ct);
        if (material is null) return Result.Failure("Exam material not found.", "EXAM_MATERIAL_NOT_FOUND");
        foreach (var path in GetPaths(material).Values) await _storage.DeleteAsync(path, ct);
        material.IsDeleted = true; material.Status = ExamMaterialStatus.Archived; material.UpdatedDate = DateTime.UtcNow;
        _repository.Update(material); await _unitOfWork.SaveChangesAsync(ct); return Result.Success();
    }

    public async Task<Result<StoredFileDownload>> DownloadAsync(Guid id, ExamMaterialFileType fileType, CancellationToken ct = default)
    {
        var material = await FindAsync(id, ct);
        if (material is null) return Result<StoredFileDownload>.Failure("Exam material not found.", "EXAM_MATERIAL_NOT_FOUND");
        var paths = GetPaths(material);
        if (!paths.TryGetValue(fileType, out var path)) return Result<StoredFileDownload>.Failure("File type is not uploaded.", "FILE_NOT_FOUND");
        var metadata = await _storage.GetMetadataAsync(path, ct);
        return Result<StoredFileDownload>.Success(await _storage.DownloadAsync(path, metadata.FileName, ct));
    }

    private async Task<Result> UploadFilesAsync(ExamMaterial material, IReadOnlyList<MaterialFileUpload> files, CancellationToken ct)
    {
        // Validate every file BEFORE uploading any of them. Doing this inline inside
        // the upload loop (as before) meant a later file failing validation would
        // leave earlier, already-uploaded files orphaned in storage with nothing
        // referencing them, since a `return` here skips the catch/cleanup block below.
        foreach (var file in files)
        {
            if (!Enum.IsDefined(file.FileType) || string.IsNullOrWhiteSpace(file.FileName) || file.Length <= 0)
                return Result.Failure("Each file must have a valid type, name, and content.", "INVALID_FILE");
        }

        var uploaded = new List<string>();
        try
        {
            foreach (var file in files)
            {
                var path = $"examinations/{material.ExaminationId ?? material.SemesterId}/{material.ExamMaterialCode}/{GetTypeCode(file.FileType)}/{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
                try
                {
                    await _storage.UploadAsync(path, file.Content, file.ContentType, ct);
                }
                finally
                {
                    await file.Content.DisposeAsync();
                }
                uploaded.Add(path);
                SetPath(material, file.FileType, path);
            }
            return Result.Success();
        }
        catch { foreach (var path in uploaded) { try { await _storage.DeleteAsync(path, CancellationToken.None); } catch { } } throw; }
    }

    private async Task<ExamMaterial?> FindAsync(Guid id, CancellationToken ct) { var material = await _repository.GetByIdAsync(id, ct); return material is null || material.IsDeleted ? null : material; }
    private async Task<Result> ValidateRelationshipAsync(Guid semesterId, Guid? examinationId, CancellationToken ct)
    {
        if (await _semesterRepository.GetByIdAsync(semesterId, ct) is null)
            return Result.Failure("Semester not found.", "SEMESTER_NOT_FOUND");
        if (examinationId is null) return Result.Success();
        var examination = await _examinationRepository.GetByIdAsync(examinationId.Value, ct);
        if (examination is null) return Result.Failure("Examination not found.", "EXAMINATION_NOT_FOUND");
        return examination.SemesterId == semesterId
            ? Result.Success()
            : Result.Failure("Examination does not belong to the selected semester.", "EXAMINATION_SEMESTER_MISMATCH");
    }
    private async Task<string> GenerateCodeAsync(CancellationToken ct) { do { var code = $"EM{Random.Shared.Next(0, 1_000_000):D6}"; if (!await _repository.IsCodeExistsAsync(code, ct)) return code; } while (true); }

    // Fixed: AnswerRubric and AnswerTemplate previously both fell through to "AT",
    // filing rubric files under an "answer template" folder segment.
    private static string GetTypeCode(ExamMaterialFileType type) => type switch
    {
        ExamMaterialFileType.Question => "EQ",
        ExamMaterialFileType.AnswerRubric => "AR",
        ExamMaterialFileType.AnswerTemplate => "AT",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown exam material file type.")
    };

    private static void SetPath(ExamMaterial m, ExamMaterialFileType type, string path) { if (type == ExamMaterialFileType.Question) m.FileQuestionDocs = path; else if (type == ExamMaterialFileType.AnswerRubric) m.FileAnswerRubric = path; else m.FileAnswerTemplate = path; }
    private static Dictionary<ExamMaterialFileType, string> GetPaths(ExamMaterial m) => new[] { (ExamMaterialFileType.Question, m.FileQuestionDocs), (ExamMaterialFileType.AnswerRubric, m.FileAnswerRubric), (ExamMaterialFileType.AnswerTemplate, m.FileAnswerTemplate) }.Where(x => x.Item2 is not null).ToDictionary(x => x.Item1, x => x.Item2!);

    private async Task<ExamMaterialMetadataDTO> ToMetadataAsync(ExamMaterial material)
    {
        var files = await Task.WhenAll(GetPaths(material).Select(async pair => { var m = await _storage.GetMetadataAsync(pair.Value); return new ExamMaterialFileDTO { FileType = pair.Key, FileName = m.FileName, ContentType = m.ContentType, FileSize = m.FileSize }; }));
        return new ExamMaterialMetadataDTO { ExamMaterialId = material.ExamMaterialId, ExamMaterialCode = material.ExamMaterialCode, Description = material.Description, TotalQuestions = material.TotalQuestions, Files = files, Status = material.Status, ExaminationId = material.ExaminationId, SemesterId = material.SemesterId, CreatedDate = material.CreatedDate, UpdatedDate = material.UpdatedDate };
    }

    private async Task<ExamMaterialDetailDTO> ToDetailAsync(ExamMaterial material)
    {
        var metadata = await ToMetadataAsync(material);
        return new ExamMaterialDetailDTO { ExamMaterialId = metadata.ExamMaterialId, ExamMaterialCode = metadata.ExamMaterialCode, Description = metadata.Description, TotalQuestions = metadata.TotalQuestions, Files = metadata.Files, Status = metadata.Status, ExaminationId = metadata.ExaminationId, SemesterId = metadata.SemesterId, CreatedDate = metadata.CreatedDate, UpdatedDate = metadata.UpdatedDate, StoragePath = string.Join(',', GetPaths(material).Values) };
    }
}