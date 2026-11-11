    using Application.Common;
    using Application.Common.Interfaces;
    using Application.DTOs.Examinations;
    using Application.Interfaces;
    using Domain.Entities;
    using Domain.Enums;
    using Microsoft.EntityFrameworkCore;

    namespace Application.Services;

    public sealed class ExaminationSessionService : IExaminationSessionService
    {
        private readonly IApplicationDbContext _db;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStudentCodeExcelReader _excelReader;

        public ExaminationSessionService(IApplicationDbContext db, IUnitOfWork unitOfWork, IStudentCodeExcelReader excelReader)
        {
            _db = db;
            _unitOfWork = unitOfWork;
            _excelReader = excelReader;
        }

        public async Task<Result<IReadOnlyList<ExaminationSessionDTO>>> GetByExaminationAsync(Guid examinationId, CancellationToken ct = default)
        {
            var sessions = await _db.ExaminationSessions
                .AsNoTracking()
                .Where(s => s.ExaminationId == examinationId)
                .Include(s => s.StudentExaminationSessions)
                .OrderBy(s => s.StartDate)
                .ThenBy(s => s.StartTime)
                .ToListAsync(ct);

            return Result<IReadOnlyList<ExaminationSessionDTO>>.Success(sessions.Select(session => Map(session)).ToList());
        }

        public async Task<Result<ExaminationSessionDTO>> GetByIdAsync(Guid examinationId, Guid sessionId, CancellationToken ct = default)
        {
            var session = await FindSessionAsync(examinationId, sessionId, ct);
            return session is null
                ? Result<ExaminationSessionDTO>.Failure("Examination session not found.", "EXAMINATION_SESSION_NOT_FOUND")
                : Result<ExaminationSessionDTO>.Success(Map(session));
        }

        public async Task<Result<ExaminationSessionDTO>> CreateAsync(Guid examinationId, CreateExaminationSessionRequest request, CancellationToken ct = default)
        {
            var examination = await _db.Examinations.FirstOrDefaultAsync(e => e.ExaminationId == examinationId, ct);
            if (examination is null)
                return Result<ExaminationSessionDTO>.Failure("Examination not found.", "EXAMINATION_NOT_FOUND");

            if (string.IsNullOrWhiteSpace(request.ExaminationCode))
                return Result<ExaminationSessionDTO>.Failure("Examination code is required.", "INVALID_EXAMINATION_SESSION");
            if (!Enum.IsDefined(request.Status))
                return Result<ExaminationSessionDTO>.Failure("Invalid examination session status.", "INVALID_EXAMINATION_SESSION_STATUS");
            if (request.StartDate < examination.StartDate || request.StartDate > examination.EndDate)
                return Result<ExaminationSessionDTO>.Failure("Session date must be within the examination dates.", "SESSION_DATE_OUT_OF_EXAMINATION");

            var paperSetId = NormalizeGuid(request.PaperSetId);
            if (paperSetId.HasValue)
            {
                var paperSet = await _db.PaperSets.FirstOrDefaultAsync(p => p.PaperSetId == paperSetId.Value && !p.IsDeleted, ct);
                if (paperSet is null)
                    return Result<ExaminationSessionDTO>.Failure("Paper set not found.", "PAPER_SET_NOT_FOUND");
                if (paperSet.ExaminationId != examinationId)
                    return Result<ExaminationSessionDTO>.Failure("Paper set does not belong to the examination.", "PAPER_SET_EXAMINATION_MISMATCH");
            }

            

            var studentIds = request.StudentIds.Where(id => id != Guid.Empty).Distinct().ToList();
            foreach (var studentId in studentIds)
            {
                if (!await _db.Students.AnyAsync(s => s.Id == studentId && !s.IsDeleted, ct))
                    return Result<ExaminationSessionDTO>.Failure("Student not found.", "STUDENT_NOT_FOUND");
            }

            var session = new ExaminationSession
            {
                ExaminationId = examinationId,
                ExaminationCode = request.ExaminationCode.Trim(),
                StartDate = request.StartDate,
                StartTime = request.StartTime,
                Status = request.Status,
                SemesterId = examination.SemesterId,
                PaperSetId = paperSetId,
                CreatedDay = VietnamTime.Now,
                UpdatedDay = VietnamTime.Now
            };

            foreach (var studentId in studentIds)
            {
                session.StudentExaminationSessions.Add(new StudentExaminationSession
                {
                    StudentId = studentId,
                    ExaminationSessionId = session.ExaminationSessionId,
                    ExaminationSession = session
                });
            }

            _db.ExaminationSessions.Add(session);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<ExaminationSessionDTO>.Success(Map(session));
        }

        public async Task<Result<ExaminationSessionImportResultDTO>> CreateWithStudentExcelAsync(
            Guid examinationId, CreateExaminationSessionRequest request, Stream excelStream, CancellationToken ct = default)
        {
            IReadOnlyList<StudentCodeRow> rows;
            try
            {
                rows = _excelReader.Read(excelStream);
            }
            catch (InvalidDataException ex)
            {
                return Result<ExaminationSessionImportResultDTO>.Failure(ex.Message, "INVALID_EXCEL_FILE");
            }

            if (rows.Count == 0)
                return Result<ExaminationSessionImportResultDTO>.Failure("The Excel file contains no student codes.", "EXCEL_EMPTY");

            var errors = new List<StudentImportErrorDTO>();

            // 1. Drop duplicates inside the file (keep the first one, report the rest).
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var uniqueRows = new List<StudentCodeRow>();
            foreach (var row in rows)
            {
                if (seen.Add(row.Code))
                    uniqueRows.Add(row);
                else
                    errors.Add(new StudentImportErrorDTO
                    {
                        Row = row.RowNumber,
                        StudentCode = row.Code,
                        Message = $"Duplicate student code '{row.Code}' in the file; ignored."
                    });
            }

            // 2. Look all codes up in one query (case-insensitive).
            var codesUpper = uniqueRows.Select(r => r.Code.ToUpperInvariant()).ToList();
            var found = await _db.Students.AsNoTracking()
                .Where(s => !s.IsDeleted && codesUpper.Contains(s.StudentCode.ToUpper()))
                .Select(s => new { s.Id, s.StudentCode })
                .ToListAsync(ct);
            var idByCode = found
                .GroupBy(s => s.StudentCode.ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.First().Id);

            // 3. Keep the valid ones, record the rest, but never stop.
            var studentIds = new List<Guid>();
            foreach (var row in uniqueRows)
            {
                if (idByCode.TryGetValue(row.Code.ToUpperInvariant(), out var studentId))
                    studentIds.Add(studentId);
                else
                    errors.Add(new StudentImportErrorDTO
                    {
                        Row = row.RowNumber,
                        StudentCode = row.Code,
                        Message = $"Student code '{row.Code}' does not exist."
                    });
            }

            // 4. Create the session with the valid students (existing validation applies).
            request.StudentIds = studentIds;
            var created = await CreateAsync(examinationId, request, ct);
            if (!created.IsSuccess)
                return Result<ExaminationSessionImportResultDTO>.Failure(created.Error!, created.ErrorCode);

            return Result<ExaminationSessionImportResultDTO>.Success(new ExaminationSessionImportResultDTO
            {
                Session = created.Data!,
                TotalRows = rows.Count,
                ImportedStudents = studentIds.Count,
                Errors = errors.OrderBy(e => e.Row).ToList()
            });
        }

        public async Task<Result<ExaminationSessionDTO>> UpdateAsync(Guid examinationId, Guid sessionId, UpdateExaminationSessionRequest request, CancellationToken ct = default)
        {
            var session = await FindSessionAsync(examinationId, sessionId, ct);
            if (session is null)
                return Result<ExaminationSessionDTO>.Failure("Examination session not found.", "EXAMINATION_SESSION_NOT_FOUND");

            var examination = await _db.Examinations.FirstOrDefaultAsync(e => e.ExaminationId == examinationId, ct);
            if (examination is null)
                return Result<ExaminationSessionDTO>.Failure("Examination not found.", "EXAMINATION_NOT_FOUND");

            var code = request.ExaminationCode?.Trim();
            if (code is not null && string.IsNullOrWhiteSpace(code))
                return Result<ExaminationSessionDTO>.Failure("Examination code is required.", "INVALID_EXAMINATION_SESSION");

            var startDate = request.StartDate ?? session.StartDate;
            if (startDate < examination.StartDate || startDate > examination.EndDate)
                return Result<ExaminationSessionDTO>.Failure("Session date must be within the examination dates.", "SESSION_DATE_OUT_OF_EXAMINATION");

            if (!string.IsNullOrWhiteSpace(code)) session.ExaminationCode = code;
            session.StartDate = startDate;
            if (request.StartTime.HasValue) session.StartTime = request.StartTime.Value;
            if (request.Status.HasValue)
            {
                if (!Enum.IsDefined(request.Status.Value))
                    return Result<ExaminationSessionDTO>.Failure("Invalid examination session status.", "INVALID_EXAMINATION_SESSION_STATUS");
                session.Status = request.Status.Value;
            }

            if (request.PaperSetId.HasValue)
            {
                var paperSetId = NormalizeGuid(request.PaperSetId);
                if (paperSetId.HasValue)
                {
                    var paperSet = await _db.PaperSets.FirstOrDefaultAsync(p => p.PaperSetId == paperSetId.Value && !p.IsDeleted, ct);
                    if (paperSet is null)
                        return Result<ExaminationSessionDTO>.Failure("Paper set not found.", "PAPER_SET_NOT_FOUND");
                    if (paperSet.ExaminationId != examinationId)
                        return Result<ExaminationSessionDTO>.Failure("Paper set does not belong to the examination.", "PAPER_SET_EXAMINATION_MISMATCH");
                }
                session.PaperSetId = paperSetId;
            }

            if (request.StudentIds is not null)
            {
                var studentIds = request.StudentIds.Where(id => id != Guid.Empty).Distinct().ToList();
                foreach (var studentId in studentIds)
                {
                    if (!await _db.Students.AnyAsync(s => s.Id == studentId && !s.IsDeleted, ct))
                        return Result<ExaminationSessionDTO>.Failure("Student not found.", "STUDENT_NOT_FOUND");
                }

                var existing = session.StudentExaminationSessions.ToList();
                foreach (var assignment in existing.Where(a => !studentIds.Contains(a.StudentId)))
                    _db.StudentExaminationSessions.Remove(assignment);
                foreach (var studentId in studentIds.Where(id => existing.All(a => a.StudentId != id)))
                    _db.StudentExaminationSessions.Add(new StudentExaminationSession { StudentId = studentId, ExaminationSessionId = sessionId });
            }

            session.UpdatedDay = VietnamTime.Now;
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<ExaminationSessionDTO>.Success(await MapLoadedAsync(session, ct));
        }

        public async Task<Result> DeleteAsync(Guid examinationId, Guid sessionId, CancellationToken ct = default)
        {
            var session = await FindSessionAsync(examinationId, sessionId, ct);
            if (session is null)
                return Result.Failure("Examination session not found.", "EXAMINATION_SESSION_NOT_FOUND");

            _db.ExaminationSessions.Remove(session);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }

        private async Task<ExaminationSession?> FindSessionAsync(Guid examinationId, Guid sessionId, CancellationToken ct)
            => await _db.ExaminationSessions
                .Include(s => s.StudentExaminationSessions)
                .FirstOrDefaultAsync(s => s.ExaminationId == examinationId && s.ExaminationSessionId == sessionId, ct);

        private async Task<ExaminationSessionDTO> MapLoadedAsync(ExaminationSession session, CancellationToken ct)
        {
            var studentIds = await _db.StudentExaminationSessions
                .AsNoTracking()
                .Where(a => a.ExaminationSessionId == session.ExaminationSessionId)
                .Select(a => a.StudentId)
                .ToListAsync(ct);
            return Map(session, studentIds);
        }

        public async Task<PagedResult<ExaminationSessionListItemDTO>> GetPagedAsync(PagedRequest request, CancellationToken ct = default)
        {
            var page = Math.Max(request.Page, 1);
            var pageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, 100);

            var query = _db.ExaminationSessions.AsNoTracking();
            var totalCount = await query.CountAsync(ct);

            var sessions = await query
                .OrderByDescending(s => s.CreatedDay)
                .ThenByDescending(s => s.StartDate)
                .ThenByDescending(s => s.StartTime)
                .ThenBy(s => s.ExaminationSessionId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var sessionIds = sessions.Select(s => s.ExaminationSessionId).ToList();
            var semesterIds = sessions.Select(s => s.SemesterId).Distinct().ToList();
            var examinationIds = sessions.Select(s => s.ExaminationId).Distinct().ToList();

            // One query each for the attendees, submission counts, semesters and examinations on this page (no N+1).
            var attendees = await _db.StudentExaminationSessions.AsNoTracking()
                .Where(a => sessionIds.Contains(a.ExaminationSessionId))
                .OrderBy(a => a.Student.StudentCode)
                .Select(a => new
                {
                    a.ExaminationSessionId,
                    StudentId = a.Student.Id,
                    a.Student.StudentCode,
                    a.Student.FullName,
                    a.Student.Email
                })
                .ToListAsync(ct);

            var studentsBySession = attendees
                .GroupBy(a => a.ExaminationSessionId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<ExaminationSessionStudentDTO>)g
                        .Select(a => new ExaminationSessionStudentDTO
                        {
                            StudentId = a.StudentId,
                            StudentCode = a.StudentCode,
                            FullName = a.FullName,
                            Email = a.Email
                        })
                        .ToList());

            var submissionCounts = await _db.StudentSubmissions.AsNoTracking()
                .Where(s => s.ExaminationSessionId.HasValue && sessionIds.Contains(s.ExaminationSessionId.Value))
                .GroupBy(s => s.ExaminationSessionId!.Value)
                .Select(g => new { ExaminationSessionId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ExaminationSessionId, x => x.Count, ct);

            var semesters = await _db.Semesters.AsNoTracking()
                .Where(s => semesterIds.Contains(s.SemesterId))
                .Select(s => new ExaminationSessionSemesterDTO
                {
                    SemesterId = s.SemesterId,
                    SemesterCode = s.SemesterCode,
                    Name = s.Name,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate
                })
                .ToDictionaryAsync(s => s.SemesterId, ct);

            var examinations = await _db.Examinations.AsNoTracking()
                .Where(e => examinationIds.Contains(e.ExaminationId))
                .Select(e => new ExaminationSessionExaminationDTO
                {
                    ExaminationId = e.ExaminationId,
                    Name = e.Name,
                    StartDate = e.StartDate,
                    EndDate = e.EndDate
                })
                .ToDictionaryAsync(e => e.ExaminationId, ct);

            var items = sessions.Select(session => new ExaminationSessionListItemDTO
            {
                ExaminationSessionId = session.ExaminationSessionId,
                ExaminationId = session.ExaminationId,
                ExaminationCode = session.ExaminationCode,
                StartDate = session.StartDate,
                StartTime = session.StartTime,
                Status = session.Status,
                SemesterId = session.SemesterId,
                PaperSetId = session.PaperSetId,
                CreatedDay = session.CreatedDay,
                UpdatedDay = session.UpdatedDay,
                TotalSubmission = submissionCounts.GetValueOrDefault(session.ExaminationSessionId),
                Students = studentsBySession.GetValueOrDefault(session.ExaminationSessionId) ?? [],
                Semester = semesters.GetValueOrDefault(session.SemesterId),
                Examination = examinations.GetValueOrDefault(session.ExaminationId)
            }).ToList();

            return new PagedResult<ExaminationSessionListItemDTO>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<Result<ExaminationSessionResultsDTO>> GetResultsAsync(Guid examinationSessionId, CancellationToken ct = default)
        {
            var session = await _db.ExaminationSessions
                .AsNoTracking()
                .Where(s => s.ExaminationSessionId == examinationSessionId)
                .Select(s => new ExaminationSessionResultsDTO
                {
                    ExaminationSessionId = s.ExaminationSessionId,
                    ExaminationCode = s.ExaminationCode,
                    StartDate = s.StartDate,
                    StartTime = s.StartTime,
                    Status = s.Status,
                    Semester = new ExaminationSessionSemesterDTO
                    {
                        SemesterId = s.Examination.SemesterId,
                        SemesterCode = s.Examination.Semester.SemesterCode,
                        Name = s.Examination.Semester.Name,
                        StartDate = s.Examination.Semester.StartDate,
                        EndDate = s.Examination.Semester.EndDate
                    },
                    Examination = new ExaminationSessionExaminationDTO
                    {
                        ExaminationId = s.ExaminationId,
                        Name = s.Examination.Name,
                        StartDate = s.Examination.StartDate,
                        EndDate = s.Examination.EndDate
                    },
                    PaperSet = s.PaperSetId == null
                        ? null
                        : new ExaminationSessionPaperSetDTO
                        {
                            PaperSetId = s.PaperSet!.PaperSetId,
                            PaperSetCode = s.PaperSet.PaperSetCode
                        }
                })
                .FirstOrDefaultAsync(ct);

            if (session is null)
                return Result<ExaminationSessionResultsDTO>.Failure("Examination session not found.", "EXAMINATION_SESSION_NOT_FOUND");

            var submissions = await _db.StudentSubmissions
                .AsNoTracking()
                .Where(s => s.ExaminationSessionId == examinationSessionId)
                .Include(s => s.Student)
                .Include(s => s.SubmissionGradingHistories)
                    .ThenInclude(h => h.Details)
                        .ThenInclude(d => d.ParsedAnswer)
                            .ThenInclude(a => a.Question)
                .OrderBy(s => s.Student.StudentCode)
                .ToListAsync(ct);

            session.Results = submissions.Select(MapResult).ToList();
            return Result<ExaminationSessionResultsDTO>.Success(session);
        }

        private static ExaminationSessionStudentResultDTO MapResult(StudentSubmission submission)
        {
            var history = FindLecturerHistory(submission.SubmissionGradingHistories);

            return new ExaminationSessionStudentResultDTO
            {
                SubmissionId = submission.SubmissionId,
                Student = new ExaminationSessionResultStudentDTO
                {
                    StudentId = submission.StudentId,
                    StudentCode = submission.Student.StudentCode,
                    FullName = submission.Student.FullName
                },
                Grading = history is null
                    ? null
                    : new ExaminationSessionResultGradingDTO
                    {
                        HistoryId = history.HistoryId,
                        EvaluationType = history.EvaluationType,
                        Status = history.Status,
                        GradingRound = history.GradingRound,
                        LecturerScore = history.TotalScore ?? history.Details.Sum(d => d.Score),
                        GeneralFeedback = history.GeneralFeedback,
                        GradedAt = history.CreatedAt,
                        Questions = history.Details
                            .OrderBy(d => d.ParsedAnswer.Question.Title)
                            .Select(d => new ExaminationSessionQuestionScoreDTO
                            {
                                DetailId = d.DetailId,
                                EvaluationId = d.EvaluationId,
                                ParsedAnswerId = d.ParsedAnswerId,
                                QuestionId = d.ParsedAnswer.QuestionId,
                                QuestionTitle = d.ParsedAnswer.Question.Title,
                                MaxScore = d.ParsedAnswer.Question.Point,
                                Score = d.Score,
                                Comment = d.Comment
                            })
                            .ToList()
                    }
            };
        }

        // The lecturer's official grade wins; otherwise the latest lecturer entry (still in review) is used.
        private static SubmissionGradingHistory? FindLecturerHistory(IEnumerable<SubmissionGradingHistory> histories)
        {
            var lecturerHistories = histories
                .Where(h => h.EvaluationType == EvaluationType.Lecturer)
                .ToList();

            return lecturerHistories
                .Where(h => h.Status == SubmissionGradingHistoryStatus.OFFICIAL)
                .OrderByDescending(h => h.GradingRound)
                .ThenByDescending(h => h.CreatedAt)
                .FirstOrDefault()
                ?? lecturerHistories
                    .OrderByDescending(h => h.GradingRound)
                    .ThenByDescending(h => h.CreatedAt)
                    .FirstOrDefault();
        }

        private static ExaminationSessionDTO Map(ExaminationSession session, IReadOnlyList<Guid>? studentIds = null) => new()
        {
            ExaminationSessionId = session.ExaminationSessionId,
            ExaminationId = session.ExaminationId,
            ExaminationCode = session.ExaminationCode,
            StartDate = session.StartDate,
            StartTime = session.StartTime,
            Status = session.Status,
            SemesterId = session.SemesterId,
            CreatedDay = session.CreatedDay,
            UpdatedDay = session.UpdatedDay,
            PaperSetId = session.PaperSetId,
            StudentIds = studentIds?.ToList() ?? session.StudentExaminationSessions.Select(a => a.StudentId).ToList()
        };

        private static Guid? NormalizeGuid(Guid? id) => id.HasValue && id.Value != Guid.Empty ? id.Value : null;
    }
