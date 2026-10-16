using System.Text;
using SharpCompress.Archives;

namespace Application.Services;

internal static class SubmissionArchive
{
    public const long MaxArchiveBytes = 100 * 1024 * 1024;
    private const long MaxWordDocumentBytes = 50 * 1024 * 1024;
    private const long MaxDiagramBytes = 2 * 1024 * 1024;
    private static readonly HashSet<string> DiagramExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".drawio", ".xml", ".puml", ".plantuml"
    };

    public sealed record Content(byte[] WordDocument, string? DiagramSource);

    public static Content Read(Stream archiveStream)
    {
        using var archive = ArchiveFactory.OpenArchive(archiveStream);
        var entries = archive.Entries.Where(entry => !entry.IsDirectory).ToList();
        if (entries.Count != 2)
            throw new InvalidDataException("Archive RAR phải chứa đúng 2 file: một file .docx và một file sơ đồ.");

        byte[]? wordDocument = null;
        string? diagramSource = null;

        foreach (var entry in entries)
        {
            var extension = Path.GetExtension(entry.Key ?? string.Empty);
            if (string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase) && wordDocument is null)
            {
                wordDocument = ReadEntry(entry, MaxWordDocumentBytes);
            }
            else if (DiagramExtensions.Contains(extension) && diagramSource is null)
            {
                diagramSource = Encoding.UTF8.GetString(ReadEntry(entry, MaxDiagramBytes));
            }
            else
            {
                throw new InvalidDataException("Archive RAR phải chứa đúng một file .docx và một file sơ đồ (.drawio, .xml, .puml hoặc .plantuml).");
            }
        }

        if (wordDocument is null || diagramSource is null)
            throw new InvalidDataException("Archive RAR phải chứa đúng một file .docx và một file sơ đồ.");

        return new Content(wordDocument, diagramSource);
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