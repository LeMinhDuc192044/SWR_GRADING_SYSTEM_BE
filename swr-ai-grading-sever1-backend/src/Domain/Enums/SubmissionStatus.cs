namespace Domain.Enums;

/// <summary>
/// Trạng thái của một bài nộp.
/// Mapping: student_submission.status (integer) trên Supabase.
/// 0 = Submitted (Mới upload, chưa chấm)
/// 1 = AI_Graded (AI đã chấm xong)
/// 2 = Lecturer_Reviewed (GV đã review)
/// 3 = Final (Đã chốt điểm)
/// </summary>
public enum SubmissionStatus
{
    /// <summary>Mới upload, chưa chấm.</summary>
    Submitted = 0,

    /// <summary>AI đã chấm xong.</summary>
    AI_Graded = 1,

    /// <summary>GV đã review.</summary>
    Lecturer_Reviewed = 2,

    /// <summary>Đã chốt điểm.</summary>
    Final = 3
}

