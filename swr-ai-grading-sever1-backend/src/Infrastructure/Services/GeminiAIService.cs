using System.Text;
using System.Text.Json;
using Application.DTOs.GradingDiaries;
using Application.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services;

public sealed class GeminiAIService : IGeminiAIService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiAIService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GEMINI_API_KEY"]
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? string.Empty;

        _model = configuration["GEMINI_MODEL"]
            ?? Environment.GetEnvironmentVariable("GEMINI_MODEL")
            ?? "gemini-3.6-flash";
    }

    public Task<string> ExtractTextFromDocxAsync(Stream docxStream, CancellationToken ct = default)
    {
        if (docxStream.CanSeek)
        {
            docxStream.Position = 0;
        }

        try
        {
            using var document = WordprocessingDocument.Open(docxStream, false);
            var body = document.MainDocumentPart?.Document?.Body;
            if (body is null)
            {
                return Task.FromResult(string.Empty);
            }

            var sb = new StringBuilder();

            foreach (var element in body.Elements())
            {
                if (element is Paragraph paragraph)
                {
                    var text = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text)).Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                }
                else if (element is Table table)
                {
                    sb.AppendLine("\n[BẢNG NỘI DUNG]:");
                    foreach (var row in table.Descendants<TableRow>())
                    {
                        var cells = row.Descendants<TableCell>()
                            .Select(cell => string.Concat(cell.Descendants<Text>().Select(t => t.Text)).Trim());
                        sb.AppendLine(string.Join(" | ", cells));
                    }
                    sb.AppendLine();
                }
            }

            return Task.FromResult(sb.ToString());
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or InvalidDataException)
        {
            throw new InvalidOperationException("File tài liệu không phải định dạng Word .docx hợp lệ.", ex);
        }
    }

    public async Task<GradingResultDto> GradeSubmissionAsync(
        string studentSubmissionText,
        string rubricContent,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("GEMINI_API_KEY chưa được cấu hình trong biến môi trường hoặc .env.");
        }

        var prompt = BuildPrompt(studentSubmissionText, rubricContent);

        var requestPayload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                maxOutputTokens = 8192,
                responseMimeType = "application/json"
            }
        };

        var requestJson = JsonSerializer.Serialize(requestPayload);
        using var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

        // Gửi API key qua header x-goog-api-key để đảm bảo an toàn (M4)
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-goog-api-key", _apiKey);
        request.Content = requestContent;

        using var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Lỗi gọi Gemini API (Status: {response.StatusCode}): {responseBody}");
        }

        var textResponse = ExtractTextFromGeminiResponse(responseBody);
        var cleanJson = CleanJsonFences(textResponse);

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            };

            var gradingResult = JsonSerializer.Deserialize<GradingResultDto>(cleanJson, options)
                ?? new GradingResultDto();

            gradingResult.RawAiLogJson = cleanJson;
            return gradingResult;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Không thể phân tích kết quả JSON từ Gemini: {cleanJson}", ex);
        }
    }

    private static string BuildPrompt(string studentSubmissionText, string rubricContent)
    {
        return $$$"""
            Bạn là giảng viên chuyên nghiệp chấm bài kiểm tra/đồ án của sinh viên.
            Nhiệm vụ của bạn là đọc kỹ bài làm của sinh viên và chấm điểm thật chi tiết, công tâm dựa trên Rubric đáp án được cung cấp.

            [RUBRIC ĐÁP ÁN & THANG ĐIỂM]
            {{{rubricContent}}}

            [BÀI LÀM CỦA SINH VIÊN]
            {{{studentSubmissionText}}}

            [YÊU CẦU ĐẦU RA]
            Hãy chấm điểm từng tiêu chí, tính tổng điểm và nhận xét chi tiết.
            Bạn PHẢI trả về định dạng JSON thuần túy theo cấu trúc sau (không kèm văn bản mở đầu hay kết thúc):
            {
              "total_score": 8.0,
              "criteria_scores": [
                {
                  "criterion": "Tên tiêu chí 1",
                  "max_score": 3.0,
                  "actual_score": 2.5,
                  "comment": "Nhận xét cụ thể đạt được gì, thiếu gì"
                }
              ],
              "missing_items": [
                "Nội dung còn thiếu sót 1",
                "Nội dung cần bổ sung 2"
              ],
              "overall_comment": "Nhận xét tổng thể chất lượng bài làm của sinh viên"
            }
            """;
    }

    private static string ExtractTextFromGeminiResponse(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("candidates", out var candidates) &&
            candidates.GetArrayLength() > 0)
        {
            var firstCandidate = candidates[0];
            if (firstCandidate.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0)
            {
                return parts[0].GetProperty("text").GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string CleanJsonFences(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[7..];
        }
        else if (trimmed.StartsWith("```"))
        {
            trimmed = trimmed[3..];
        }

        if (trimmed.EndsWith("```"))
        {
            trimmed = trimmed[..^3];
        }

        return trimmed.Trim();
    }
}
