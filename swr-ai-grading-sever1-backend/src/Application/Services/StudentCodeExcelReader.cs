using Application.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Application.Services;

public sealed class StudentCodeExcelReader : IStudentCodeExcelReader
{
    private static readonly HashSet<string> HeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "studentcode", "student code", "student_code", "code", "mssv"
    };

    public IReadOnlyList<StudentCodeRow> Read(Stream stream)
    {
        if (stream.CanSeek) stream.Position = 0;

        try
        {
            using var document = SpreadsheetDocument.Open(stream, false);
            var workbookPart = document.WorkbookPart
                ?? throw new InvalidDataException("The Excel file has no workbook.");
            var firstSheet = workbookPart.Workbook.Descendants<Sheet>().FirstOrDefault()
                ?? throw new InvalidDataException("The Excel file has no worksheet.");
            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(firstSheet.Id!);
            var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

            var result = new List<StudentCodeRow>();
            var firstValueSeen = false;

            foreach (var row in worksheetPart.Worksheet.Descendants<Row>())
            {
                var cell = row.Elements<Cell>().FirstOrDefault(c => ColumnOf(c.CellReference?.Value) == "A");
                var value = cell is null ? string.Empty : GetCellText(cell, sharedStrings).Trim();
                if (value.Length == 0) continue;

                if (!firstValueSeen)
                {
                    firstValueSeen = true;
                    if (HeaderNames.Contains(value)) continue;   // skip header row
                }

                result.Add(new StudentCodeRow((int)(row.RowIndex?.Value ?? 0), value));
            }

            return result;
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or FileFormatException or InvalidDataException)
        {
            throw new InvalidDataException("The file is not a valid Excel (.xlsx) workbook.", ex);
        }
    }

    private static string ColumnOf(string? cellReference)
        => new((cellReference ?? string.Empty).TakeWhile(char.IsLetter).ToArray());

    private static string GetCellText(Cell cell, SharedStringTable? sharedStrings)
    {
        if (cell.DataType?.Value == CellValues.InlineString)
            return cell.InlineString?.Text?.Text ?? string.Empty;

        var raw = cell.CellValue?.Text ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString &&
            int.TryParse(raw, out var index) && sharedStrings is not null)
            return sharedStrings.ElementAtOrDefault(index)?.InnerText ?? string.Empty;

        return raw;
    }
}