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

    public Task<string> ExtractTextFromXlsxAsync(Stream xlsxStream, CancellationToken ct = default)
    {
        if (xlsxStream.CanSeek)
        {
            xlsxStream.Position = 0;
        }

        try
        {
            using var document = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(xlsxStream, false);
            var workbookPart = document.WorkbookPart;
            if (workbookPart == null) return Task.FromResult(string.Empty);

            var sharedStringTable = workbookPart.SharedStringTablePart?.SharedStringTable;
            var sb = new StringBuilder();

            foreach (var sheetPart in workbookPart.WorksheetParts)
            {
                var worksheet = sheetPart.Worksheet;
                foreach (var row in worksheet.Descendants<DocumentFormat.OpenXml.Spreadsheet.Row>())
                {
                    var rowValues = new List<string>();
                    foreach (var cell in row.Descendants<DocumentFormat.OpenXml.Spreadsheet.Cell>())
                    {
                        string cellValue = GetCellValue(cell, sharedStringTable);
                        if (!string.IsNullOrWhiteSpace(cellValue))
                        {
                            rowValues.Add(cellValue.Trim());
                        }
                    }
                    if (rowValues.Count > 0)
                    {
                        sb.AppendLine(string.Join(" | ", rowValues));
                    }
                }
            }

            return Task.FromResult(sb.ToString());
        }
        catch (Exception ex)
        {
            return Task.FromException<string>(ex);
        }
    }

    private static string GetCellValue(DocumentFormat.OpenXml.Spreadsheet.Cell cell, DocumentFormat.OpenXml.Spreadsheet.SharedStringTable? sharedStringTable)
    {
        if (cell.CellValue == null) return string.Empty;
        string val = cell.CellValue.Text;
        if (cell.DataType != null && cell.DataType.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.SharedString)
        {
            if (int.TryParse(val, out int index) && sharedStringTable != null)
            {
                var item = sharedStringTable.ElementAtOrDefault(index);
                return item?.InnerText ?? string.Empty;
            }
        }
        return val;
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

        var modelsToTry = new List<string> { _model, "gemini-2.5-flash" };

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
            Bạn là giảng viên chấm thi đại học chuyên nghiệp, công tâm và vô cùng cẩn thận.
            Nhiệm vụ của bạn là xem xét toàn bộ bài làm của sinh viên (bao gồm cả VĂN BẢN TRÍCH XUẤT và TẤT CẢ HÌNH ÁNH / SƠ ĐỒ ĐÍNH KÈM) và chấm điểm chi tiết từng tiêu chí dựa trên Rubric đáp án được cung cấp.

            [RUBRIC ĐÁP ÁN & THANG ĐIỂM CHI TIẾT]
            {{{rubricContent}}}

            [BÀI LÀM CỦA SINH VIÊN (VĂN BẢN & VỊ TRÍ SƠ ĐỒ)]
            {{{studentSubmissionText}}}

            [QUY TẮC PHÂN TÍCH HÌNH ÁNH / SƠ ĐỒ ĐÍNH KÈM - BẮT BUỘC THỰC HIỆN]
            1. Các hình ảnh/sơ đồ được đính kèm theo đúng thứ tự 1, 2, 3... tương ứng với các ký hiệu tag [HÌNH ÁNH / SƠ ĐỒ #1...], [HÌNH ÁNH / SƠ ĐỒ #2...] trong văn bản bài làm.
            2. Bạn PHẢI mở và phân tích TẤT CẢ các hình ảnh/sơ đồ đính kèm tương ứng với từng câu hỏi (như Question 1, Question 2, Question 3...).
            3. RẤT QUAN TRỌNG: Nếu một câu hỏi (ví dụ Question 3) có sơ đồ hoặc hình ảnh làm bài được chèn bên dưới tiêu đề câu hỏi đó, bạn PHẢI quan sát kỹ hình ảnh/sơ đồ đó để chấm điểm. TUYỆT ĐỐI KHÔNG ĐƯỢC kết luận là "Sinh viên chỉ ghi tiêu đề và bỏ trống" khi câu hỏi đó có sơ đồ/hình ảnh làm bài đi kèm.
            4. Hãy chấm đúng từng tiêu chí theo Rubric. Tổng điểm bằng tổng điểm thực tế của tất cả các tiêu chí.

            [YÊU CẦU ĐẦU RA JSON CHUẨN]
            Trả về duy nhất định dạng JSON thuần túy theo cấu trúc:
            {
              "total_score": 8.0,
              "criteria_scores": [
                {
                  "criterion": "Question X: Tên câu hỏi / tiêu chí",
                  "max_score": 3.0,
                  "actual_score": 2.5,
                  "comment": "Nhận xét chi tiết đạt được gì, thiếu sót gì dựa trên cả phần văn bản và sơ đồ/hình ảnh đính kèm của câu hỏi này."
                }
              ],
              "missing_items": [
                "Các chi tiết hoặc sơ đồ/yêu cầu còn thiếu"
              ],
              "overall_comment": "Nhận xét tổng quan toàn bộ bài làm."
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
