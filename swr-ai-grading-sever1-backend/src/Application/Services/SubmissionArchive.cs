using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SharpCompress.Archives;

namespace Application.Services;

internal static class SubmissionArchive
{
    public const long MaxArchiveBytes = 100 * 1024 * 1024;
    private const long MaxWordDocumentBytes = 50 * 1024 * 1024;
    private const long MaxDiagramBytes = 2 * 1024 * 1024;
    private const long MaxImageBytes = 20 * 1024 * 1024;
    private static readonly HashSet<string> DiagramExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".drawio", ".dio", ".xml", ".puml", ".plantuml"
    };
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".tif", ".tiff"
    };

    public sealed record Content(
        byte[]? WordDocument,
        IReadOnlyDictionary<string, string> DiagramSources,
        IReadOnlyDictionary<string, byte[]> ImageSources);

    public static Content Read(Stream archiveStream)
    {
        using var archive = ArchiveFactory.OpenArchive(archiveStream);
        var entries = archive.Entries.Where(entry => !entry.IsDirectory).ToList();
        byte[]? wordDocument = null;
        var diagramSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var imageSources = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var fileName = Path.GetFileName(entry.Key ?? string.Empty);
            var extension = Path.GetExtension(fileName);
            var baseName = Path.GetFileNameWithoutExtension(fileName);

            if (string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase) && wordDocument is null)
            {
                wordDocument = ReadEntry(entry, MaxWordDocumentBytes);
            }
            else if (DiagramExtensions.Contains(extension))
            {
                var diagramName = GetQuestionKey(baseName);
                var source = Encoding.UTF8.GetString(ReadEntry(entry, MaxDiagramBytes));
                if (string.Equals(extension, ".puml", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".plantuml", StringComparison.OrdinalIgnoreCase))
                    source = ConvertPlantUmlToXml(diagramName, source);

                if (!diagramSources.TryAdd(diagramName, source))
                    throw new InvalidDataException($"Archive chứa nhiều file sơ đồ có cùng tên câu hỏi '{diagramName}'.");
            }
            else if (ImageExtensions.Contains(extension))
            {
                var imageKey = GetQuestionKey(baseName);
                if (!imageSources.TryAdd(imageKey, ReadEntry(entry, MaxImageBytes)))
                    throw new InvalidDataException($"Archive chứa nhiều file hình ảnh có cùng tên câu hỏi '{imageKey}'.");
            }
        }

        if (wordDocument is null && diagramSources.Count == 0 && imageSources.Count == 0)
            throw new InvalidDataException("Archive phải chứa ít nhất một file .docx, sơ đồ hoặc hình ảnh.");

        return new Content(wordDocument, diagramSources, imageSources);
    }

    private static string ConvertPlantUmlToXml(string questionKey, string plantUml)
    {
        var escaped = SecurityElementEscape(plantUml);
        return $"<plantuml question=\"{questionKey}\"><source>{escaped}</source></plantuml>";
    }

    private static string SecurityElementEscape(string text)
    {
        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("\"", "&quot;").Replace("'", "&apos;");
    }

    internal static string GetQuestionKey(string name)
    {
        var match = Regex.Match(name, @"^\s*(?:Q|Question|Câu)\s*0*(\d+)", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var questionNumber)
            ? $"Q{questionNumber.ToString(CultureInfo.InvariantCulture)}"
            : name.Trim();
    }

    private static byte[] ReadEntry(IArchiveEntry entry, long maxBytes)
    {
        if (entry.Size > maxBytes)
            throw new InvalidDataException("Một file bên trong archive vượt quá giới hạn dung lượng cho phép.");

        using var source = entry.OpenEntryStream();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (output.Length + bytesRead > maxBytes)
                throw new InvalidDataException("Một file bên trong archive vượt quá giới hạn dung lượng cho phép.");
            output.Write(buffer, 0, bytesRead);
        }

        return output.ToArray();
    }
}