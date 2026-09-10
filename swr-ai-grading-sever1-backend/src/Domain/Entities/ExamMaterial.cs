using Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("exam_materials")]
public class ExamMaterial
{
    [Key]
    [Column("exam_material_id")]
    public Guid ExamMaterialId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(20)]
    [Column("exam_material_code")]
    public string ExamMaterialCode { get; set; } = string.Empty;

    [Column("file_question_docs")]
    public string? FileQuestionDocs { get; set; }

    [Column("file_answer_rubric")]
    public string? FileAnswerRubric { get; set; }

    [Column("file_answer_template")]
    public string? FileAnswerTemplate { get; set; }

    [Required]
    [Column("file_examination_type")]
    public ExamMaterialFileType FileType { get; set; }

    [Required]
    [Column("created_date")]
    public DateTime CreatedDate { get; set; }

    [Required]
    [Column("updated_date")]
    public DateTime UpdatedDate { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Required]
    [Column("status")]
    public ExamMaterialStatus Status { get; set; }

    [Required]
    [Column("examination_id")]
    [ForeignKey(nameof(Examination))]
    public Guid ExaminationId { get; set; }

    public Examination Examination { get; set; } = null!;

    [Required]
    [Column("create_by_id")]
    [ForeignKey(nameof(CreateBy))]
    public Guid CreateById { get; set; }

    public Lecturer CreateBy { get; set; } = null!;
}