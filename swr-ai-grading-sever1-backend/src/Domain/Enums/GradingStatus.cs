namespace Domain.Enums;

/// <summary>
/// Trạng thái của một lượt chấm điểm.
/// Mapping: gradings.status (integer) trên Supabase.
/// </summary>
public enum GradingStatus
{
    /// <summary>Mới tạo, chưa có điểm.</summary>
    Pending = 0,

    /// <summary>AI đang chấm.</summary>
    AI_Processing = 1,

    /// <summary>AI đã chấm xong, có ai_score.</summary>
    AI_Completed = 2,

    /// <summary>Giảng viên đã review, có lecturer_score.</summary>
    Lecturer_Reviewed = 3,

    /// <summary>Đã chốt final_score.</summary>
    Finalized = 4,

    /// <summary>Bị hủy.</summary>
    Cancelled = 5
}
