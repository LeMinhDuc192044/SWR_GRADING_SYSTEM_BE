using Application.DTOs.Examinations;
using FluentValidation;

namespace Application.Validators.Examinations;

public sealed class UpdateExaminationRequestValidator : AbstractValidator<UpdateExaminationRequest>
{
    public UpdateExaminationRequestValidator()
    {
        RuleFor(request => request.Name)
            .Must(name => name is null || !string.IsNullOrWhiteSpace(name))
            .WithMessage("Examination name is required.");

        RuleFor(request => request.ExaminationType)
            .IsInEnum()
            .When(request => request.ExaminationType.HasValue)
            .WithMessage("Invalid examination type.");

        RuleFor(request => request.StartDate)
            .Must(date => date is null || date.Value != default)
            .WithMessage("Examination start date is required.");

        RuleFor(request => request.EndDate)
            .Must((request, endDate) => endDate is null || endDate.Value != default)
            .WithMessage("Examination end date is required.");

        RuleFor(request => request.Status)
            .IsInEnum()
            .When(request => request.Status.HasValue)
            .WithMessage("Invalid examination status.");
    }
}
