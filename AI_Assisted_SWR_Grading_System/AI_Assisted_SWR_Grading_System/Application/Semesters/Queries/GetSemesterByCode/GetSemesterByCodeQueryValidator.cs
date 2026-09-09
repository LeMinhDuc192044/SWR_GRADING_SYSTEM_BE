using FluentValidation;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Queries.GetSemesterByCode;

public class GetSemesterByCodeQueryValidator : AbstractValidator<GetSemesterByCodeQuery>
{
    public GetSemesterByCodeQueryValidator()
    {
        RuleFor(x => x.SemesterCode)
            .NotEmpty().WithMessage("Semester code is required.");
    }
}