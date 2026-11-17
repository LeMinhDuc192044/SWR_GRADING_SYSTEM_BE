using Application.Services;

namespace Application.UnitTests;

public class StudentCodeParserTests
{
    [Theory]
    [InlineData("SE192044", "SE192044")]
    [InlineData("SE192044_1", "SE192044")]
    [InlineData("se192044_2", "SE192044")]
    [InlineData("SE192044_1.rar", "SE192044")]
    [InlineData("SE19204", "SE19204")]
    [InlineData("XX_SE192044", "SE192044")]
    [InlineData("  SE192044 extra", "SE192044")]
    [InlineData("SE1920441", "SE1920441")]
    public void TryExtract_ReturnsStudentCode(string name, string expected)
    {
        Assert.True(StudentCodeParser.TryExtract(name, out var code));
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("")]
    [InlineData("SE")]
    public void TryExtract_ReturnsFalseWithoutCode(string name)
    {
        Assert.False(StudentCodeParser.TryExtract(name, out var code));
        Assert.Equal(string.Empty, code);
    }
}
