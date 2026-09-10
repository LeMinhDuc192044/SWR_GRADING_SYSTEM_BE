namespace Domain.Enums;

/// <summary>
/// Trạng thái của một bài nộp.
/// Mapping: submissions.status (integer) trên Supabase.
/// </summary>
public enum SubmissionStatus
{
    /// <summary>Mới tạo, chưa submit.</summary>
    Draft = 0,

    /// <summary>Đã submit, chờ xử lý.</summary>
    Submitted = 1,

    /// <summary>AI đang chấm.</summary>
    AI_Grading = 2,

    /// <summary>AI chấm xong, chờ giảng viên review.</summary>
    AI_Graded = 3,

    /// <summary>Giảng viên đã review xong.</summary>
    Lecturer_Reviewed = 4,

    /// <summary>Đã chốt điểm cuối.</summary>
    Final = 5,

    /// <summary>Bị hủy.</summary>
    Cancelled = 6
}
