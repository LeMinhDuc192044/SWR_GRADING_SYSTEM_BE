using System.Text;
using System.Text.Json;
using Application.DTOs.GradingDiaries;
using Application.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using IronOcr;
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
            ?? "gemini-3.8-flash";
    }

    public async Task<string> ExtractTextFromDocxAsync(Stream docxStream, CancellationToken ct = default)
    {
        var content = await ExtractContentFromDocxAsync(docxStream, ct);
        return content.Text;
    }

    public Task<DocxExtractedContentDto> ExtractContentFromDocxAsync(Stream docxStream, CancellationToken ct = default)
    {
        if (docxStream.CanSeek)
        {
            docxStream.Position = 0;
        }

        try
        {
            using var document = WordprocessingDocument.Open(docxStream, false);
            var mainPart = document.MainDocumentPart;
            var body = mainPart?.Document?.Body;
            var result = new DocxExtractedContentDto();
            var sb = new StringBuilder();

            if (body is not null && mainPart is not null)
            {
                var processedParts = new HashSet<ImagePart>();
                int imageIndex = 1;

                void ProcessImagePart(ImagePart imagePart)
                {
                    if (processedParts.Contains(imagePart)) return;
                    processedParts.Add(imagePart);

                    try
                    {
                        using var imageStream = imagePart.GetStream();
                        using var ms = new MemoryStream();
                        imageStream.CopyTo(ms);
                        var imageBytes = ms.ToArray();
                        if (imageBytes.Length > 0)
                        {
                            var rawContentType = (imagePart.ContentType ?? string.Empty).ToLowerInvariant();
                            var mimeType = rawContentType switch
                            {
                                var c when c.Contains("jpeg") || c.Contains("jpg") => "image/jpeg",
                                var c when c.Contains("webp") => "image/webp",
                                var c when c.Contains("gif") => "image/gif",
                                _ => "image/png"
                            };
                            var base64 = Convert.ToBase64String(imageBytes);
                            result.Images.Add(new DocxImageDto
                            {
                                MimeType = mimeType,
                                Base64Data = base64
                            });

                            sb.AppendLine($"\n[HÌNH ÁNH / SƠ ĐỒ #{imageIndex} ĐƯỢC CHÈN TRỰC TIẾP TẠI VỊ TRÍ NÀY]");

                            try
                            {
                                var ocr = new IronTesseract();
                                using var input = new OcrInput();
                                input.LoadImage(imageBytes);
                                var ocrResult = ocr.Read(input);
                                if (!string.IsNullOrWhiteSpace(ocrResult.Text))
                                {
                                    sb.AppendLine($"[VĂN BẢN TRÍCH XUẤT TỪ HÌNH ÁNH #{imageIndex}]:");
                                    sb.AppendLine(ocrResult.Text.Trim());
                                }
                            }
                            catch { }

                            imageIndex++;
                        }
                    }
                    catch { }
                }

                void TryProcessPart(string? rId)
                {
                    if (string.IsNullOrEmpty(rId)) return;
                    try
                    {
                        var part = mainPart.GetPartById(rId);
                        if (part is ImagePart imgPart)
                        {
                            ProcessImagePart(imgPart);
                        }
                    }
                    catch
                    {
                        // Bỏ qua nếu rId không phải là ImagePart
                    }
                }

                foreach (var element in body.Elements())
                {
                    if (element is Paragraph paragraph)
                    {
                        var text = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text)).Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            sb.AppendLine(text);
                        }

                        // Tìm hình ảnh gắn liền trong Paragraph này theo thứ tự
                        foreach (var blip in paragraph.Descendants<DocumentFormat.OpenXml.Drawing.Blip>())
                        {
                            TryProcessPart(blip.Embed?.Value);
                        }

                        foreach (var imgData in paragraph.Descendants<DocumentFormat.OpenXml.Vml.ImageData>())
                        {
                            TryProcessPart(imgData.RelationshipId?.Value);
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

                        foreach (var blip in table.Descendants<DocumentFormat.OpenXml.Drawing.Blip>())
                        {
                            TryProcessPart(blip.Embed?.Value);
                        }
                    }
                }

                // Xử lý các ImageParts dư còn lại nếu chưa được duyệt ở trên
                if (mainPart.ImageParts != null)
                {
                    foreach (var imgPart in mainPart.ImageParts)
                    {
                        ProcessImagePart(imgPart);
                    }
                }
            }

            result.Text = sb.ToString();
            if (string.IsNullOrWhiteSpace(result.Text) && result.Images.Count > 0)
            {
                result.Text = "[Tài liệu chứa các sơ đồ / hình ảnh đính kèm bên dưới]";
            }

            return Task.FromResult(result);
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or InvalidDataException)
        {
            throw new InvalidOperationException("File tài liệu không phải định dạng Word .docx hợp lệ.", ex);
        }
    }

    public async Task<GradingResultDto> GradeSubmissionAsync(
        string studentSubmissionText,
        string rubricContent,
        List<DocxImageDto>? images = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("GEMINI_API_KEY chưa được cấu hình trong biến môi trường hoặc .env.");
        }

        var prompt = BuildPrompt(studentSubmissionText, rubricContent);

        var partsList = new List<object>
        {
            new { text = prompt }
        };

        if (images != null && images.Count > 0)
        {
            foreach (var img in images)
            {
                if (!string.IsNullOrWhiteSpace(img.Base64Data))
                {
                    partsList.Add(new
                    {
                        inline_data = new
                        {
                            mime_type = img.MimeType,
                            data = img.Base64Data
                        }
                    });
                }
            }
        }

        var requestPayload = new
        {
            contents = new[]
            {
                new
                {
                    parts = partsList.ToArray()
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
        HttpResponseMessage response = null!;
        string responseBody = string.Empty;

        var modelsToTry = new List<string> { _model };

        bool success = false;
        foreach (var currentModel in modelsToTry)
        {
            const int maxRetries = 5;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{currentModel}:generateContent";
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Add("x-goog-api-key", _apiKey);
                request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                response = await _httpClient.SendAsync(request, ct);
                responseBody = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                {
                    success = true;
                    break;
                }

                // Nếu model 404 (Không tồn tại/bị bãi bỏ), lập tức dừng thử model này để chuyển sang model kế tiếp
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    break;
                }

                // Nếu gặp 503 (ServiceUnavailable) hoặc 429 (Rate Limit), chờ vài giây rồi thử lại
                if ((response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                     response.StatusCode == (System.Net.HttpStatusCode)429) && attempt < maxRetries)
                {
                    int delaySeconds = attempt * 4; // 4s, 8s, 12s...
                    
                    var match = System.Text.RegularExpressions.Regex.Match(responseBody, @"retry in ([\d\.]+)s", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var secondsParsed))
                    {
                        delaySeconds = (int)Math.Ceiling(secondsParsed) + 1;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
                    continue;
                }
            }

            if (success) break;
        }

        if (!success)
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

            [LƯU Ý QUAN TRỌNG VỀ HÌNH ẢNH / SƠ ĐỒ ĐÍNH KÈM]
            - Bài làm của sinh viên bao gồm cả VĂN BẢN TRÍCH XUẤT bên trên VÀ CÁC HÌNH ẢNH / SƠ ĐỒ (Use Case diagram, Class diagram, Sequence diagram, ERD, Flowchart...) được đính kèm trực tiếp trong yêu cầu này.
            - Bạn PHẢI quan sát và phân tích tất cả các hình ảnh/sơ đồ đính kèm bên dưới để đánh giá bài làm của sinh viên.
            - Nếu sinh viên vẽ sơ đồ hoặc làm bài trong hình ảnh đính kèm, bạn PHẢI chấm điểm đạt tương ứng với tiêu chí đó. TUYỆT ĐỐI KHÔNG ĐƯỢC đánh giá là "sinh viên bỏ trống hoặc không trả lời" khi có hình ảnh/sơ đồ thể hiện bài làm.

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
                  "comment": "Nhận xét cụ thể đạt được gì, thiếu gì (kết hợp phân tích từ văn bản và sơ đồ/hình ảnh đính kèm)"
                }
              ],
              "missing_items": [
                "Nội dung còn thiếu sót 1",
                "Nội dung cần bổ sung 2"
              ],
              "overall_comment": "Nhận xét tổng thể chất lượng bài làm của sinh viên (bao gồm cả phần văn bản và sơ đồ/hình ảnh đính kèm)"
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
