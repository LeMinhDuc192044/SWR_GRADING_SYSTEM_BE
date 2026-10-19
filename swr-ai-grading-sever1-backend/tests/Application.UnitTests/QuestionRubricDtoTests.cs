using System.Text.Json;
using Application.DTOs.PaperSets;
using Application.DTOs.Questions;
using Application.DTOs.Rubrics;
using Xunit;

namespace Application.UnitTests;

public class QuestionRubricDtoTests
{
    [Fact]
    public void RubricDTO_CanSerializeAndDeserialize_SubItems()
    {
        var subItems = new List<RubricSubItemDto>
        {
            new() { SubId = "1.1", Point = 1.0m, Guideline = "Vẽ đúng actor" },
            new() { SubId = "1.2", Point = 2.0m, Guideline = "Vẽ đúng use cases" }
        };

        var json = JsonSerializer.Serialize(subItems, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var rubricDto = new RubricDTO
        {
            RubricId = Guid.NewGuid(),
            PaperSetId = Guid.NewGuid(),
            Point = 3.0m,
            Content = json,
            SubItems = JsonSerializer.Deserialize<List<RubricSubItemDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? []
        };

        Assert.Equal(2, rubricDto.SubItems.Count);
        Assert.Equal("1.1", rubricDto.SubItems[0].SubId);
        Assert.Equal(1.0m, rubricDto.SubItems[0].Point);
        Assert.Equal("Vẽ đúng actor", rubricDto.SubItems[0].Guideline);
    }

    [Fact]
    public void RubricDTO_Serializing_With_UnsafeRelaxedJsonEscaping_Preserves_Vietnamese()
    {
        var subItems = new List<RubricSubItemDto>
        {
            new() { SubId = "1.1", Point = 1.0m, Guideline = "Vẽ đúng actor và biểu đồ ca sử dụng" }
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        var json = JsonSerializer.Serialize(subItems, options);

        Assert.Contains("Vẽ đúng actor và biểu đồ ca sử dụng", json);
        Assert.DoesNotContain("\\u", json);
    }

    [Fact]
    public void QuestionDTO_CanHold_RubricSummary()
    {
        var questionDto = new QuestionDTO
        {
            QuestionId = Guid.NewGuid(),
            PaperSetId = Guid.NewGuid(),
            Title = "Question 1",
            Content = "Draw Context Diagram",
            Point = 2.5m,
            DiagramName = "Context Diagram",
            Rubric = new QuestionRubricSummaryDTO
            {
                RubricId = Guid.NewGuid(),
                Point = 2.5m,
                Content = "[{\"subId\":\"1\",\"point\":2.5}]",
                SubItems = new List<RubricSubItemDto>
                {
                    new() { SubId = "1", Point = 2.5m }
                }
            }
        };

        Assert.NotNull(questionDto.Rubric);
        Assert.Single(questionDto.Rubric.SubItems);
        Assert.Equal(2.5m, questionDto.Rubric.Point);
    }
}
