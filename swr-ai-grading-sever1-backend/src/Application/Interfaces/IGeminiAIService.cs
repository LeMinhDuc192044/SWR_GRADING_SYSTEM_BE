using Application.DTOs.GradingDiaries;

namespace Application.Interfaces;

public interface IGeminiAIService
{
    Task<string> ExtractTextFromDocxAsync(Stream docxStream, CancellationToken ct = default);
    Task<GradingResultDto> GradeSubmissionAsync(string studentSubmissionText, string rubricContent, CancellationToken ct = default);
}
