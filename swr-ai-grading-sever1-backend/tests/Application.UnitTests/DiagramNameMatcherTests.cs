using Application.Common;
using Domain.Entities;

namespace Application.UnitTests;

public class DiagramNameMatcherTests
{
    private static Diagram Diagram(string name) => new()
    {
        DiagramId = Guid.NewGuid(),
        Name = name
    };

    [Fact]
    public void Match_DiagramNameInTitle_Matches()
    {
        var useCase = Diagram("Use Case Diagram");
        var erd = Diagram("ERD");

        var match = DiagramNameMatcher.Match(
            "Vẽ sơ đồ Use Case cho hệ thống", "Sinh viên nộp file.", [useCase, erd]);

        Assert.Equal(useCase.DiagramId, match);
    }

    [Fact]
    public void Match_DiagramNameInContentWithHyphen_Matches()
    {
        var useCase = Diagram("Use Case Diagram");

        var match = DiagramNameMatcher.Match(
            "Câu 2", "Hãy vẽ use-case diagram cho kịch bản đặt lịch.", [useCase]);

        Assert.Equal(useCase.DiagramId, match);
    }

    [Fact]
    public void Match_NoMentionedDiagram_ReturnsNull()
    {
        var useCase = Diagram("Use Case Diagram");

        var match = DiagramNameMatcher.Match(
            "Câu 1", "Trình bày khái niệm kiểm thử hộp đen.", [useCase]);

        Assert.Null(match);
    }

    [Fact]
    public void Match_WordFragment_DoesNotMatch()
    {
        var erd = Diagram("ERD");

        var match = DiagramNameMatcher.Match("Câu 1", "Herderd is not a diagram word.", [erd]);

        Assert.Null(match);
    }

    [Fact]
    public void Match_TwoDiagramsMentioned_LongestNameWins()
    {
        var shortName = Diagram("Case");
        var longName = Diagram("Use Case Diagram");

        var match = DiagramNameMatcher.Match(
            "Câu 3", "Hoàn thiện sơ đồ Use Case Diagram của hệ thống.", [shortName, longName]);

        Assert.Equal(longName.DiagramId, match);
    }

    [Fact]
    public void Match_NoDiagrams_ReturnsNull()
    {
        var match = DiagramNameMatcher.Match("Use Case", "Vẽ sơ đồ", []);

        Assert.Null(match);
    }
}
