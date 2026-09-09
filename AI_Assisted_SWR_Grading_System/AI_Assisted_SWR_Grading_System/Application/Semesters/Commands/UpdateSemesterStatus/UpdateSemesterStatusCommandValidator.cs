using FluentValidation;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Commands.UpdateSemesterStatus;

public class UpdateSemesterStatusCommandValidator : AbstractValidator<UpdateSemesterStatusCommand>
{
    public UpdateSemesterStatusCommandValidator()
    {
        RuleFor(x => x.SemesterId)
            .NotEmpty();

        RuleFor(x => x.Status)
            .IsInEnum();
    }
}