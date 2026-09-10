namespace Domain.Enums;

/// <summary>
/// Trạng thái của tài liệu thi (đề thi, đáp án, rubric).
/// Mapping: exam_materials.status (integer) trên Supabase.
/// </summary>
public enum ExamMaterialStatus
{
    /// <summary>Mới upload, đang xử lý.</summary>
    Processing = 0,

    /// <summary>Đã sẵn sàng sử dụng.</summary>
    Ready = 1,

    /// <summary>Đang được sử dụng cho một đợt thi.</summary>
    InUse = 2,

    /// <summary>Đã lưu trữ / không dùng nữa.</summary>
    Archived = 3,

    /// <summary>Bị lỗi khi xử lý.</summary>
    Failed = 4
}
