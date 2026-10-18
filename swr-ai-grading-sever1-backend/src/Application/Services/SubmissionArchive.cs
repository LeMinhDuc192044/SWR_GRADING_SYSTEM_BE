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
    private static readonly HashSet<string> DiagramExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".drawio", ".io", ".xml", ".puml", ".plantuml"
    };

    public sealed record Content(byte[] WordDocument, IReadOnlyDictionary<string, string> DiagramSources);

    public static Content Read(Stream archiveStream)
    {
        using var archive = ArchiveFactory.OpenArchive(archiveStream);
        var entries = archive.Entries.Where(entry => !entry.IsDirectory).ToList();
        byte[]? wordDocument = null;
        var diagramSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var extension = Path.GetExtension(entry.Key ?? string.Empty);
            if (string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase) && wordDocument is null)
            {
                wordDocument = ReadEntry(entry, MaxWordDocumentBytes);
            }
            else if (DiagramExtensions.Contains(extension))
            {
                var diagramName = GetQuestionKey(Path.GetFileNameWithoutExtension(entry.Key ?? string.Empty));
                if (!diagramSources.TryAdd(diagramName, Encoding.UTF8.GetString(ReadEntry(entry, MaxDiagramBytes))))
                    throw new InvalidDataException($"Archive RAR chứa nhiều file sơ đồ có cùng tên '{diagramName}'.");
            }
            else
            {
                throw new InvalidDataException("Archive RAR phải chứa đúng một file .docx và một file sơ đồ (.drawio, .xml, .puml hoặc .plantuml).");
            }
        }

        if (wordDocument is null)
            throw new InvalidDataException("Archive RAR phải chứa một file .docx.");

        return new Content(wordDocument, diagramSources);
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