namespace Domain.Enums;


public enum SubmissionStatus
{
    /// <summary>Mới upload, chưa chấm.</summary>
    Submitted = 0,
    Grading = 1,

    /// <summary>AI đã chấm xong.</summary>
    AI_Graded = 2,

    /// <summary>GV đã review.</summary>
    Lecturer_Reviewed = 3,

    /// <summary>Đã chốt điểm.</summary>
    Final = 4
}

