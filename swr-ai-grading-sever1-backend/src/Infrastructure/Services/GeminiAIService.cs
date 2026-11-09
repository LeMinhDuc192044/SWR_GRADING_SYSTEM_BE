using System.Text;
using System.Text.Json;
using Application.DTOs.GradingDiaries;
using Application.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Domain.Entities;
using IronOcr;
using Microsoft.Extensions.Configuration;
using System.Xml.Linq;
using System.Text.RegularExpressions;

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

        var cleanJson = await GenerateJsonResponseAsync(prompt, images, ct, temperature: 0);
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
            return new GradingResultDto();

        // One entry per question; each entry holds all of that question's answers (text + XML + image flag).
        var studentAnswersData = questionAnswers
            .GroupBy(qa => qa.Question.QuestionId)
            .Select(g => new
            {
                question_id = g.Key,
                question_title = g.First().Question.Title,
                question_content = g.First().Question.Content,
                max_point = g.First().Question.Point,
                answers = g.Select(qa => new
                {
                    student_answer_text = string.IsNullOrWhiteSpace(qa.ParsedAnswer.ExtractedText)
                        ? "[Không có nội dung văn bản]" : qa.ParsedAnswer.ExtractedText,
                    diagram_xml_summary = string.IsNullOrWhiteSpace(qa.ParsedAnswer.Extracted_Xml)
                        ? "[Không có XML]" : SummarizeDiagramXml(qa.ParsedAnswer.Extracted_Xml),
                    image_attached = !string.IsNullOrWhiteSpace(qa.ParsedAnswer.ExtractedImageUrl)
                }).ToList()
            }).ToList();

        var prompt = $$$"""
            Bạn là máy chấm thi. Chỉ đối chiếu bài làm với tiêu chí chấm. KHÔNG khen, KHÔNG động viên, KHÔNG nhận xét cảm tính.

            [RUBRIC: MỖI CÂU GỒM CÁC TIÊU CHÍ ĐÁNH SỐ criterion_no]
            {{{rubricContent}}}

            [BÀI LÀM CỦA SINH VIÊN THEO TỪNG CÂU]
            {{{JsonSerializer.Serialize(studentAnswersData, PromptJsonOptions)}}}

            Mỗi câu có tối đa 3 nguồn bằng chứng:
            - student_answer_text: văn bản sinh viên viết.
            - diagram_xml_summary: XML draw.io đã rút gọn (danh sách đối tượng, nhãn, các kết nối, kiểu mũi tên).
            - Hình ảnh đính kèm sau prompt; mỗi ảnh có một dòng nhãn ghi rõ thuộc câu hỏi nào.

            [CÁCH CHẤM - BẮT BUỘC]
            1. Với từng câu, xét lần lượt từng tiêu chí (criterion_no). Với mỗi tiêu chí, đối chiếu CẢ BA nguồn (văn bản, XML, hình ảnh) với yêu cầu của tiêu chí. Một ý được tính nếu xuất hiện ở BẤT KỲ nguồn nào; cùng một ý xuất hiện ở nhiều nguồn chỉ tính MỘT lần. Khi các nguồn mâu thuẫn: XML đúng hơn cho tên/nhãn/quan hệ, hình ảnh dùng để kiểm tra ký hiệu và bố cục.
            2. Tiêu chí dạng "Award X point for each ...": liệt kê trong matched_items MỖI ý đúng (một chuỗi ngắn, ghi nguồn trong ngoặc, ví dụ "Book an appointment (xml)"). Ý trùng lặp, ý sai, ý không phù hợp với kịch bản trong question_content, hoặc ý không đúng loại mà tiêu chí yêu cầu (ví dụ không phải yêu cầu chức năng) KHÔNG được tính: đưa vào rejected_items kèm lý do ngắn. awarded_score = số ý đúng x X, không vượt max_point của tiêu chí.
            3. Tiêu chí dạng khác: awarded_score theo đúng mô tả tiêu chí, không vượt max_point; matched_items là các ý đã thỏa. Không có bằng chứng thì không cho điểm.
            4. Chỉ tính những gì NHÌN THẤY trong bài. Không suy đoán, không cho điểm thiện chí. Phần nào không đọc được thì ghi vào not_recognized và không cho điểm phần đó. KHÔNG kết luận sinh viên bỏ trống khi có văn bản, XML hoặc hình ảnh: phải xem đủ cả ba.
            5. actual_score của câu = tổng awarded_score các tiêu chí và không vượt max_score của câu.
            6. Mỗi question_id chỉ trả về MỘT mục trong criteria_scores (gộp mọi câu trả lời của câu đó).

            [QUY TẮC VIẾT NHẬN XÉT]
            - CẤM mọi lời khen, động viên, đánh giá cảm tính (ví dụ: "tốt", "xuất sắc", "rất đầy đủ", "ấn tượng", "good", "great", "well done").
            - comment của từng tiêu chí: tối đa 2 câu, chỉ nêu sự việc: đếm được bao nhiêu ý đúng, ý nào bị loại và vì sao, ý nào còn thiếu so với rubric.
            - comment của câu và overall_comment: tối đa 2 câu, chỉ nêu lỗi hoặc thiếu sót chính. Không có lỗi thì để chuỗi rỗng.
            - Viết nhận xét bằng cùng ngôn ngữ với rubric.

            [ĐỊNH DẠNG JSON ĐẦU RA]
            {
            "criteria_scores": [
                {
                "question_id": "GUID của câu hỏi",
                "criterion": "Tiêu đề câu hỏi",
                "max_score": 3.0,
                "actual_score": 1.5,
                "criteria_results": [
                    {
                    "criterion_no": 1,
                    "matched_items": ["Book an appointment (xml)"],
                    "rejected_items": ["Login: không có trong kịch bản"],
                    "awarded_score": 0.25,
                    "comment": "1 ý đúng. Login bị loại vì không thuộc kịch bản."
                    }
                ],
                "comment": "Điểm bị trừ của câu (không khen)",
                "not_recognized": null
                }
            ],
            "missing_items": [],
            "overall_comment": ""
            }
            """;

        var cleanJson = await GenerateJsonResponseAsync(prompt, images, ct, temperature: 0);

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            };
            var gradingResult = JsonSerializer.Deserialize<GradingResultDto>(cleanJson, options) ?? new GradingResultDto();
            gradingResult.RawAiLogJson = cleanJson;
            return gradingResult;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Không thể phân tích kết quả JSON chấm điểm từ Gemini: {cleanJson}", ex);
        }
    }

    private async Task<string> GenerateJsonResponseAsync(string prompt, List<DocxImageDto>? images, CancellationToken ct, float temperature)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("GEMINI_API_KEY chưa được cấu hình trong biến môi trường hoặc .env.");

        var partsList = new List<object> { new { text = prompt } };
        if (images is not null)
        {
            foreach (var image in images.Where(i => !string.IsNullOrWhiteSpace(i.Base64Data)))
            {
                if (!string.IsNullOrWhiteSpace(image.Label))
                    partsList.Add(new { text = image.Label });          // label comes right before its image
                partsList.Add(new { inline_data = new { mime_type = image.MimeType, data = image.Base64Data } });
            }
        }

        var requestJson = JsonSerializer.Serialize(new
        {
            contents = new[]
            {
                new { parts = partsList.ToArray() }
            },
            generationConfig = new
            {
                temperature,
                maxOutputTokens = 16384,
                responseMimeType = "application/json"
            }
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

            if (firstCandidate.TryGetProperty("finishReason", out var finishReasonProp))
            {
                var finishReason = finishReasonProp.GetString();
                if (string.Equals(finishReason, "MAX_TOKENS", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Câu trả lời của Gemini bị cắt ngang giữa chừng do chạm ngưỡng giới hạn token (finishReason = MAX_TOKENS).");
                }
            }

            if (firstCandidate.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0)
            {
                var sb = new StringBuilder();
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textProp))
                    {
                        sb.Append(textProp.GetString());
                    }
                }
                return sb.ToString();
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
    
    private static readonly JsonSerializerOptions PromptJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private sealed record DiagramNode(string Id, string Label, string Style, string? Parent,
        string? Source, string? Target, bool IsVertex, bool IsEdge);

    private static string SummarizeDiagramXml(string xml)
    {
        const int maxChars = 30000;
        try
        {
            var doc = XDocument.Parse(xml);
            var nodes = doc.Descendants().Select(ToDiagramNode).Where(n => n is not null).Select(n => n!).ToList();
            if (nodes.Count == 0)
                return "[XML không đọc được cấu trúc, xem hình ảnh]";

            var vertices = nodes.Where(n => n.IsVertex).GroupBy(n => n.Id).ToDictionary(g => g.Key, g => g.First());
            var edges = nodes.Where(n => n.IsEdge).ToList();
            var edgeIds = edges.Select(e => e.Id).ToHashSet();
            // text boxes attached to an edge (multiplicity labels such as "1", "0..*")
            var edgeLabels = vertices.Values
                .Where(v => v.Parent is not null && edgeIds.Contains(v.Parent) && v.Label.Length > 0)
                .ToLookup(v => v.Parent!, v => OneLine(v.Label));

            var sb = new StringBuilder();
            sb.AppendLine("OBJECTS:");
            foreach (var v in vertices.Values.Where(v => v.Parent is null || !edgeIds.Contains(v.Parent)))
                sb.AppendLine($"- [{v.Id}] {OneLine(v.Label)} | {StyleHints(v.Style)}");

            sb.AppendLine("CONNECTIONS:");
            foreach (var e in edges)
            {
                var src = e.Source is not null && vertices.TryGetValue(e.Source, out var s) ? FirstLine(s.Label) : "(unattached)";
                var dst = e.Target is not null && vertices.TryGetValue(e.Target, out var t) ? FirstLine(t.Label) : "(unattached)";
                var labels = new List<string>();
                if (e.Label.Length > 0) labels.Add(OneLine(e.Label));
                labels.AddRange(edgeLabels[e.Id]);
                sb.AppendLine($"- {src} -> {dst} | labels: [{string.Join(", ", labels)}] | {StyleHints(e.Style)}");
            }

            var text = sb.ToString();
            return text.Length > maxChars ? text[..maxChars] + "\n...[đã cắt bớt]" : text;
        }
        catch (System.Xml.XmlException)
        {
            // truncated or malformed XML: give the model the raw text instead
            return "[XML không phân tích được, đưa nguyên bản]\n" + (xml.Length > 20000 ? xml[..20000] + "...[cắt]" : xml);
        }
    }

    private static DiagramNode? ToDiagramNode(XElement e)
    {
        var name = e.Name.LocalName;
        XElement? cell;
        string? label;
        if (name is "object" or "UserObject") { cell = e.Element("mxCell"); label = (string?)e.Attribute("label"); }
        else if (name == "mxCell")
        {
            if (e.Parent?.Name.LocalName is "object" or "UserObject") return null;
            cell = e; label = (string?)e.Attribute("value");
        }
        else return null;

        var id = (string?)e.Attribute("id");
        if (cell is null || id is null) return null;

        return new DiagramNode(id, HtmlToText(label), (string?)cell.Attribute("style") ?? string.Empty,
            (string?)cell.Attribute("parent"), (string?)cell.Attribute("source"), (string?)cell.Attribute("target"),
            (string?)cell.Attribute("vertex") == "1", (string?)cell.Attribute("edge") == "1");
    }

    private static string HtmlToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var text = Regex.Replace(html, @"<hr[^>]*>", "\n---\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<br\s*/?>|</p>|</div>|</li>|</tr>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", string.Empty);
        text = System.Net.WebUtility.HtmlDecode(text).Replace('\u00A0', ' ');
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\s*\n\s*", "\n");
        return text.Trim();
    }

    private static string OneLine(string s) => s.Replace("\n", " / ");
    private static string FirstLine(string s) => s.Split('\n')[0];

    private static string StyleHints(string style)
    {
        var keep = new HashSet<string> { "shape", "endArrow", "startArrow", "endFill", "startFill", "dashed" };
        return string.Join(";", style.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !p.Contains('=') || keep.Contains(p[..p.IndexOf('=')])));
    }
}
