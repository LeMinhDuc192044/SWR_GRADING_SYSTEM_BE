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

    public Student student { get; set; } = new Student();

}
