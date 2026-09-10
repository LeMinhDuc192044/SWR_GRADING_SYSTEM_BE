namespace Domain.Enums;

/// <summary>
/// Trạng thái của một học kỳ trong hệ thống.
/// Mapping: semester.status (text) trên Supabase.
/// </summary>
public enum SemesterStatus
{
    /// <summary>Học kỳ sắp diễn ra.</summary>
    Upcoming = 0,

    /// <summary>Học kỳ đang diễn ra.</summary>
    Active = 1,

    /// <summary>Học kỳ đã kết thúc.</summary>
    Closed = 2
}
