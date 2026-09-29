using Application.DTOs.GradingDiaries;

namespace Application.Interfaces;

public interface IGeminiAIService
{
    Task<string> ExtractTextFromDocxAsync(Stream docxStream, CancellationToken ct = default);
    Task<DocxExtractedContentDto> ExtractContentFromDocxAsync(Stream docxStream, CancellationToken ct = default);
    Task<GradingResultDto> GradeSubmissionAsync(string studentSubmissionText, string rubricContent, List<DocxImageDto>? images = null, CancellationToken ct = default);
}
