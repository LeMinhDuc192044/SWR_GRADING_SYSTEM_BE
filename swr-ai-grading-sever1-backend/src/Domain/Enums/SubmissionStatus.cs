namespace Domain.Enums;


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

