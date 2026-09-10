namespace Application.Common;

/// <summary>
/// Exception ném ra khi không tìm thấy entity theo id.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object id)
        : base($"{entityName} với id '{id}' không tồn tại.") { }

    public NotFoundException(string message) : base(message) { }
}

/// <summary>
/// Exception ném ra khi validate dữ liệu thất bại (trùng email, vi phạm rule...).
/// </summary>
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}

/// <summary>
/// Exception ném ra khi user không có quyền truy cập.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
