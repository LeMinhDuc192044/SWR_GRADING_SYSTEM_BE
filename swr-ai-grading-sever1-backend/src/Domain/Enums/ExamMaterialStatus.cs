namespace Domain.Enums;

/// <summary>
/// Trạng thái của tài liệu thi (đề thi, đáp án, rubric).
/// Mapping: exam_materials.status (integer) trên Supabase.
/// </summary>
public enum ExamMaterialStatus
{
    Processing = 0,
    Ready = 1,

    InUse = 2,

    Archived = 3,
    Failed = 4
}
