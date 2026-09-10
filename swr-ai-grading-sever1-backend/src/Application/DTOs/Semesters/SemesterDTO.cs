using Domain.Enums;

namespace Application.DTOs.Semesters;
public class SemesterDTO
{
    public Guid SemesterId { get; set; }
    public string SemesterCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public SemesterStatus Status { get; set; }
}

public class CreateSemesterRequest
{
    public string SemesterCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public SemesterStatus Status { get; set; }
}

public class UpdateSemesterRequest
{
    public string? SemesterCode { get; set; }
    public string? Name { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public SemesterStatus? Status { get; set; }
}

public class UpdateSemesterStatusRequest
{
    public SemesterStatus Status { get; set; }
}
