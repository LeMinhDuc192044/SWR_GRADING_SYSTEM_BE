namespace Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

public class Application
{
    [Key]
    [Column("application_id")]
    public Guid ApplicationId { get; set; } = Guid.NewGuid();

    [Column("reason")]
    public string Reason { get; set; } = string.Empty;


    [Column("status")]
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;

    [Column("created_day")]
    public DateTime CreatedDay { get; set; } = DateTime.UtcNow;

    [Column("updated_day")]
    public DateTime UpdatedDay { get; set; } = DateTime.UtcNow;

    [Column("student_id")]
    [ForeignKey(nameof(Student))]
    public Guid StudentId { get; set; }

    public Student Student { get; set; } = new Student();
}
