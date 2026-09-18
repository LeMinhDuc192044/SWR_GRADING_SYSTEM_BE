using Application.DTOs.PaperSets;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Application.Validators.PaperSets;

public sealed class CreatePaperSetRequestValidator : AbstractValidator<CreatePaperSetRequest>
{
    public CreatePaperSetRequestValidator()
    {
        RuleFor(request => request.Question)
            .Must(file => HasNonEmptyContent(file))
            .When(request => request.Question is not null)
            .WithMessage("Question file must not be empty.");

        RuleFor(request => request.Question)
            .Must(file => HasExtension(file, ".docx", ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".pdf"))
            .When(request => request.Question is not null)
            .WithMessage("Question file must be a .docx, image, or PDF file.");

        RuleFor(request => request.AnswerRubric)
            .Must(file => HasNonEmptyContent(file))
            .When(request => request.AnswerRubric is not null)
            .WithMessage("Answer rubric file must not be empty.");

        RuleFor(request => request.AnswerRubric)
            .Must(file => HasExtension(file, ".xls", ".xlsx"))
            .When(request => request.AnswerRubric is not null)
            .WithMessage("Answer rubric file must be an Excel spreadsheet (.xls or .xlsx).");

        RuleFor(request => request.AnswerTemplate)
            .Must(file => HasNonEmptyContent(file))
            .When(request => request.AnswerTemplate is not null)
            .WithMessage("Answer template file must not be empty.");

        RuleFor(request => request.AnswerTemplate)
            .Must(file => HasExtension(file, ".doc", ".docx"))
            .When(request => request.AnswerTemplate is not null)
            .WithMessage("Answer template file must be a Word document (.doc or .docx).");
    }

    private static bool HasNonEmptyContent(IFormFile? file) => file is not null && file.Length > 0;

    private static bool HasExtension(IFormFile? file, params string[] extensions)
    {
        if (file is null || string.IsNullOrWhiteSpace(file.FileName)) return false;

        var extension = Path.GetExtension(file.FileName);
        return extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
