namespace Domain.Enums;

/// <summary>
/// Trạng thái của một đợt thi.
/// Mapping: examinations.status (integer) trên Supabase.
/// </summary>
public enum ExaminationStatus
{
    /// <summary>Mới tạo, chưa bắt đầu.</summary>
    Draft = 0,

    /// <summary>Đang mở cho sinh viên nộp bài.</summary>
    Ongoing = 1,

    /// <summary>Đã đóng, không nhận bài nộp.</summary>
    Closed = 2,

    /// <summary>Đã hoàn tất chấm điểm.</summary>
    Completed = 3,

    /// <summary>Bị hủy.</summary>
    Cancelled = 4
}
