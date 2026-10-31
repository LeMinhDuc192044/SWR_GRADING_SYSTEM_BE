namespace Domain.Enums;

/// <summary>
/// Trạng thái của đơn phúc khảo bài thi.
/// </summary>
public enum RegradeApplicationStatus
{
    /// <summary>Đơn mới tạo, đang chờ Admin xét duyệt.</summary>
    Pending = 0,

    /// <summary>Admin đã chấp nhận và phân công giảng viên chấm lại.</summary>
    Approved = 1,

    /// <summary>Admin đã từ chối đơn phúc khảo.</summary>
    Rejected = 2,

    /// <summary>Giảng viên được phân công đã chấm xong phúc khảo.</summary>
    Completed = 3
}

