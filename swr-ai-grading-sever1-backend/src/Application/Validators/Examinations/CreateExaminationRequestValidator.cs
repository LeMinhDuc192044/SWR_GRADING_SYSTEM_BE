using Application.DTOs.Examinations;
using FluentValidation;

namespace Application.Validators.Examinations;

public sealed class CreateExaminationRequestValidator : AbstractValidator<CreateExaminationRequest>
{
    public CreateExaminationRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .WithMessage("Examination name is required.");

        RuleFor(request => request.ExaminationType)
            .IsInEnum()
            .WithMessage("Invalid examination type.");

        RuleFor(request => request.StartDate)
            .NotEmpty()
            .WithMessage("Examination start date is required.");

        RuleFor(request => request.EndDate)
            .Must((request, endDate) => endDate == default || endDate >= request.StartDate)
            .WithMessage("Examination end date must be on or after start date.");

        RuleFor(request => request.DurationMinutes)
            .GreaterThan(0)
            .WithMessage("Duration must be greater than 0.");

        RuleFor(request => request.BeforeTimeMinutes)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Before-time minutes cannot be negative.");

        RuleFor(request => request.Status)
            .IsInEnum()
            .WithMessage("Invalid examination status.");

        RuleFor(request => request.SemesterId)
            .NotEmpty()
            .WithMessage("Semester is required.");
    }
}
