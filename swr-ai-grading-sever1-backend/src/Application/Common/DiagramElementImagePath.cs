namespace Application.Common;

/// <summary>
/// An element's uploaded image is stored in <c>diagram_elements.file_template_url</c>, the same
/// column as the optional template link. Only paths under this element's own image folder are
/// treated as the uploaded image, so a template URL is never served, deleted or renamed as one.
/// </summary>
public static class DiagramElementImagePath
{
    private const string Folder = "diagram-elements";

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".gif" };

    public static string Build(Guid elementId, string extension)
        => $"{Folder}/{elementId:D}/image{extension}";

    public static bool Matches(Guid elementId, string? storedPath)
    {
        if (elementId == Guid.Empty || string.IsNullOrWhiteSpace(storedPath))
            return false;

        var folder = $"{Folder}/{elementId:D}/";
        if (!storedPath.StartsWith(folder, StringComparison.Ordinal))
            return false;

        var fileName = storedPath[folder.Length..];
        return fileName.IndexOfAny(['/', '\\']) < 0 &&
               string.Equals(Path.GetFileNameWithoutExtension(fileName), "image", StringComparison.Ordinal) &&
               AllowedExtensions.Contains(Path.GetExtension(fileName));
    }
}
