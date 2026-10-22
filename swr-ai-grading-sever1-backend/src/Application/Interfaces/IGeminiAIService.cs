using Application.DTOs.GradingDiaries;
using Domain.Entities;

namespace Application.Interfaces;

public interface IGeminiAIService
{
    Task<string> ExtractTextFromDocxAsync(Stream docxStream, CancellationToken ct = default);
    Task<DocxExtractedContentDto> ExtractContentFromDocxAsync(Stream docxStream, CancellationToken ct = default);
    Task<string> ExtractTextFromXlsxAsync(Stream xlsxStream, CancellationToken ct = default);
    Task<IReadOnlyList<ExtractedQuestionAnswerDto>> ExtractAnswersForQuestionsAsync(
        string studentSubmissionText,
        IReadOnlyList<Question> questions,
        List<DocxImageDto>? images = null,
        CancellationToken ct = default);
    Task<GradingResultDto> GradeSubmissionAnswersAsync(
        IReadOnlyList<(Question Question, SubmissionParsedAnswer ParsedAnswer)> questionAnswers,
        string rubricContent,
        List<DocxImageDto>? images = null,
        CancellationToken ct = default);
}

