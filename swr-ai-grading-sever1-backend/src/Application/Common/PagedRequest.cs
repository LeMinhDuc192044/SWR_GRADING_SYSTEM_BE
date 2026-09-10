namespace Application.Common;

/// <summary>
/// Tham số phân trang chuẩn cho mọi list API.
/// </summary>
public class PagedRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 20 : (value > 100 ? 100 : value);
    }
}

/// <summary>
/// Kết quả phân trang chuẩn.
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}
