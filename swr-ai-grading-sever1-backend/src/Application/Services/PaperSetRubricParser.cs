using System.Globalization;
using System.Text.RegularExpressions;
using Application.Common;
using Application.DTOs.PaperSets;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Application.Services;

public static partial class PaperSetRubricParser
{
    public static Result<IReadOnlyList<ParsedRubricDto>> Parse(Stream content)
    {
        try
        {
            if (content.CanSeek) content.Position = 0;
            using var document = SpreadsheetDocument.Open(content, false);
            var workbookPart = document.WorkbookPart;
            if (workbookPart is null)
                return Result<IReadOnlyList<ParsedRubricDto>>.Failure("Invalid Excel document structure.", "INVALID_RUBRIC_DOCUMENT");

            var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable?
                .Elements<SharedStringItem>().Select(item => item.InnerText).ToList() ?? [];

            var rubrics = new List<ParsedRubricDto>();

            foreach (var sheet in workbookPart.Workbook.Sheets?.Elements<Sheet>() ?? [])
            {
                var relationshipId = sheet.Id?.Value;
                if (string.IsNullOrEmpty(relationshipId) || workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
                    continue;

                var rows = worksheetPart.Worksheet.Descendants<Row>().ToList();
                if (rows.Count == 0) continue;

                var sheetRubrics = ParseWorksheet(rows, sharedStrings);
                if (sheetRubrics.Count > 0)
                {
                    rubrics.AddRange(sheetRubrics);
                }
            }

            if (rubrics.Count == 0)
            {
                return Result<IReadOnlyList<ParsedRubricDto>>.Failure("No valid rubric criteria found in the Excel document.", "NO_RUBRIC_CRITERIA_FOUND");
            }

            // Deduplicate / Merge if multiple sheets or contiguous blocks
            var grouped = rubrics
                .GroupBy(r => NormalizeQuestionNumber(r.QuestionNumber))
                .Select(g =>
                {
                    var first = g.First();
                    var allSubItems = g.SelectMany(x => x.SubItems).ToList();
                    return new ParsedRubricDto
                    {
                        QuestionNumber = first.QuestionNumber,
                        TotalPoint = allSubItems.Sum(x => x.Point),
                        SubItems = allSubItems
                    };
                })
                .OrderBy(r => ExtractNumber(r.QuestionNumber))
                .ToList();

            return Result<IReadOnlyList<ParsedRubricDto>>.Success(grouped);
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or InvalidDataException or IOException)
        {
            return Result<IReadOnlyList<ParsedRubricDto>>.Failure("Failed to read the rubric Excel file.", "RUBRIC_READ_FAILED");
        }
        finally
        {
            if (content.CanSeek) content.Position = 0;
        }
    }

    private static List<ParsedRubricDto> ParseWorksheet(List<Row> rows, IReadOnlyList<string> sharedStrings)
    {
        // Try format B first: Check if row 1 or 2 is a header (Criteria, MaxScore, EvaluationGuideline)
        var formatBRubrics = TryParseFormatB(rows, sharedStrings);
        if (formatBRubrics.Count > 0)
        {
            return formatBRubrics;
        }

        // Fallback to format A: Block-based (Question X rows followed by item rows)
        return ParseFormatA(rows, sharedStrings);
    }

    private static List<ParsedRubricDto> TryParseFormatB(List<Row> rows, IReadOnlyList<string> sharedStrings)
    {
        var result = new List<ParsedRubricDto>();
        int headerRowIndex = -1;
        int criteriaCol = -1, maxScoreCol = -1, guidelineCol = -1;

        for (int i = 0; i < Math.Min(5, rows.Count); i++)
        {
            var cellValues = GetRowCellMap(rows[i], sharedStrings);
            foreach (var (col, text) in cellValues)
            {
                var lower = text.ToLowerInvariant();
                if (lower.Contains("criteria") || lower.Contains("tiêu chí")) criteriaCol = col;
                if (lower.Contains("score") || lower.Contains("point") || lower.Contains("điểm")) maxScoreCol = col;
                if (lower.Contains("guideline") || lower.Contains("hướng dẫn") || lower.Contains("description") || lower.Contains("mô tả")) guidelineCol = col;
            }

            if (criteriaCol > 0 && maxScoreCol > 0)
            {
                headerRowIndex = i;
                break;
            }
        }

        if (headerRowIndex < 0) return result;

        var currentGroups = new Dictionary<string, List<RubricSubItemDto>>(StringComparer.OrdinalIgnoreCase);

        for (int i = headerRowIndex + 1; i < rows.Count; i++)
        {
            var cellValues = GetRowCellMap(rows[i], sharedStrings);
            if (!cellValues.TryGetValue(criteriaCol, out var criteriaText) || string.IsNullOrWhiteSpace(criteriaText))
                continue;

            decimal point = 0;
            if (cellValues.TryGetValue(maxScoreCol, out var scoreText))
            {
                point = ParseDecimalPoint(scoreText);
            }

            string guideline = string.Empty;
            if (guidelineCol > 0 && cellValues.TryGetValue(guidelineCol, out var gText))
            {
                guideline = gText.Trim();
            }

            var questionKey = ExtractQuestionKey(criteriaText);
            var subId = ExtractSubId(criteriaText);

            if (!currentGroups.TryGetValue(questionKey, out var items))
            {
                items = [];
                currentGroups[questionKey] = items;
            }

            items.Add(new RubricSubItemDto
            {
                SubId = subId,
                Point = point,
                Guideline = string.IsNullOrWhiteSpace(guideline) ? criteriaText.Trim() : guideline
            });
        }

        foreach (var (qKey, subItems) in currentGroups)
        {
            result.Add(new ParsedRubricDto
            {
                QuestionNumber = qKey,
                TotalPoint = subItems.Sum(s => s.Point),
                SubItems = subItems
            });
        }

        return result;
    }

    private static List<ParsedRubricDto> ParseFormatA(List<Row> rows, IReadOnlyList<string> sharedStrings)
    {
        var result = new List<ParsedRubricDto>();
        ParsedRubricDto? currentRubric = null;

        foreach (var row in rows)
        {
            var cellMap = GetRowCellMap(row, sharedStrings);
            if (cellMap.Count == 0) continue;

            // Check if this row is a Question header, e.g. "Question 2" or "Câu 2"
            // Ensure we only match the actual header cell, not accidental occurrences of "câu X" inside guidelines
            var headerCell = cellMap.Values.FirstOrDefault(IsQuestionHeaderCell);
            if (headerCell is not null && cellMap.Count <= 2)
            {
                var questionMatch = QuestionBlockRegex().Match(headerCell);
                if (questionMatch.Success)
                {
                    if (currentRubric is not null && currentRubric.SubItems.Count > 0)
                    {
                        currentRubric.TotalPoint = currentRubric.SubItems.Sum(s => s.Point);
                        result.Add(currentRubric);
                    }

                    currentRubric = new ParsedRubricDto
                    {
                        QuestionNumber = questionMatch.Groups["num"].Value,
                        TotalPoint = 0,
                        SubItems = []
                    };
                    continue;
                }
            }

            if (currentRubric is null) continue;

            var sortedCols = cellMap.OrderBy(k => k.Key).ToList();
            if (sortedCols.Count < 2) continue;

            // Check if row is a Total Points summary row (e.g. first cell is "Total Points" or "Tổng điểm")
            var firstCell = sortedCols[0].Value.Trim();
            if (firstCell.StartsWith("Total", StringComparison.OrdinalIgnoreCase) ||
                firstCell.StartsWith("Tổng", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var subId = sortedCols[0].Value.Trim();
            var pointText = sortedCols[1].Value.Trim();
            if (pointText.StartsWith("=", StringComparison.Ordinal)) continue;

            var itemPoint = ParseDecimalPoint(pointText);
            int pointColIndex = 1;

            if (itemPoint <= 0 && sortedCols.Count >= 3)
            {
                var p2 = ParseDecimalPoint(sortedCols[2].Value.Trim());
                if (p2 > 0)
                {
                    subId = sortedCols[1].Value.Trim();
                    itemPoint = p2;
                    pointColIndex = 2;
                }
            }

            if (itemPoint > 0)
            {
                var guidelines = sortedCols
                    .Where((_, idx) => idx != pointColIndex && idx != (pointColIndex - 1))
                    .Select(x => x.Value.Trim())
                    .Where(x => x.Length > 0 && !x.StartsWith("=", StringComparison.Ordinal));

                var guidelineText = string.Join(" | ", guidelines);

                currentRubric.SubItems.Add(new RubricSubItemDto
                {
                    SubId = subId,
                    Point = itemPoint,
                    Guideline = guidelineText
                });
            }
        }

        if (currentRubric is not null && currentRubric.SubItems.Count > 0)
        {
            currentRubric.TotalPoint = currentRubric.SubItems.Sum(s => s.Point);
            result.Add(currentRubric);
        }

        return result;
    }

    private static Dictionary<int, string> GetRowCellMap(Row row, IReadOnlyList<string> sharedStrings)
    {
        var map = new Dictionary<int, string>();
        foreach (var cell in row.Elements<Cell>())
        {
            var colIndex = GetColumnIndex(cell.CellReference?.Value);
            var text = GetCellText(cell, sharedStrings);
            if (!string.IsNullOrWhiteSpace(text))
            {
                map[colIndex] = text.Trim();
            }
        }
        return map;
    }

    private static int GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrEmpty(cellReference)) return 0;
        int col = 0;
        foreach (char c in cellReference)
        {
            if (char.IsLetter(c))
            {
                col = col * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
            }
            else break;
        }
        return col;
    }

    private static string GetCellText(Cell cell, IReadOnlyList<string> sharedStrings)
    {
        if ((cell.DataType?.Value == CellValues.SharedString || cell.DataType?.InnerText == "s") &&
            int.TryParse(cell.CellValue?.Text, out var sharedStringIndex) &&
            sharedStringIndex >= 0 && sharedStringIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedStringIndex];
        }

        return cell.InlineString?.InnerText ?? cell.CellValue?.Text ?? string.Empty;
    }

    private static decimal ParseDecimalPoint(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0;
        raw = raw.Trim().Replace(',', '.');
        var match = NumberRegex().Match(raw);
        if (match.Success && decimal.TryParse(match.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var point))
        {
            return point;
        }
        return 0;
    }

    private static string ExtractQuestionKey(string criteriaText)
    {
        var match = QuestionNumberFromCriteriaRegex().Match(criteriaText);
        if (match.Success)
        {
            return match.Groups["q"].Value;
        }
        return "1";
    }

    private static string ExtractSubId(string criteriaText)
    {
        var match = QuestionNumberFromCriteriaRegex().Match(criteriaText);
        if (match.Success && match.Groups["sub"].Success)
        {
            return match.Groups["sub"].Value;
        }
        return criteriaText.Length > 30 ? criteriaText[..30] : criteriaText;
    }

    private static string NormalizeQuestionNumber(string val)
    {
        var digits = DigitsOnlyRegex().Match(val);
        return digits.Success ? digits.Value : val.Trim();
    }

    private static int ExtractNumber(string val)
    {
        var digits = DigitsOnlyRegex().Match(val);
        return digits.Success && int.TryParse(digits.Value, out var n) ? n : 999;
    }

    private static bool IsQuestionHeaderCell(string text)
    {
        return QuestionHeaderCellRegex().IsMatch(text.Trim());
    }

    [GeneratedRegex(@"^(?:Question|Câu)\s+\d+(?:\s*[:\-].*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex QuestionHeaderCellRegex();

    [GeneratedRegex(@"(?:Question|Câu)\s+(?<num>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex QuestionBlockRegex();

    [GeneratedRegex(@"(?:Question|Câu)\s*(?<q>\d+)(?:\.(?<sub>\d+|[a-zA-Z]))?", RegexOptions.IgnoreCase)]
    private static partial Regex QuestionNumberFromCriteriaRegex();

    [GeneratedRegex(@"\d+(?:\.\d+)?")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitsOnlyRegex();
}
