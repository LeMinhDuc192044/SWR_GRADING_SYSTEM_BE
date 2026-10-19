using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.PaperSets;
using Application.DTOs.Questions;
using Application.DTOs.Rubrics;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class PaperSetService : IPaperSetService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private readonly IPaperSetRepository _repository;
    private readonly IExaminationRepository _examinationRepository;
    private readonly ISemesterRepository _semesterRepository;
    private readonly ISupabaseStorage _storage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserRepository _userRepository;
    private readonly IApplicationDbContext _dbContext;

    public PaperSetService(
        IPaperSetRepository repository,
        IExaminationRepository examinationRepository,
        ISemesterRepository semesterRepository,
        ISupabaseStorage storage,
        IUnitOfWork unitOfWork,
        IUserRepository userRepository,
        IApplicationDbContext dbContext)
    {
        _repository = repository;
        _examinationRepository = examinationRepository;
        _semesterRepository = semesterRepository;
        _storage = storage;
        _unitOfWork = unitOfWork;
        _userRepository = userRepository;
        _dbContext = dbContext;
    }

    public Task<PagedResult<PaperSetMetadataDTO>> GetPagedAsync(PagedRequest request, CancellationToken ct = default)
        => GetPagedInternalAsync(request, null, ct);

    public Task<PagedResult<PaperSetMetadataDTO>> GetLecturerByIdAsync(Guid lecturerId, PagedRequest request, CancellationToken ct = default)
        => GetPagedInternalAsync(request, lecturerId, ct);

    private async Task<PagedResult<PaperSetMetadataDTO>> GetPagedInternalAsync(PagedRequest request, Guid? lecturerId, CancellationToken ct)
    {
        var materials = (await _repository.GetAllAsync(ct))
            .Where(m => !m.IsDeleted && (!lecturerId.HasValue || m.CreateById == lecturerId.Value))
            .OrderByDescending(m => m.CreatedDate)
            .ToList();
        var page = materials.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        var items = await Task.WhenAll(page.Select(ToMetadataAsync));
        return new PagedResult<PaperSetMetadataDTO> { Items = items, TotalCount = materials.Count, Page = request.Page, PageSize = request.PageSize };
    }

    public async Task<Result<PaperSetDetailDTO>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var material = await FindAsync(id, ct);
        return material is null ? Result<PaperSetDetailDTO>.Failure("Paper set not found.", "PAPER_SET_NOT_FOUND") : Result<PaperSetDetailDTO>.Success(await ToDetailAsync(material));
    }

    public Task<Result<IReadOnlyList<CreateQuestionInput>>> PreviewQuestionsAsync(MaterialFileUpload file, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(file.FileName);
        return Task.FromResult(string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase)
            ? PaperSetQuestionDocumentParser.Parse(file)
            : PaperSetQuestionDocumentParser.ParseOcr(file));
    }

    public async Task<Result<PaperSetPreviewResultDTO>> PreviewPaperSetAsync(
        MaterialFileUpload questionFile,
        MaterialFileUpload? rubricFile,
        CancellationToken ct = default)
    {
        var extension = Path.GetExtension(questionFile.FileName);
        var questionResult = string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase)
            ? PaperSetQuestionDocumentParser.Parse(questionFile)
            : PaperSetQuestionDocumentParser.ParseOcr(questionFile);

        if (!questionResult.IsSuccess)
        {
            return Result<PaperSetPreviewResultDTO>.Failure(questionResult.Error!, questionResult.ErrorCode);
        }

        var questions = questionResult.Data!;
        IReadOnlyList<ParsedRubricDto> rubrics = [];

        if (rubricFile is not null && rubricFile.Length > 0)
        {
            var rubricResult = PaperSetRubricParser.Parse(rubricFile.Content);
            if (rubricResult.IsSuccess)
            {
                rubrics = rubricResult.Data!;
            }
        }

        var diagrams = await _dbContext.Diagrams.ToListAsync(ct);
        var preview = BuildPreviewResult(questions, rubrics, diagrams);
        return Result<PaperSetPreviewResultDTO>.Success(preview);
    }

    public Task<Result<PaperSetExtractedFileDTO>> ReadDocumentAsync(MaterialFileUpload file, CancellationToken ct = default)
    {
        var result = PaperSetDocumentReader.Read(file);
        return Task.FromResult(result.IsSuccess
            ? Result<PaperSetExtractedFileDTO>.Success(new PaperSetExtractedFileDTO
            {
                FileName = file.FileName,
                Text = result.Data!
            })
            : Result<PaperSetExtractedFileDTO>.Failure(result.Error!, result.ErrorCode));
    }

    public async Task<Result<PaperSetExtractedContentsDTO>> ReadFilesAsync(Guid id, CancellationToken ct = default)
    {
        var material = await FindAsync(id, ct);
        if (material is null)
            return Result<PaperSetExtractedContentsDTO>.Failure("Paper set not found.", "PAPER_SET_NOT_FOUND");

        var extractedFiles = new List<PaperSetExtractedFileDTO>();
        foreach (var (fileType, path) in GetPaths(material))
        {
            var metadata = await _storage.GetMetadataAsync(path, ct);
            var download = await _storage.DownloadAsync(path, metadata.FileName, ct);
            await using (download.Content)
            {
                var result = PaperSetDocumentReader.Read(new MaterialFileUpload
                {
                    FileType = fileType,
                    Content = download.Content,
                    FileName = download.FileName,
                    ContentType = download.ContentType,
                    Length = metadata.FileSize
                });

                if (!result.IsSuccess)
                    return Result<PaperSetExtractedContentsDTO>.Failure(
                        $"Could not read {fileType} file '{metadata.FileName}': {result.Error}", result.ErrorCode);

                extractedFiles.Add(new PaperSetExtractedFileDTO
                {
                    FileType = fileType,
                    FileName = metadata.FileName,
                    Text = result.Data!
                });
            }
        }

        return Result<PaperSetExtractedContentsDTO>.Success(new PaperSetExtractedContentsDTO
        {
            PaperSetId = id,
            Files = extractedFiles
        });
    }

    public async Task<Result<IReadOnlyList<PaperSetMetadataDTO>>> CreateAsync(Guid semesterId, string description, IReadOnlyList<CreateQuestionInput> questions, IReadOnlyList<MaterialFileUpload> files, Guid createdById, CancellationToken ct = default)
    {
        if (files.Count == 0) return Result<IReadOnlyList<PaperSetMetadataDTO>>.Failure("At least one file is required.", "FILES_REQUIRED");
        var fileValidation = ValidateFiles(files);
        if (!fileValidation.IsSuccess) return Result<IReadOnlyList<PaperSetMetadataDTO>>.Failure(fileValidation.Error!, fileValidation.ErrorCode);
        var questionResult = ParseQuestionDocument(questions, files);
        if (!questionResult.IsSuccess) return Result<IReadOnlyList<PaperSetMetadataDTO>>.Failure(questionResult.Error!, questionResult.ErrorCode);
        var questionValidation = ValidateQuestions(questionResult.Data!);
        if (!questionValidation.IsSuccess) return Result<IReadOnlyList<PaperSetMetadataDTO>>.Failure(questionValidation.Error!, questionValidation.ErrorCode);
        var semesterValidation = await ValidateSemesterForUploadAsync(semesterId, ct);
        if (!semesterValidation.IsSuccess) return Result<IReadOnlyList<PaperSetMetadataDTO>>.Failure(semesterValidation.Error!, semesterValidation.ErrorCode);
        if (await _userRepository.GetLecturerByIdAsync(createdById, ct) is null) return Result<IReadOnlyList<PaperSetMetadataDTO>>.Failure("Only a lecturer can create paper sets.", "LECTURER_REQUIRED");
        var materialResult = await CreateMaterialAsync(semesterId, description, questionResult.Data!, files, createdById, ct);
        if (!materialResult.IsSuccess) return Result<IReadOnlyList<PaperSetMetadataDTO>>.Failure(materialResult.Error!, materialResult.ErrorCode);
        await _repository.AddAsync(materialResult.Data!, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<IReadOnlyList<PaperSetMetadataDTO>>.Success(new[] { await ToMetadataAsync(materialResult.Data!) });
    }

    public async Task<Result<IReadOnlyList<PaperSetMetadataDTO>>> CreateManyAsync(
        Guid semesterId,
        IReadOnlyList<CreatePaperSetInput> materials,
        Guid createdById,
        CancellationToken ct = default)
    {
        if (materials.Count == 0) return Result<IReadOnlyList<PaperSetMetadataDTO>>.Failure("At least one paper set is required.", "MATERIALS_REQUIRED");
        if (materials.Any(material => material.Files.Count == 0)) return Result<IReadOnlyList<PaperSetMetadataDTO>>.Failure("Each paper set must contain at least one file.", "FILES_REQUIRED");
        var fileValidation = ValidateFiles(materials.SelectMany(material => material.Files));
        if (!fileValidation.IsSuccess) return Result<IReadOnlyList<PaperSetMetadataDTO>>.Failure(fileValidation.Error!, fileValidation.ErrorCode);
        var parsedMaterials = new List<(CreatePaperSetInput Input, IReadOnlyList<CreateQuestionInput> Questions)>();
        foreach (var material in materials)
        {
            var questionResult = ParseQuestionDocument(material.Questions, material.Files);
            if (!questionResult.IsSuccess) return Result<IReadOnlyList<PaperSetMetadataDTO>>.Failure(questionResult.Error!, questionResult.ErrorCode);
            if (!ValidateQuestions(questionResult.Data!).IsSuccess) return Result<IReadOnlyList<PaperSetMetadataDTO>>.Failure("Each question must have a title, content, and non-negative point.", "INVALID_QUESTION");
            parsedMaterials.Add((material, questionResult.Data!));
        }
        var semesterValidation = await ValidateSemesterForUploadAsync(semesterId, ct);
        if (!semesterValidation.IsSuccess) return Result<IReadOnlyList<PaperSetMetadataDTO>>.Failure(semesterValidation.Error!, semesterValidation.ErrorCode);
        if (await _userRepository.GetLecturerByIdAsync(createdById, ct) is null) return Result<IReadOnlyList<PaperSetMetadataDTO>>.Failure("Only a lecturer can create paper sets.", "LECTURER_REQUIRED");

        var created = new List<PaperSet>();
        try
        {
            foreach (var parsedMaterial in parsedMaterials)
            {
                var materialResult = await CreateMaterialAsync(semesterId, parsedMaterial.Input.Description, parsedMaterial.Questions, parsedMaterial.Input.Files, createdById, ct);
                if (!materialResult.IsSuccess)
                    return Result<IReadOnlyList<PaperSetMetadataDTO>>.Failure(materialResult.Error!, materialResult.ErrorCode);

                created.Add(materialResult.Data!);
                await _repository.AddAsync(materialResult.Data!, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            return Result<IReadOnlyList<PaperSetMetadataDTO>>.Success(
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

    private static Result<IReadOnlyList<CreateQuestionInput>> ParseQuestionDocument(
        IReadOnlyList<CreateQuestionInput> questions,
        IReadOnlyList<MaterialFileUpload> files)
    {
        if (questions.Any(question =>
                !string.IsNullOrWhiteSpace(question.Title) ||
                !string.IsNullOrWhiteSpace(question.Content) ||
                question.Point != 0))
            return Result<IReadOnlyList<CreateQuestionInput>>.Success(questions);

        var questionFile = files.FirstOrDefault(file => file.FileType == PaperSetFileType.Question);
        return questionFile is null
            ? Result<IReadOnlyList<CreateQuestionInput>>.Success(questions)
            : string.Equals(Path.GetExtension(questionFile.FileName), ".docx", StringComparison.OrdinalIgnoreCase)
                ? PaperSetQuestionDocumentParser.Parse(questionFile)
                : PaperSetQuestionDocumentParser.ParseOcr(questionFile);
    }

    private async Task<Result<PaperSet>> CreateMaterialAsync(
        Guid semesterId,
        string description,
        IReadOnlyList<CreateQuestionInput> questions,
        IReadOnlyList<MaterialFileUpload> files,
        Guid createdById,
        CancellationToken ct)
    {
        var material = new PaperSet
        {
            PaperSetCode = await GenerateCodeAsync(ct),
            Description = description.Trim(),
            TotalQuestions = questions.Count,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
            Status = PaperSetStatus.Ready,
            SemesterId = semesterId,
            CreateById = createdById
        };

        var now = DateTime.UtcNow;

        // Parse Answer Rubric if present
        var rubricFile = files.FirstOrDefault(f => f.FileType == PaperSetFileType.AnswerRubric);
        IReadOnlyList<ParsedRubricDto> parsedRubrics = [];
        if (rubricFile is not null && rubricFile.Length > 0)
        {
            var rubricResult = PaperSetRubricParser.Parse(rubricFile.Content);
            if (rubricResult.IsSuccess)
            {
                parsedRubrics = rubricResult.Data!;
            }
        }

        var diagrams = await _dbContext.Diagrams.ToListAsync(ct);
        var rubricMap = parsedRubrics.ToDictionary(
            r => NormalizeQuestionKey(r.QuestionNumber),
            r => r,
            StringComparer.OrdinalIgnoreCase);
        var usedRubricKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var questionEntities = new List<Question>();
        for (int i = 0; i < questions.Count; i++)
        {
            var qInput = questions[i];
            var qNum = qInput.QuestionNumber > 0 ? qInput.QuestionNumber : (i + 1);
            var qKey = qNum.ToString();

            var diagramId = MatchDiagramId(qInput.DetectedDiagramType, diagrams);

            var question = new Question
            {
                Title = qInput.Title.Trim(),
                Content = qInput.Content.Trim(),
                Point = qInput.Point,
                CreatedDay = now,
                UpdatedDay = now,
                PaperSet = material,
                DiagramId = diagramId
            };

            ParsedRubricDto? matchedRubric = null;
            if (rubricMap.TryGetValue(qKey, out var rDirect))
            {
                matchedRubric = rDirect;
                usedRubricKeys.Add(qKey);
            }
            else if (i < parsedRubrics.Count && !usedRubricKeys.Contains(NormalizeQuestionKey(parsedRubrics[i].QuestionNumber)))
            {
                matchedRubric = parsedRubrics[i];
                usedRubricKeys.Add(NormalizeQuestionKey(parsedRubrics[i].QuestionNumber));
            }

            if (matchedRubric is not null)
            {
                var jsonContent = JsonSerializer.Serialize(matchedRubric.SubItems, JsonOptions);
                var qRubric = new QuestionRubric
                {
                    RubricId = Guid.NewGuid(),
                    Content = jsonContent,
                    Point = matchedRubric.TotalPoint,
                    PaperSet = material,
                    PaperSetId = material.PaperSetId,
                    DiagramId = diagramId
                };

                material.Rubrics.Add(qRubric);
                question.Rubric = qRubric;
                question.RubricId = qRubric.RubricId;
            }

            questionEntities.Add(question);
        }

        material.Questions = questionEntities;

        var uploadResult = await UploadFilesAsync(material, files, ct);
        if (!uploadResult.IsSuccess) return Result<PaperSet>.Failure(uploadResult.Error!, uploadResult.ErrorCode);
        return Result<PaperSet>.Success(material);
    }

    private static PaperSetPreviewResultDTO BuildPreviewResult(
        IReadOnlyList<CreateQuestionInput> questions,
        IReadOnlyList<ParsedRubricDto> rubrics,
        IReadOnlyList<Diagram> diagrams)
    {
        var result = new PaperSetPreviewResultDTO();
        var questionPreviewList = new List<QuestionPreviewDto>();

        var rubricMap = rubrics.ToDictionary(
            r => NormalizeQuestionKey(r.QuestionNumber),
            r => r,
            StringComparer.OrdinalIgnoreCase);

        var usedRubricKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < questions.Count; i++)
        {
            var q = questions[i];
            var qNum = q.QuestionNumber > 0 ? q.QuestionNumber : (i + 1);
            var qKey = qNum.ToString();

            ParsedRubricDto? matchedRubric = null;
            if (rubricMap.TryGetValue(qKey, out var rDirect))
            {
                matchedRubric = rDirect;
                usedRubricKeys.Add(qKey);
            }
            else if (i < rubrics.Count && !usedRubricKeys.Contains(NormalizeQuestionKey(rubrics[i].QuestionNumber)))
            {
                matchedRubric = rubrics[i];
                usedRubricKeys.Add(NormalizeQuestionKey(rubrics[i].QuestionNumber));
            }

            var diagramId = MatchDiagramId(q.DetectedDiagramType, diagrams);

            var qPreview = new QuestionPreviewDto
            {
                QuestionNumber = qNum,
                Title = q.Title,
                Content = q.Content,
                Point = q.Point,
                DetectedDiagramType = q.DetectedDiagramType,
                DiagramId = diagramId,
                Rubric = matchedRubric
            };

            if (matchedRubric is null)
            {
                if (rubrics.Count > 0)
                {
                    result.Warnings.Add($"Câu '{q.Title}' ({q.Point:0.##}đ) chưa có tiêu chí chi tiết trong file rubric.");
                }
            }
            else
            {
                if (Math.Abs(q.Point - matchedRubric.TotalPoint) > 0.001m)
                {
                    qPreview.IsScoreMatched = false;
                    qPreview.ScoreDifferenceMessage = $"Điểm đề bài là {q.Point:0.##}đ nhưng tổng điểm rubric là {matchedRubric.TotalPoint:0.##}đ.";
                    result.Warnings.Add($"Lệch điểm tại '{q.Title}': Đề bài là {q.Point:0.##}đ nhưng tổng rubric là {matchedRubric.TotalPoint:0.##}đ.");
                }
            }

            questionPreviewList.Add(qPreview);
        }

        foreach (var r in rubrics)
        {
            var key = NormalizeQuestionKey(r.QuestionNumber);
            if (!usedRubricKeys.Contains(key))
            {
                result.Warnings.Add($"File rubric có mục 'Question {r.QuestionNumber}' ({r.TotalPoint:0.##}đ) nhưng không tìm thấy câu hỏi tương ứng trong đề thi.");
            }
        }

        result.Questions = questionPreviewList;
        result.TotalQuestionsScore = questions.Sum(q => q.Point);
        result.TotalRubricsScore = rubrics.Sum(r => r.TotalPoint);

        if (Math.Abs(result.TotalQuestionsScore - 10.0m) > 0.001m)
        {
            result.Warnings.Add($"Tổng điểm các câu hỏi trong đề thi là {result.TotalQuestionsScore:0.##}/10.0đ (Chưa đúng thang điểm 10 chuẩn).");
        }

        result.IsValid = result.Warnings.Count == 0 && result.Errors.Count == 0;
        return result;
    }

    private static Guid? MatchDiagramId(string? detectedType, IReadOnlyList<Diagram> diagrams)
    {
        if (string.IsNullOrWhiteSpace(detectedType) || diagrams.Count == 0) return null;
        var lower = detectedType.ToLowerInvariant();
        var match = diagrams.FirstOrDefault(d =>
            d.Name.Contains(detectedType, StringComparison.OrdinalIgnoreCase) ||
            (lower.Contains("context") && d.Name.Contains("context", StringComparison.OrdinalIgnoreCase)) ||
            (lower.Contains("use case") && d.Name.Contains("use case", StringComparison.OrdinalIgnoreCase)) ||
            (lower.Contains("erd") && d.Name.Contains("erd", StringComparison.OrdinalIgnoreCase)) ||
            (lower.Contains("swimlane") && d.Name.Contains("swimlane", StringComparison.OrdinalIgnoreCase)));
        return match?.DiagramId;
    }

    private static string NormalizeQuestionKey(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var digits = System.Text.RegularExpressions.Regex.Match(raw, @"\d+");
        return digits.Success ? digits.Value : raw.Trim();
    }

    private async Task<Result> ValidateSemesterForUploadAsync(Guid semesterId, CancellationToken ct)
    {
        var semester = await _semesterRepository.GetByIdAsync(semesterId, ct);
        if (semester is null)
            return Result.Failure("Semester not found.", "SEMESTER_NOT_FOUND");

        if (semester.Status == SemesterStatus.Closed)
            return Result.Failure("Cannot upload paper sets to a closed semester.", "SEMESTER_CLOSED");

        return Result.Success();
    }

    public async Task<Result<PaperSetDetailDTO>> AddFilesAsync(Guid id, IReadOnlyList<MaterialFileUpload> files, CancellationToken ct = default)
    {
        var material = await FindAsync(id, ct);
        if (material is null) return Result<PaperSetDetailDTO>.Failure("Paper set not found.", "PAPER_SET_NOT_FOUND");
        if (files.Count == 0) return Result<PaperSetDetailDTO>.Failure("At least one file is required.", "FILES_REQUIRED");
        var uploadResult = await UploadFilesAsync(material, files, ct);
        if (!uploadResult.IsSuccess) return Result<PaperSetDetailDTO>.Failure(uploadResult.Error!, uploadResult.ErrorCode);
        material.UpdatedDate = DateTime.UtcNow;
        _repository.Update(material);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<PaperSetDetailDTO>.Success(await ToDetailAsync(material));
    }

    public async Task<Result<PaperSetDetailDTO>> UpdateAsync(Guid id, UpdatePaperSetRequest request, CancellationToken ct = default)
    {
        var material = await FindAsync(id, ct);
        if (material is null) return Result<PaperSetDetailDTO>.Failure("Paper set not found.", "PAPER_SET_NOT_FOUND");
        if (request.Status.HasValue && !Enum.IsDefined(request.Status.Value)) return Result<PaperSetDetailDTO>.Failure("Invalid paper set status.", "INVALID_STATUS");
        if (request.TotalQuestions is < 0) return Result<PaperSetDetailDTO>.Failure("Total questions cannot be negative.", "INVALID_TOTAL_QUESTIONS");
        var semesterId = request.SemesterId ?? material.SemesterId;
        var examinationId = request.ExaminationId ?? material.ExaminationId;
        var relationshipResult = await ValidateRelationshipAsync(semesterId, examinationId, ct);
        if (!relationshipResult.IsSuccess) return Result<PaperSetDetailDTO>.Failure(relationshipResult.Error!, relationshipResult.ErrorCode);
        material.SemesterId = semesterId;
        material.ExaminationId = examinationId;
        if (request.Description is not null) material.Description = request.Description.Trim();
        if (request.TotalQuestions.HasValue) material.TotalQuestions = request.TotalQuestions.Value;
        if (request.Status.HasValue) material.Status = request.Status.Value;
        material.UpdatedDate = DateTime.UtcNow;
        _repository.Update(material);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<PaperSetDetailDTO>.Success(await ToDetailAsync(material));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var material = await FindAsync(id, ct);
        if (material is null) return Result.Failure("Paper set not found.", "PAPER_SET_NOT_FOUND");
        foreach (var path in GetPaths(material).Values) await _storage.DeleteAsync(path, ct);
        material.IsDeleted = true; material.Status = PaperSetStatus.Archived; material.UpdatedDate = DateTime.UtcNow;
        _repository.Update(material); await _unitOfWork.SaveChangesAsync(ct); return Result.Success();
    }

    public async Task<Result<StoredFileDownload>> DownloadAsync(Guid id, CancellationToken ct = default)
    {
        var material = await FindAsync(id, ct);
        if (material is null) return Result<StoredFileDownload>.Failure("Paper set not found.", "PAPER_SET_NOT_FOUND");
        var paths = GetPaths(material);
        if (paths.Count == 0) return Result<StoredFileDownload>.Failure("No files are uploaded.", "FILE_NOT_FOUND");

        var downloads = await Task.WhenAll(paths.Select(async pair =>
        {
            var metadata = await _storage.GetMetadataAsync(pair.Value, ct);
            var download = await _storage.DownloadAsync(pair.Value, metadata.FileName, ct);
            return (Type: pair.Key, Download: download);
        }));

        var archive = new MemoryStream();
        var entryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var item in downloads)
            {
                await using var content = item.Download.Content;
                var entryName = Path.GetFileName(item.Download.FileName);
                if (string.IsNullOrWhiteSpace(entryName))
                    entryName = GetTypeCode(item.Type);
                if (!entryNames.Add(entryName))
                    entryName = $"{GetTypeCode(item.Type)}_{entryName}";

                var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await content.CopyToAsync(entryStream, ct);
            }
        }

        archive.Position = 0;
        return Result<StoredFileDownload>.Success(new StoredFileDownload
        {
            Content = archive,
            ContentType = "application/zip",
            FileName = $"{material.PaperSetCode}.zip"
        });
    }

    private async Task<Result> UploadFilesAsync(PaperSet material, IReadOnlyList<MaterialFileUpload> files, CancellationToken ct)
    {
        // Validate every file BEFORE uploading any of them. Doing this inline inside
        // the upload loop (as before) meant a later file failing validation would
        // leave earlier, already-uploaded files orphaned in storage with nothing
        // referencing them, since a `return` here skips the catch/cleanup block below.
        foreach (var file in files)
        {
            var validation = ValidateFile(file);
            if (!validation.IsSuccess) return validation;
        }

        var semester = await _semesterRepository.GetByIdAsync(material.SemesterId, ct);
        if (semester is null)
            return Result.Failure("Semester not found.", "SEMESTER_NOT_FOUND");

        var semesterFolder = NormalizeStorageSegment(semester.Name);

        var uploaded = new List<string>();
        try
        {
            foreach (var file in files)
            {
                var path = $"examinations/{semesterFolder}/{material.PaperSetCode}/{GetTypeCode(file.FileType)}/{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
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

    private async Task<PaperSet?> FindAsync(Guid id, CancellationToken ct) { var material = await _repository.GetByIdAsync(id, ct); return material is null || material.IsDeleted ? null : material; }
    private static Result ValidateQuestions(IReadOnlyList<CreateQuestionInput> questions)
    {
        return questions.Count > 0 && questions.All(question =>
            !string.IsNullOrWhiteSpace(question.Title) &&
            !string.IsNullOrWhiteSpace(question.Content) &&
            question.Point >= 0)
            ? Result.Success()
            : Result.Failure("Each question must have a title, content, and non-negative point.", "INVALID_QUESTION");
    }

    private static Result ValidateFiles(IEnumerable<MaterialFileUpload> files)
    {
        foreach (var file in files)
        {
            var validation = ValidateFile(file);
            if (!validation.IsSuccess) return validation;
        }

        return Result.Success();
    }

    private static Result ValidateFile(MaterialFileUpload file)
    {
        if (!Enum.IsDefined(file.FileType) || string.IsNullOrWhiteSpace(file.FileName) || file.Length <= 0)
            return Result.Failure("Each file must have a valid type, name, and non-empty content.", "INVALID_FILE");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var validExtensions = file.FileType switch
        {
            PaperSetFileType.Question or PaperSetFileType.AnswerTemplate => new[] { ".doc", ".docx" },
            PaperSetFileType.AnswerRubric => new[] { ".xls", ".xlsx" },
            _ => Array.Empty<string>()
        };

        return validExtensions.Contains(extension)
            ? Result.Success()
            : Result.Failure($"The {file.FileType} file must be a Word document or Excel spreadsheet with a valid extension.", "INVALID_FILE_TYPE");
    }

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
    private async Task<string> GenerateCodeAsync(CancellationToken ct) { do { var code = $"PS{Random.Shared.Next(0, 1_000_000):D6}"; if (!await _repository.IsCodeExistsAsync(code, ct)) return code; } while (true); }
    private static string NormalizeStorageSegment(string value) => value.Trim().Replace('/', '-').Replace('\\', '-');

    // Fixed: AnswerRubric and AnswerTemplate previously both fell through to "AT",
    // filing rubric files under an "answer template" folder segment.
    private static string GetTypeCode(PaperSetFileType type) => type switch
    {
        PaperSetFileType.Question => "EQ",
        PaperSetFileType.AnswerRubric => "AR",
        PaperSetFileType.AnswerTemplate => "AT",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown paper set file type.")
    };

    private static void SetPath(PaperSet m, PaperSetFileType type, string path) { if (type == PaperSetFileType.Question) m.FileQuestionDocs = path; else if (type == PaperSetFileType.AnswerRubric) m.FileAnswerRubric = path; else m.FileAnswerTemplate = path; }
    private static Dictionary<PaperSetFileType, string> GetPaths(PaperSet m) => new[] { (PaperSetFileType.Question, m.FileQuestionDocs), (PaperSetFileType.AnswerRubric, m.FileAnswerRubric), (PaperSetFileType.AnswerTemplate, m.FileAnswerTemplate) }.
    Where(x => x.Item2 is not null).ToDictionary(x => x.Item1, x => x.Item2!);

    public async Task<Result<IReadOnlyList<QuestionDTO>>> GetQuestionsByPaperSetIdAsync(Guid paperSetId, CancellationToken ct = default)
    {
        var paperSet = await _repository.GetByIdAsync(paperSetId, ct);
        if (paperSet is null || paperSet.IsDeleted)
            return Result<IReadOnlyList<QuestionDTO>>.Failure("Paper set not found.", "PAPER_SET_NOT_FOUND");

        var questions = await _dbContext.Questions
            .AsNoTracking()
            .Where(q => q.PaperSetId == paperSetId)
            .Include(q => q.Diagram)
            .Include(q => q.Rubric)
            .OrderBy(q => q.CreatedDay)
            .ThenBy(q => q.Title)
            .ToListAsync(ct);

        var dtos = questions.Select(MapToQuestionDTO).ToList();
        return Result<IReadOnlyList<QuestionDTO>>.Success(dtos);
    }

    public async Task<Result<QuestionDTO>> GetQuestionByIdAsync(Guid questionId, CancellationToken ct = default)
    {
        var question = await _dbContext.Questions
            .AsNoTracking()
            .Where(q => q.QuestionId == questionId)
            .Include(q => q.Diagram)
            .Include(q => q.Rubric)
            .FirstOrDefaultAsync(ct);

        if (question is null)
            return Result<QuestionDTO>.Failure("Question not found.", "QUESTION_NOT_FOUND");

        return Result<QuestionDTO>.Success(MapToQuestionDTO(question));
    }

    public async Task<Result<IReadOnlyList<RubricDTO>>> GetRubricsByPaperSetIdAsync(Guid paperSetId, CancellationToken ct = default)
    {
        var paperSet = await _repository.GetByIdAsync(paperSetId, ct);
        if (paperSet is null || paperSet.IsDeleted)
            return Result<IReadOnlyList<RubricDTO>>.Failure("Paper set not found.", "PAPER_SET_NOT_FOUND");

        var rubrics = await _dbContext.QuestionRubrics
            .AsNoTracking()
            .Where(r => r.PaperSetId == paperSetId)
            .Include(r => r.Diagram)
            .Include(r => r.Questions)
            .ToListAsync(ct);

        var dtos = rubrics.Select(MapToRubricDTO).ToList();
        return Result<IReadOnlyList<RubricDTO>>.Success(dtos);
    }

    public async Task<Result<RubricDTO>> GetRubricByIdAsync(Guid rubricId, CancellationToken ct = default)
    {
        var rubric = await _dbContext.QuestionRubrics
            .AsNoTracking()
            .Where(r => r.RubricId == rubricId)
            .Include(r => r.Diagram)
            .Include(r => r.Questions)
            .FirstOrDefaultAsync(ct);

        if (rubric is null)
            return Result<RubricDTO>.Failure("Rubric not found.", "RUBRIC_NOT_FOUND");

        return Result<RubricDTO>.Success(MapToRubricDTO(rubric));
    }

    private static QuestionDTO MapToQuestionDTO(Question q) => new()
    {
        QuestionId = q.QuestionId,
        PaperSetId = q.PaperSetId,
        Title = q.Title,
        Content = q.Content,
        Point = q.Point,
        DiagramId = q.DiagramId,
        DiagramName = q.Diagram?.Name,
        RubricId = q.RubricId,
        Rubric = q.Rubric is null ? null : new QuestionRubricSummaryDTO
        {
            RubricId = q.Rubric.RubricId,
            Point = q.Rubric.Point,
            Content = q.Rubric.Content,
            SubItems = ParseSubItems(q.Rubric.Content)
        },
        CreatedDay = q.CreatedDay,
        UpdatedDay = q.UpdatedDay
    };

    private static RubricDTO MapToRubricDTO(QuestionRubric r) => new()
    {
        RubricId = r.RubricId,
        PaperSetId = r.PaperSetId,
        Point = r.Point,
        Content = r.Content,
        SubItems = ParseSubItems(r.Content),
        DiagramId = r.DiagramId,
        DiagramName = r.Diagram?.Name,
        Questions = r.Questions.Select(q => new RubricQuestionSummaryDTO
        {
            QuestionId = q.QuestionId,
            Title = q.Title,
            Point = q.Point
        }).ToList()
    };

    private static IReadOnlyList<RubricSubItemDto> ParseSubItems(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return [];
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<RubricSubItemDto>>(content, options) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task<PaperSetMetadataDTO> ToMetadataAsync(PaperSet material)
    {
        var files = await Task.WhenAll(GetPaths(material).Select(async pair => { var m = await _storage.GetMetadataAsync(pair.Value); return new PaperSetFileDTO { FileType = pair.Key, FileName = m.FileName, ContentType = m.ContentType, FileSize = m.FileSize }; }));
        return new PaperSetMetadataDTO
        {
            PaperSetId = material.PaperSetId,
            PaperSetCode = material.PaperSetCode,
            Description = material.Description,
            TotalQuestions = material.TotalQuestions,
            Questions = material.Questions.Select(question => new PaperSetQuestionDTO
            {
                QuestionId = question.QuestionId,
                Title = question.Title,
                Content = question.Content,
                Point = question.Point,
                DiagramId = question.DiagramId,
                DiagramName = question.Diagram?.Name,
                RubricId = question.RubricId,
                CreatedDay = question.CreatedDay,
                UpdatedDay = question.UpdatedDay
            }).ToList(),
            Files = files,
            Status = material.Status,
            ExaminationId = material.ExaminationId,
            SemesterId = material.SemesterId,
            CreateById = material.CreateById,
            LecturerName = material.CreateBy?.FullName ?? string.Empty,
            LecturerCode = material.CreateBy?.LecturerCode ?? string.Empty,
            Subject = material.CreateBy?.Subject ?? string.Empty,
            CreatedDate = material.CreatedDate,
            UpdatedDate = material.UpdatedDate
        };
    }

    private async Task<PaperSetDetailDTO> ToDetailAsync(PaperSet material)
    {
        var metadata = await ToMetadataAsync(material);
        return new PaperSetDetailDTO
        {
            PaperSetId = metadata.PaperSetId,
            PaperSetCode = metadata.PaperSetCode,
            Description = metadata.Description,
            TotalQuestions = metadata.TotalQuestions,
            Questions = metadata.Questions,
            Files = metadata.Files,
            Status = metadata.Status,
            ExaminationId = metadata.ExaminationId,
            SemesterId = metadata.SemesterId,
            CreateById = metadata.CreateById,
            LecturerName = metadata.LecturerName,
            LecturerCode = metadata.LecturerCode,
            Subject = metadata.Subject,
            CreatedDate = metadata.CreatedDate,
            UpdatedDate = metadata.UpdatedDate,
            StoragePath = string.Join(',', GetPaths(material).Values),
            Rubrics = material.Rubrics.Select(MapToRubricDTO).ToList()
        };
    }
}