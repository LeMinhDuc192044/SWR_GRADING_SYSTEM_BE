namespace Application.DTOs.Students;

public sealed class StudentImportResultDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<StudentImportErrorDto> Errors { get; set; } = new();
    public List<StudentImportSuccessDto> ImportedStudents { get; set; } = new();
}

public sealed class StudentImportSuccessDto
{
    public int RowIndex { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Major { get; set; } = string.Empty;
    public bool EmailSent { get; set; }
}

public sealed class StudentImportErrorDto
{
    public int RowIndex { get; set; }
    public string? StudentCode { get; set; }
    public string? Email { get; set; }
    public string Reason { get; set; } = string.Empty;
}

