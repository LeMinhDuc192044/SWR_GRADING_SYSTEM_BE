using IronOcr;
using Xunit;
using Xunit.Abstractions;

namespace Application.UnitTests;

public class OcrHelperTests
{
    private readonly ITestOutputHelper _output;

    public OcrHelperTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ReadDiagramText()
    {
        var ocr = new IronTesseract();
        var imgPath = @"C:\Users\haivl\.gemini\antigravity\brain\532a1315-cefc-407e-907b-cbd014b6e70a\.user_uploaded\media_1793542755709_12e3201b.jpg";
        using var input = new OcrInput(imgPath);
        var result = ocr.Read(input);
        _output.WriteLine("=== OCR RESULT ===");
        _output.WriteLine(result.Text);
        Assert.NotNull(result.Text);
    }
}
