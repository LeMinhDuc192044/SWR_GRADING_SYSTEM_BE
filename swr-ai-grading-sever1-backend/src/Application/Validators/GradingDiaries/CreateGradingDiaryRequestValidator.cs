using Application.DTOs.GradingDiaries;
using FluentValidation;

namespace Application.Validators.GradingDiaries;

public sealed class CreateGradingDiaryRequestValidator : AbstractValidator<CreateGradingDiaryRequest>
{
    public CreateGradingDiaryRequestValidator()
    {
        RuleFor(x => x.PaperSetId)
            .NotEmpty()
            .WithMessage("Đề thi (PaperSetId) là bắt buộc.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Tên sổ chấm không được để trống.")
            .MaximumLength(200)
            .WithMessage("Tên sổ chấm không được vượt quá 200 ký tự.");

        RuleFor(x => x.Content)
            .MaximumLength(21)
            .WithMessage("Nội dung (Content) không được vượt quá 21 ký tự.");
    }
}
