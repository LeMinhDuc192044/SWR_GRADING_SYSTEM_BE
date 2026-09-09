using FluentValidation;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Commands.UpdateSemester;

public class UpdateSemesterCommandValidator : AbstractValidator<UpdateSemesterCommand>
{
    public UpdateSemesterCommandValidator()
    {
        RuleFor(x => x.SemesterId).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().MaximumLength(100);

        RuleFor(x => x.Code)
            .NotEmpty().MaximumLength(20);

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date.");

        RuleFor(x => x.Status).IsInEnum();
    }
}