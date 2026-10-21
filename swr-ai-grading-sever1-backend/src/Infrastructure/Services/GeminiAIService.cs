using System.Text;
using System.Text.Json;
using Application.DTOs.GradingDiaries;
using Application.Interfaces;
using Domain.Entities;
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
            ?? "gemini-3-flash-preview";
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

    public async Task<IReadOnlyList<ExtractedQuestionAnswerDto>> ExtractAnswersForQuestionsAsync(
        string studentSubmissionText,
        IReadOnlyList<Question> questions,
        List<DocxImageDto>? images = null,
        CancellationToken ct = default)
    {
        if (questions.Count == 0)
            return [];

        var questionData = questions.Select(question => new
        {
            question_id = question.QuestionId,
            title = question.Title,
            content = question.Content
        });
        var prompt = $$"""
            Trích xuất câu trả lời của sinh viên cho từng câu hỏi từ bài làm bên dưới.
            Chỉ sử dụng nội dung có trong bài làm, không tự suy luận hoặc bổ sung câu trả lời.
            Trả về đúng một mục cho mỗi question_id được cung cấp; nếu không tìm thấy câu trả lời, extracted_text phải là chuỗi rỗng.
            Với mỗi câu trả lời có sử dụng hình ảnh trong bài làm, thêm image_indexes là danh sách chỉ số hình ảnh bắt đầu từ 1 theo marker [HÌNH ÁNH / SƠ ĐỒ #N]. Nếu không dùng hình ảnh, trả về danh sách rỗng.
            Giữ nguyên nội dung và ngôn ngữ của sinh viên. Kết quả phải là JSON đúng định dạng:
            {"answers":[{"question_id":"GUID","extracted_text":"...","image_indexes":[1]}]}

            CÂU HỎI:
            {{JsonSerializer.Serialize(questionData)}}

            BÀI LÀM SINH VIÊN:
            {{studentSubmissionText}}
            """;

        var cleanJson = await GenerateJsonResponseAsync(prompt, images, ct);
        try
        {
            var result = JsonSerializer.Deserialize<ExtractedQuestionAnswersDto>(cleanJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return result?.Answers ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Không thể phân tích câu trả lời trích xuất từ Gemini: {cleanJson}", ex);
        }
    }

    public async Task<GradingResultDto> GradeSubmissionAnswersAsync(
        IReadOnlyList<(Question Question, SubmissionParsedAnswer ParsedAnswer)> questionAnswers,
        string rubricContent,
        List<DocxImageDto>? images = null,
        CancellationToken ct = default)
    {
        if (questionAnswers.Count == 0)
        {
            return new GradingResultDto();
        }

        var studentAnswersData = questionAnswers.Select(qa => new
        {
            question_id = qa.Question.QuestionId,
            question_title = qa.Question.Title,
            question_content = qa.Question.Content,
            max_point = qa.Question.Point,
            student_answer = string.IsNullOrWhiteSpace(qa.ParsedAnswer.ExtractedText) 
                ? "[Không có nội dung văn bản]" 
                : qa.ParsedAnswer.ExtractedText,
            diagram_element_attached = qa.ParsedAnswer.ElementId.HasValue,
            image_attached = !string.IsNullOrWhiteSpace(qa.ParsedAnswer.ExtractedImageUrl)
        });

        var prompt = $$$"""
            Bạn là giảng viên chấm thi đại học chuyên nghiệp, công tâm và vô cùng cẩn thận.
            Nhiệm vụ của bạn là xem xét toàn bộ câu trả lời của sinh viên cho từng câu hỏi (bao gồm cả VĂN BẢN TRẢ LỜI CỦA TỪNG CÂU và CÁC HÌNH ÁNH / SƠ ĐỒ ĐÍNH KÈM) và chấm điểm chi tiết từng câu dựa trên Rubric đáp án được cung cấp.

            [RUBRIC ĐÁP ÁN & THANG ĐIỂM CHI TIẾT]
            {{{rubricContent}}}

            [BÀI LÀM ĐÃ ĐƯỢC PHÂN TÁCH THEO TỪNG CÂU HỎI CỦA SINH VIÊN]
            {{{JsonSerializer.Serialize(studentAnswersData)}}}

            [QUY TẮC CHẤM ĐIỂM HÌNH ÁNH / SƠ ĐỒ & BÀI LÀM - BẮT BUỘC THỰC HIỆN]
            1. Các hình ảnh/sơ đồ được đính kèm theo thứ tự trong bài làm tương ứng với các câu hỏi.
            2. Bạn PHẢI quan sát kỹ hình ảnh/sơ đồ nếu câu hỏi yêu cầu vẽ biểu đồ/sơ đồ.
            3. RẤT QUAN TRỌNG: Nếu một câu hỏi có sơ đồ hoặc hình ảnh làm bài hoặc có văn bản trả lời, bạn PHẢI quan sát để chấm điểm. TUYỆT ĐỐI KHÔNG ĐƯỢC kết luận là sinh viên bỏ trống khi có câu trả lời hoặc sơ đồ.
            4. Điểm actual_score của từng câu không được vượt quá max_score của câu đó.
            5. Tổng điểm total_score bằng tổng điểm actual_score của tất cả các câu hỏi.

            [YÊU CẦU ĐẦU RA JSON CHUẨN]
            Bạn PHẢI trả về JSON đúng định dạng theo cấu trúc sau (bắt buộc có question_id cho từng tiêu chí):
            {
              "total_score": 8.0,
              "criteria_scores": [
                {
                  "question_id": "GUID của câu hỏi tương ứng trong danh sách trên",
                  "criterion": "Tiêu đề câu hỏi / Tiêu chí",
                  "max_score": 3.0,
                  "actual_score": 2.5,
                  "comment": "Nhận xét chi tiết đạt được gì, thiếu sót gì dựa trên bài làm và rubric của câu hỏi này",
                  "not_recognized": "Ghi chú những phần AI không nhận diện được rõ (chữ mờ, sơ đồ không đọc được chi tiết, công thức mờ, v.v.), hoặc để trống/null nếu nhận diện tốt"
                }
              ],
              "missing_items": [
                "Nội dung hoặc sơ đồ còn thiếu"
              ],
              "overall_comment": "Nhận xét tổng quan toàn bộ bài làm của sinh viên"
            }
            """;

        var cleanJson = await GenerateJsonResponseAsync(prompt, images, ct);

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
            throw new InvalidOperationException($"Không thể phân tích kết quả JSON chấm điểm từ Gemini: {cleanJson}", ex);
        }
    }

    private async Task<string> GenerateJsonResponseAsync(string prompt, List<DocxImageDto>? images, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("GEMINI_API_KEY chưa được cấu hình trong biến môi trường hoặc .env.");

        var partsList = new List<object> { new { text = prompt } };
        if (images is not null)
        {
            partsList.AddRange(images
                .Where(image => !string.IsNullOrWhiteSpace(image.Base64Data))
                .Select(image => (object)new
                {
                    inline_data = new { mime_type = image.MimeType, data = image.Base64Data }
                }));
        }

        var requestJson = JsonSerializer.Serialize(new
        {
            contents = new[] { new { parts = partsList.ToArray() } },
            generationConfig = new { temperature = 0.2, maxOutputTokens = 8192, responseMimeType = "application/json" }
        });

        HttpResponseMessage response = null!;
        string responseBody = string.Empty;
        var success = false;
        foreach (var currentModel in new[] { _model, "gemini-3-flash-preview", "gemini-3.5-flash", "gemini-3.5-flash-lite" })
        {
            for (var attempt = 1; attempt <= 5; attempt++)
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"https://generativelanguage.googleapis.com/v1beta/models/{currentModel}:generateContent");
                request.Headers.Add("x-goog-api-key", _apiKey);
                request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                response = await _httpClient.SendAsync(request, ct);
                responseBody = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                {
                    success = true;
                    break;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    break;

                if ((response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                     response.StatusCode == (System.Net.HttpStatusCode)429) && attempt < 5)
                {
                    var delaySeconds = attempt * 4;
                    var match = System.Text.RegularExpressions.Regex.Match(
                        responseBody,
                        @"retry in ([\d\.]+)s",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                        delaySeconds = (int)Math.Ceiling(seconds) + 1;
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
                }
            }

            if (success)
                break;
        }

        if (!success)
            throw new InvalidOperationException($"Lỗi gọi Gemini API (Status: {response.StatusCode}): {responseBody}");

        return CleanJsonFences(ExtractTextFromGeminiResponse(responseBody));
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
