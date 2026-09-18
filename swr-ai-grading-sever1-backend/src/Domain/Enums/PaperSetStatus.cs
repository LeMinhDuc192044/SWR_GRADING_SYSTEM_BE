namespace Domain.Enums;

/// <summary>
/// Trạng thái của tài liệu thi (đề thi, đáp án, rubric).
/// Mapping: paper_sets.status (integer) trên Supabase.
/// </summary>
public enum PaperSetStatus
{
    Processing = 0,
    Ready = 1,

    Used = 2,

    Archived = 3,
    Failed = 4
}
