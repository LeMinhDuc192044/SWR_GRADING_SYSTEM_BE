using Application.Common;
using Domain.Entities;

namespace Application.UnitTests;

public class DiagramElementMatcherTests
{
    private static DiagramElement Element(string code, string name) => new()
    {
        ElementId = Guid.NewGuid(),
        DiagramId = Guid.NewGuid(),
        ElementCode = code,
        Name = name
    };

    [Fact]
    public void Match_CodeInsideAnotherCode_MatchesOnlyTheLongerCode()
    {
        var e1 = Element("E1", "Class One");
        var e10 = Element("E10", "Class Ten");

        var match = DiagramElementMatcher.Match([e1, e10], "The answer uses E10 for the student entity.");

        Assert.Equal(e10.ElementId, match);
    }

    [Fact]
    public void Match_SingleLetterCodeInProse_DoesNotMatch()
    {
        var a = Element("A", "Class Alpha");
        var b = Element("B", "Class Beta");

        var match = DiagramElementMatcher.Match([a, b], "There is a class with b attributes.");

        Assert.Null(match);
    }

    [Fact]
    public void Match_CodeAsWholeToken_Matches()
    {
        var c01 = Element("C01", "Student Class");
        var c02 = Element("C02", "Course Class");

        var match = DiagramElementMatcher.Match([c01, c02], "The submitted diagram contains C02.");

        Assert.Equal(c02.ElementId, match);
    }

    [Fact]
    public void Match_NoCodeButElementName_MatchesByName()
    {
        var c01 = Element("C01", "Student Class");
        var c02 = Element("C02", "Course Class");

        var match = DiagramElementMatcher.Match([c01, c02], "This shows the Course Class with its attributes.");

        Assert.Equal(c02.ElementId, match);
    }

    [Fact]
    public void Match_MultipleElementsWithoutEvidence_ReturnsNull()
    {
        var match = DiagramElementMatcher.Match(
            [Element("C01", "Student Class"), Element("C02", "Course Class")], "An unrelated answer.");

        Assert.Null(match);
    }

    [Fact]
    public void Match_SingleElementDiagram_MatchesWithoutEvidence()
    {
        var only = Element("C01", "Student Class");

        var match = DiagramElementMatcher.Match([only], null, "Some diagram xml", null);

        Assert.Equal(only.ElementId, match);
    }
}
