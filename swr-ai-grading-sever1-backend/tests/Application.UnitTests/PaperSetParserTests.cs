using Application.Common;
using Application.DTOs.PaperSets;
using Application.Services;
using Domain.Enums;
using Xunit;

namespace Application.UnitTests;

public class PaperSetParserTests
{
    [Fact]
    public void Parse_Question_File_Docx_Returns_Expected_Questions()
    {
        var filePath = @"D:\FPT\2025_k9\DoAn\my-notes\test\Test_AI_Grading\Question_File.docx";
        Assert.True(File.Exists(filePath), "Question file must exist.");

        using var stream = File.OpenRead(filePath);
        var upload = new MaterialFileUpload
        {
            FileType = PaperSetFileType.Question,
            Content = stream,
            FileName = Path.GetFileName(filePath),
            Length = stream.Length
        };

        var result = PaperSetQuestionDocumentParser.Parse(upload);
        Assert.True(result.IsSuccess);
        var questions = result.Data!;

        Assert.Equal(4, questions.Count);
        Assert.Equal(0.5m, questions[0].Point);
        Assert.Equal(3.0m, questions[1].Point);
        Assert.Equal(4.0m, questions[2].Point);
        Assert.Equal(2.5m, questions[3].Point);
        Assert.Equal(10.0m, questions.Sum(q => q.Point));

        Assert.Equal("Context Diagram", questions[1].DetectedDiagramType);
        Assert.Equal("Use Case Diagram", questions[2].DetectedDiagramType);
        Assert.Equal("ERD", questions[3].DetectedDiagramType);
    }

    [Fact]
    public void Parse_SWR_Answer_Rubric_Xlsx_Returns_Expected_Rubrics()
    {
        var filePath = @"D:\FPT\2025_k9\DoAn\my-notes\test\Test_AI_Grading\SWR_Answer_Rubric.xlsx";
        Assert.True(File.Exists(filePath), "Rubric file must exist.");

        using var stream = File.OpenRead(filePath);
        var result = PaperSetRubricParser.Parse(stream);
        Assert.True(result.IsSuccess);
        var rubrics = result.Data!;

        Assert.True(rubrics.Count >= 2);
        var q2Rubric = rubrics.FirstOrDefault(r => r.QuestionNumber == "2");
        Assert.NotNull(q2Rubric);
        Assert.Equal(3.0m, q2Rubric.TotalPoint);
        Assert.NotEmpty(q2Rubric.SubItems);

        var q3Rubric = rubrics.FirstOrDefault(r => r.QuestionNumber == "3");
        Assert.NotNull(q3Rubric);
        Assert.Equal(4.0m, q3Rubric.TotalPoint);
        Assert.NotEmpty(q3Rubric.SubItems);
        Assert.Equal(15, q3Rubric.SubItems.Count);
    }

    [Fact]
    public void Parse_De01_Rubric_Xlsx_Returns_Expected_Rubrics()
    {
        var filePath = @"D:\FPT\2025_k9\DoAn\my-notes\test\BO_DE_THI_SP27\[1] DE_01_KHACH_SAN_HOTEL_BOOKING\DE_THI\Rubric_SWR_SP27_De01.xlsx";
        Assert.True(File.Exists(filePath), "De01 Rubric file must exist.");

        using var stream = File.OpenRead(filePath);
        var result = PaperSetRubricParser.Parse(stream);
        Assert.True(result.IsSuccess);
        var rubrics = result.Data!;

        Assert.NotEmpty(rubrics);
        Assert.Equal(10.0m, rubrics.Sum(r => r.TotalPoint));
    }
}
