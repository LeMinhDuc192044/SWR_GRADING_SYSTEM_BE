using FluentValidation;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Commands.CreateSemester;

public class CreateSemesterCommandValidator : AbstractValidator<CreateSemesterCommand>
{
    public CreateSemesterCommandValidator()
    {
        RuleFor(x => x.SemesterCode)
            .NotEmpty().WithMessage("Semester code is required.")
            .MaximumLength(20);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100);

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(20);

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date.");

        RuleFor(x => x.Status)
            .IsInEnum();
    }
}