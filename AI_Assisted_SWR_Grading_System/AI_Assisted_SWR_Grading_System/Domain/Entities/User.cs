using AI_Assisted_SWR_Grading_System.Domain.Enums;

namespace AI_Assisted_SWR_Grading_System.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserCode { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime Birthday { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string Cccd { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public bool IsActive { get; set; }
}