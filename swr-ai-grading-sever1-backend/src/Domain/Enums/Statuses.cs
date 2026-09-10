namespace Domain.Enums;

/// <summary>
/// Trạng thái chung của các entity trong hệ thống.
/// Mapping: status (text) — bảng examination, submission, grading trên Supabase.
/// </summary>
public enum Statuses
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
    Failed = 4
}
