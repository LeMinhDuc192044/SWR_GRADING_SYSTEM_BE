using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Application.Common;
using Application.DTOs.PaperSets;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using IronOcr;

namespace Application.Services;

public static partial class PaperSetQuestionDocumentParser
{
    public static Result<IReadOnlyList<CreateQuestionInput>> Parse(MaterialFileUpload file)
    {
        if (!string.Equals(Path.GetExtension(file.FileName), ".docx", StringComparison.OrdinalIgnoreCase))
            return Result<IReadOnlyList<CreateQuestionInput>>.Failure("The question file must be a .docx Word document.", "INVALID_QUESTION_DOCUMENT");

        try
        {
            if (file.Content.CanSeek) file.Content.Position = 0;
            using var document = WordprocessingDocument.Open(file.Content, false);
            var paragraphs = document.MainDocumentPart?.Document.Body?.Descendants<Paragraph>()
                .Select(paragraph => string.Concat(paragraph.Descendants<Text>().Select(text => text.Text)).Trim())
                .Where(text => text.Length > 0)
                .ToList() ?? [];

            var questions = new List<CreateQuestionInput>();
            QuestionBlock? current = null;

            foreach (var paragraph in paragraphs)
            {
                var match = QuestionHeaderRegex().Match(paragraph);
                if (match.Success)
                {
                    AddQuestion(questions, current);
                    var qNumStr = match.Groups["number"].Value;
                    int.TryParse(qNumStr, out var qNum);
                    var rawPoint = match.Groups["point"].Value;
                    var subtitle = match.Groups["content"].Value.Trim();
                    var title = string.IsNullOrWhiteSpace(subtitle)
                        ? $"Question {qNumStr}"
                        : $"Question {qNumStr}: {subtitle}";

                    current = new QuestionBlock(
                        qNum,
                        title,
                        ParsePoint(rawPoint),
                        subtitle);
                    continue;
                }

                if (current is not null)
                {
                    current.Content.AppendLine(paragraph);
                }
            }

            AddQuestion(questions, current);
            return questions.Count == 0
                ? Result<IReadOnlyList<CreateQuestionInput>>.Failure("The question document does not contain any headings such as 'Question 1' or 'Câu 1'.", "NO_QUESTIONS_FOUND")
                : Result<IReadOnlyList<CreateQuestionInput>>.Success(questions);
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or InvalidDataException)
        {
            return Result<IReadOnlyList<CreateQuestionInput>>.Failure("The question document is not a valid .docx file.", "INVALID_QUESTION_DOCUMENT");
        }
        finally
        {
            if (file.Content.CanSeek) file.Content.Position = 0;
        }
    }

    public static Result<IReadOnlyList<CreateQuestionInput>> ParseOcr(MaterialFileUpload file)
    {
        try
        {
            if (file.Content.CanSeek) file.Content.Position = 0;
            using var memory = new MemoryStream();
            file.Content.CopyTo(memory);
            var ocr = new IronTesseract();
            using var input = new OcrInput();
            input.LoadImage(memory.ToArray());
            var result = ocr.Read(input);
            return ParseText(result.Text);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException)
        {
            return Result<IReadOnlyList<CreateQuestionInput>>.Failure("The question file could not be processed with OCR.", "OCR_FAILED");
        }
        finally
        {
            if (file.Content.CanSeek) file.Content.Position = 0;
        }
    }

    public static Result<IReadOnlyList<CreateQuestionInput>> ParseText(string text)
    {
        var questions = new List<CreateQuestionInput>();
        QuestionBlock? current = null;
        foreach (var paragraph in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(value => value.Trim()).Where(value => value.Length > 0))
        {
            var match = QuestionHeaderRegex().Match(paragraph);
            if (match.Success)
            {
                AddQuestion(questions, current);
                var qNumStr = match.Groups["number"].Value;
                int.TryParse(qNumStr, out var qNum);
                var subtitle = match.Groups["content"].Value.Trim();
                var title = string.IsNullOrWhiteSpace(subtitle) ? $"Question {qNumStr}" : $"Question {qNumStr}: {subtitle}";
                current = new QuestionBlock(qNum, title, ParsePoint(match.Groups["point"].Value), subtitle);
                continue;
            }

            if (current is not null) current.Content.AppendLine(paragraph);
        }

        AddQuestion(questions, current);
        return questions.Count == 0
            ? Result<IReadOnlyList<CreateQuestionInput>>.Failure("The OCR text does not contain headings such as 'Question 1'.", "NO_QUESTIONS_FOUND")
            : Result<IReadOnlyList<CreateQuestionInput>>.Success(questions);
    }

    private static void AddQuestion(List<CreateQuestionInput> questions, QuestionBlock? question)
    {
        if (question is null || question.Content.Length == 0) return;
        var fullText = $"{question.Title} {question.Content}";
        questions.Add(new CreateQuestionInput
        {
            QuestionNumber = question.Number,
            Title = question.Title,
            Content = question.Content.ToString().Trim(),
            Point = question.Point,
            DetectedDiagramType = DetectDiagramType(fullText)
        });
    }

    public static string? DetectDiagramType(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var lower = text.ToLowerInvariant();
        if (lower.Contains("context diagram") || lower.Contains("ngữ cảnh")) return "Context Diagram";
        if (lower.Contains("use case") || lower.Contains("usecase")) return "Use Case Diagram";
        if (lower.Contains("erd") || lower.Contains("entity-relationship") || lower.Contains("thực thể liên kết")) return "ERD";
        if (lower.Contains("swimlane") || lower.Contains("phân làn") || lower.Contains("activity")) return "Swimlane Diagram";
        if (lower.Contains("dfd") || lower.Contains("data flow")) return "DFD";
        if (lower.Contains("dialog map") || lower.Contains("hội thoại")) return "Dialog Map";
        return null;
    }

    private static decimal ParsePoint(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        value = value.Trim().Replace(',', '.');
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var point) ? point : 0;
    }

    [GeneratedRegex(@"^(?:Question|Câu)\s+(?<number>\d+)\s*(?:\((?<point>[\d.,]+)\s*(?:points?|pts?|điểm|đ)\))?\s*[:\-–—]?\s*(?<content>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex QuestionHeaderRegex();

    private sealed class QuestionBlock(int number, string title, decimal point, string initialContent)
    {
        public int Number { get; } = number;
        public string Title { get; } = title;
        public decimal Point { get; } = point;
        public StringBuilder Content { get; } = new(initialContent);
    }
}