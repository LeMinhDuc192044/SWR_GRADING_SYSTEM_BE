using AI_Assisted_SWR_Grading_System.Domain.Common;
using FluentValidation;
using System.Text.RegularExpressions;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Commands.CreateSemester;

public class CreateSemesterCommandValidator : AbstractValidator<CreateSemesterCommand>
{
    private static readonly Regex SemesterCodePattern = new(
        "^(SP|SU|FA)[0-9]{2}$",
        RegexOptions.Compiled);

    public CreateSemesterCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100);

        RuleFor(x => x.StartDate)
            .NotEmpty();

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date.");

        RuleFor(x => x.Status)
            .IsInEnum();

        RuleFor(x => x.SemesterCode)
            .NotEmpty().WithMessage("Semester code is required.")
            .MaximumLength(20)
            .Matches(SemesterCodePattern)
            .WithMessage("Semester code must start with SP, SU, or FA and end with 2 digits (e.g. SP26, SU26, FA26).");
    }
}