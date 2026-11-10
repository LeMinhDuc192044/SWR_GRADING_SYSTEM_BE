using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using SharpCompress.Archives;

namespace Application.Services;

internal static class SubmissionArchive
{
    public const long MaxArchiveBytes = 500L * 1024 * 1024;
    private const long MaxWordDocumentBytes = 50L * 1024 * 1024;
    private const long MaxDiagramBytes = 2L * 1024 * 1024;
    private const long MaxImageBytes = 20L * 1024 * 1024;
    private const long MaxNestedArchiveBytes = 100L * 1024 * 1024;
    private const long MaxTotalUncompressedBytes = 1024L * 1024 * 1024;
    private const int MaxArchiveDepth = 2;

    private static readonly HashSet<string> DiagramExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".drawio", ".dio", ".xml", ".puml", ".plantuml"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".tif", ".tiff"
    };

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar"
    };

    public sealed record Content(
        byte[]? WordDocument,
        IReadOnlyDictionary<string, string> DiagramSources,
        IReadOnlyDictionary<string, byte[]> ImageSources);

    public sealed record ExtractedPackage(
        bool IsSuccess,
        string DisplayFileName,
        string? ErrorMessage,
        byte[] OriginalBytes,
        string StorageExtension,
        string StorageContentType,
        Content? Content,
        IReadOnlyList<string> StudentHints);

    private sealed record RawEntry(
        string RelativePath,
        string FileName,
        string Extension,
        byte[] Data);

    public static Content Read(Stream archiveStream)
    {
        var entries = ExtractRawEntries(archiveStream);
        return BuildSingleContent(entries);
    }

    public static IReadOnlyList<ExtractedPackage> ReadPackages(Stream archiveStream, string archiveFileName)
    {
        return ReadPackagesRecursive(archiveStream, archiveFileName, depth: 0, inheritedHints: []);
    }

    public static string TryExtractDocxPlainText(byte[]? docxBytes)
    {
        if (docxBytes is null || docxBytes.Length == 0)
            return string.Empty;

        try
        {
            using var ms = new MemoryStream(docxBytes, writable: false);
            using var doc = WordprocessingDocument.Open(ms, false);
            var sb = new StringBuilder();

            if (doc.MainDocumentPart?.HeaderParts is not null)
            {
                foreach (var header in doc.MainDocumentPart.HeaderParts)
                    sb.AppendLine(header.Header?.InnerText);
            }

            if (doc.MainDocumentPart?.Document?.Body is { } body)
                sb.Append(body.InnerText);

            return sb.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static IReadOnlyList<ExtractedPackage> ReadPackagesRecursive(
        Stream archiveStream,
        string archiveFileName,
        int depth,
        IReadOnlyList<string> inheritedHints)
    {
        if (depth > MaxArchiveDepth)
        {
            return
            [
                FailurePackage(archiveFileName, $"Archive '{archiveFileName}' vượt quá độ sâu giải nén cho phép.")
            ];
        }

        List<RawEntry> entries;
        try
        {
            entries = ExtractRawEntries(archiveStream);
        }
        catch (Exception ex)
        {
            return [FailurePackage(archiveFileName, ex.Message)];
        }

        if (entries.Count == 0)
        {
            return [FailurePackage(archiveFileName, "Archive không chứa dữ liệu bài làm hợp lệ.")];
        }

        var archiveStem = Path.GetFileNameWithoutExtension(archiveFileName);
        var stripped = StripCommonWrapperFolderIfBatch(entries, out var strippedWrappers);
        var currentHints = inheritedHints
            .Concat(strippedWrappers)
            .Append(archiveStem)
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var docxCount = stripped.Count(e => IsDocx(e.Extension));
        var nestedArchives = stripped.Where(e => ArchiveExtensions.Contains(e.Extension)).ToList();

        // Trường hợp truyền thống: 1 sinh viên (<= 1 docx, không có archive con)
        if (nestedArchives.Count == 0 && docxCount <= 1)
        {
            try
            {
                var singleContent = BuildSingleContent(stripped);
                var docxEntry = stripped.FirstOrDefault(e => IsDocx(e.Extension));
                var folderSegments = stripped
                    .SelectMany(e => GetDirectorySegments(e.RelativePath))
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                var hints = new List<string>();
                if (docxEntry is not null)
                    hints.Add(Path.GetFileNameWithoutExtension(docxEntry.FileName));
                hints.AddRange(folderSegments);
                hints.AddRange(currentHints);

                archiveStream.Position = 0;
                var rawBytes = ReadStreamToBytes(archiveStream);
                var ext = Path.GetExtension(archiveFileName).ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(ext)) ext = ".zip";

                return
                [
                    new ExtractedPackage(
                        IsSuccess: true,
                        DisplayFileName: archiveFileName,
                        ErrorMessage: null,
                        OriginalBytes: rawBytes,
                        StorageExtension: ext,
                        StorageContentType: ext == ".rar" ? "application/vnd.rar" : "application/zip",
                        Content: singleContent,
                        StudentHints: hints.Distinct(StringComparer.OrdinalIgnoreCase).ToList())
                ];
            }
            catch (Exception ex)
            {
                return [FailurePackage(archiveFileName, ex.Message)];
            }
        }

        // Trường hợp Batch: giải nén từng bài trong archive phòng thi
        var results = new List<ExtractedPackage>();

        foreach (var nested in nestedArchives)
        {
            var nestedDisplay = $"{archiveFileName}/{nested.RelativePath}";
            var nestedHints = GetDirectorySegments(nested.RelativePath)
                .Concat(currentHints)
                .ToList();

            using var nestedStream = new MemoryStream(nested.Data, writable: false);
            var subPackages = ReadPackagesRecursive(nestedStream, nested.FileName, depth + 1, nestedHints);
            foreach (var pkg in subPackages)
            {
                results.Add(pkg with
                {
                    DisplayFileName = subPackages.Count == 1 ? nestedDisplay : $"{archiveFileName}/{pkg.DisplayFileName}"
                });
            }
        }

        var remaining = stripped.Where(e => !ArchiveExtensions.Contains(e.Extension)).ToList();
        var groupedByFolder = remaining.GroupBy(e => GetTopLevelDirectory(e.RelativePath), StringComparer.OrdinalIgnoreCase);

        foreach (var folderGroup in groupedByFolder)
        {
            var folderEntries = folderGroup.ToList();
            var folderDocxEntries = folderEntries.Where(e => IsDocx(e.Extension)).ToList();

            // Thư mục riêng cho từng sinh viên (tối đa 1 docx + diagrams/images đi kèm, kể cả nằm trong sub-folder)
            if (!string.IsNullOrEmpty(folderGroup.Key) && folderDocxEntries.Count <= 1)
            {
                var displayName = $"{archiveFileName}/{folderGroup.Key}";
                try
                {
                    var groupContent = BuildSingleContent(folderEntries);
                    var folderHints = new List<string>();
                    if (folderDocxEntries.Count == 1)
                        folderHints.Add(Path.GetFileNameWithoutExtension(folderDocxEntries[0].FileName));
                    folderHints.Add(folderGroup.Key);
                    folderHints.AddRange(currentHints);

                    var (bytes, ext, contentType) = PackageEntriesForStorage(folderEntries, folderDocxEntries);

                    results.Add(new ExtractedPackage(
                        IsSuccess: true,
                        DisplayFileName: displayName,
                        ErrorMessage: null,
                        OriginalBytes: bytes,
                        StorageExtension: ext,
                        StorageContentType: contentType,
                        Content: groupContent,
                        StudentHints: folderHints.Distinct(StringComparer.OrdinalIgnoreCase).ToList()));
                }
                catch (Exception ex)
                {
                    results.Add(FailurePackage(displayName, ex.Message));
                }

                continue;
            }

            // Nhiều file .docx nằm chung trong 1 thư mục (hoặc ở root của ZIP) -> tách từng .docx thành 1 bài thi
            foreach (var docxEntry in folderDocxEntries)
            {
                var displayName = $"{archiveFileName}/{docxEntry.RelativePath}";
                var hints = new List<string>
                {
                    Path.GetFileNameWithoutExtension(docxEntry.FileName)
                };
                hints.AddRange(GetDirectorySegments(docxEntry.RelativePath));
                hints.AddRange(currentHints);

                results.Add(new ExtractedPackage(
                    IsSuccess: true,
                    DisplayFileName: displayName,
                    ErrorMessage: null,
                    OriginalBytes: docxEntry.Data,
                    StorageExtension: ".docx",
                    StorageContentType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    Content: new Content(
                        docxEntry.Data,
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                        new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)),
                    StudentHints: hints.Distinct(StringComparer.OrdinalIgnoreCase).ToList()));
            }
        }

        if (results.Count == 0)
            return [FailurePackage(archiveFileName, "Archive không chứa bài làm .docx, sơ đồ hoặc hình ảnh hợp lệ.")];

        return results;
    }

    private static Content BuildSingleContent(IReadOnlyList<RawEntry> entries)
    {
        byte[]? wordDocument = null;
        var diagramSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var imageSources = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var baseName = Path.GetFileNameWithoutExtension(entry.FileName);

            if (IsDocx(entry.Extension) && wordDocument is null)
            {
                wordDocument = entry.Data;
            }
            else if (DiagramExtensions.Contains(entry.Extension))
            {
                var diagramName = GetQuestionKey(baseName);
                var source = Encoding.UTF8.GetString(entry.Data);
                if (string.Equals(entry.Extension, ".puml", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(entry.Extension, ".plantuml", StringComparison.OrdinalIgnoreCase))
                {
                    source = ConvertPlantUmlToXml(diagramName, source);
                }

                if (!diagramSources.TryAdd(diagramName, source))
                    throw new InvalidDataException($"Archive chứa nhiều file sơ đồ có cùng tên câu hỏi '{diagramName}'.");
            }
            else if (ImageExtensions.Contains(entry.Extension))
            {
                var imageKey = GetQuestionKey(baseName);
                if (!imageSources.TryAdd(imageKey, entry.Data))
                    throw new InvalidDataException($"Archive chứa nhiều file hình ảnh có cùng tên câu hỏi '{imageKey}'.");
            }
        }

        if (wordDocument is null && diagramSources.Count == 0 && imageSources.Count == 0)
            throw new InvalidDataException("Archive phải chứa ít nhất một file .docx, sơ đồ hoặc hình ảnh.");

        return new Content(wordDocument, diagramSources, imageSources);
    }

    private static List<RawEntry> ExtractRawEntries(Stream archiveStream)
    {
        using var archive = ArchiveFactory.OpenArchive(archiveStream);
        var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
        var rawEntries = new List<RawEntry>();
        long totalBytes = 0;

        foreach (var entry in entries)
        {
            var normalizedPath = NormalizeArchivePath(entry.Key ?? string.Empty);
            if (string.IsNullOrWhiteSpace(normalizedPath) || IsIgnoredSystemEntry(normalizedPath))
                continue;

            var fileName = Path.GetFileName(normalizedPath);
            var extension = Path.GetExtension(fileName);
            var maxEntryBytes = GetMaxEntryBytes(extension);
            if (maxEntryBytes <= 0)
                continue;

            var data = ReadEntry(entry, maxEntryBytes);
            totalBytes += data.Length;
            if (totalBytes > MaxTotalUncompressedBytes)
                throw new InvalidDataException("Tổng dung lượng giải nén vượt quá giới hạn cho phép (1 GB).");

            rawEntries.Add(new RawEntry(normalizedPath, fileName, extension, data));
        }

        return rawEntries;
    }

    private static List<RawEntry> StripCommonWrapperFolderIfBatch(
        List<RawEntry> entries,
        out List<string> strippedFolderNames)
    {
        strippedFolderNames = [];
        var current = entries;

        while (current.Count > 0 && current.All(e => e.RelativePath.Contains('/')))
        {
            var firstSegments = current
                .Select(e => e.RelativePath.Split('/')[0])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (firstSegments.Count != 1)
                break;

            var innerDocxAndArchives = current.Count(e =>
                IsDocx(e.Extension) || ArchiveExtensions.Contains(e.Extension));
            if (innerDocxAndArchives <= 1)
                break;

            strippedFolderNames.Add(firstSegments[0]);
            var prefix = firstSegments[0] + "/";
            current = current
                .Select(e => e with { RelativePath = e.RelativePath[prefix.Length..] })
                .ToList();
        }

        return current;
    }

    private static (byte[] Bytes, string Extension, string ContentType) PackageEntriesForStorage(
        IReadOnlyList<RawEntry> entries,
        IReadOnlyList<RawEntry> docxEntries)
    {
        if (entries.Count == 1 && docxEntries.Count == 1)
        {
            return (
                docxEntries[0].Data,
                ".docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        }

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                var zipEntry = zip.CreateEntry(entry.RelativePath, CompressionLevel.Optimal);
                using var target = zipEntry.Open();
                target.Write(entry.Data, 0, entry.Data.Length);
            }
        }

        return (ms.ToArray(), ".zip", "application/zip");
    }

    private static ExtractedPackage FailurePackage(string displayName, string error) => new(
        IsSuccess: false,
        DisplayFileName: displayName,
        ErrorMessage: error,
        OriginalBytes: [],
        StorageExtension: ".zip",
        StorageContentType: "application/octet-stream",
        Content: null,
        StudentHints: []);

    private static long GetMaxEntryBytes(string extension)
    {
        if (IsDocx(extension)) return MaxWordDocumentBytes;
        if (DiagramExtensions.Contains(extension)) return MaxDiagramBytes;
        if (ImageExtensions.Contains(extension)) return MaxImageBytes;
        if (ArchiveExtensions.Contains(extension)) return MaxNestedArchiveBytes;
        return 0;
    }

    private static bool IsDocx(string extension)
        => string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeArchivePath(string rawPath)
    {
        var path = rawPath.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s is "." or ".."))
            throw new InvalidDataException($"Đường dẫn trong archive không hợp lệ: '{rawPath}'.");

        return string.Join('/', segments);
    }

    private static bool IsIgnoredSystemEntry(string path)
    {
        var segments = path.Split('/');
        if (segments.Any(s => s.Equals("__MACOSX", StringComparison.OrdinalIgnoreCase)))
            return true;

        var fileName = segments[^1];
        return fileName.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("~$", StringComparison.Ordinal);
    }

    private static string GetTopLevelDirectory(string relativePath)
    {
        var idx = relativePath.IndexOf('/');
        return idx > 0 ? relativePath[..idx] : string.Empty;
    }

    private static IEnumerable<string> GetDirectorySegments(string relativePath)
    {
        var idx = relativePath.LastIndexOf('/');
        if (idx <= 0)
            return [];

        return relativePath[..idx]
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Reverse();
    }

    private static byte[] ReadStreamToBytes(Stream stream)
    {
        if (stream is MemoryStream ms)
            return ms.ToArray();

        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
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
