using Application.DTOs.GradingDiaries;
using FluentValidation;

namespace Application.Validators.GradingDiaries;

public sealed class UpdateGradingDiaryRequestValidator : AbstractValidator<UpdateGradingDiaryRequest>
{
    public UpdateGradingDiaryRequestValidator()
    {
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
