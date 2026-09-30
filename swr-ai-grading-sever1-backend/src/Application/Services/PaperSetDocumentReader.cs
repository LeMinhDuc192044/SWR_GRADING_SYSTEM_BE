using System.Text;
using Application.Common;
using Application.DTOs.PaperSets;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using IronOcr;

namespace Application.Services;

public static class PaperSetDocumentReader
{
    public static Result<string> Read(MaterialFileUpload file)
    {
        try
        {
            if (file.Content.CanSeek) file.Content.Position = 0;

            var extension = Path.GetExtension(file.FileName);
            var text = extension.ToLowerInvariant() switch
            {
                ".docx" => ReadWordDocument(file.Content),
                ".xlsx" => ReadSpreadsheet(file.Content),
                ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff" => ReadImageWithOcr(file.Content),
                _ => null
            };

            return text is null
                ? Result<string>.Failure("Supported files are .docx, .xlsx, or image files (.png, .jpg, .jpeg, .bmp, .tif, .tiff).", "UNSUPPORTED_FILE_TYPE")
                : string.IsNullOrWhiteSpace(text)
                    ? Result<string>.Failure("No readable text was found in the file.", "NO_TEXT_FOUND")
                    : Result<string>.Success(text.Trim());
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or InvalidDataException or IOException or ArgumentException or InvalidOperationException)
        {
            return Result<string>.Failure("The file is invalid or could not be read.", "DOCUMENT_READ_FAILED");
        }
        catch (Exception ex) when (ex.GetType().Namespace?.StartsWith("IronSoftware", StringComparison.Ordinal) == true)
        {
            return Result<string>.Failure("OCR could not read the image file.", "OCR_FAILED");
        }
        finally
        {
            if (file.Content.CanSeek) file.Content.Position = 0;
        }
    }

    private static string ReadWordDocument(Stream content)
    {
        using var document = WordprocessingDocument.Open(content, false);
        return string.Join(Environment.NewLine,
            document.MainDocumentPart?.Document.Body?.Descendants<Paragraph>()
                .Select(paragraph => string.Concat(paragraph.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(text => text.Text)).Trim())
                .Where(text => text.Length > 0) ?? []);
    }

    private static string ReadSpreadsheet(Stream content)
    {
        using var document = SpreadsheetDocument.Open(content, false);
        var workbookPart = document.WorkbookPart;
        if (workbookPart is null) return string.Empty;

        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable?
            .Elements<SharedStringItem>().Select(item => item.InnerText).ToList() ?? [];
        var output = new StringBuilder();

        foreach (var sheet in workbookPart.Workbook.Sheets?.Elements<Sheet>() ?? [])
        {
            var relationshipId = sheet.Id?.Value;
            if (string.IsNullOrEmpty(relationshipId) || workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
                continue;

            var rows = worksheetPart.Worksheet.Descendants<Row>()
                .Select(row => string.Join("\t", row.Elements<Cell>()
                    .Select(cell => GetCellText(cell, sharedStrings))
                    .Where(value => value.Length > 0)))
                .Where(row => row.Length > 0)
                .ToList();

            if (rows.Count == 0) continue;
            if (output.Length > 0) output.AppendLine().AppendLine();
            output.Append('[').Append(sheet.Name?.Value ?? "Sheet").AppendLine("]");
            output.Append(string.Join(Environment.NewLine, rows));
        }

        return output.ToString();
    }

    private static string GetCellText(Cell cell, IReadOnlyList<string> sharedStrings)
    {
        if (cell.DataType?.Value == CellValues.SharedString &&
            int.TryParse(cell.CellValue?.Text, out var sharedStringIndex) &&
            sharedStringIndex >= 0 && sharedStringIndex < sharedStrings.Count)
            return sharedStrings[sharedStringIndex];

        return cell.InlineString?.InnerText ?? cell.CellValue?.Text ?? string.Empty;
    }

    private static string ReadImageWithOcr(Stream content)
    {
        using var memory = new MemoryStream();
        content.CopyTo(memory);
        var ocr = new IronTesseract();
        using var input = new OcrInput();
        input.LoadImage(memory.ToArray());
        return ocr.Read(input).Text;
    }
}