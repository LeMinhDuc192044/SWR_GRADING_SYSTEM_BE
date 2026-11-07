using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Domain.Enums;


namespace Application.DTOs.Examinations;

public sealed class ExaminationSessionImportResultDTO
{
    public ExaminationSessionDTO Session { get; set; } = null!;
    public int TotalRows { get; set; }
    public int ImportedStudents { get; set; }
    public int ErrorCount => Errors.Count;
    public IReadOnlyList<StudentImportErrorDTO> Errors { get; set; } = [];
}

public sealed class StudentImportErrorDTO
{
    public int Row { get; set; }                       // Excel row number (header row counts)
    public string StudentCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class CreateExaminationSessionFromExcelForm
{
    [Required] public string ExaminationCode { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public ExaminationSessionStatus Status { get; set; }
    public Guid? PaperSetId { get; set; }
    [Required] public IFormFile File { get; set; } = null!;
}