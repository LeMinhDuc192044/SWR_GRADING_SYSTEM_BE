using Application.DTOs.Semesters;
using FluentValidation;
using System.Text.RegularExpressions;

namespace Application.Validators.Semesters;

public sealed class CreateSemesterRequestValidator : AbstractValidator<CreateSemesterRequest>
{
    public CreateSemesterRequestValidator()
    {
        RuleFor(request => request.SemesterCode)
            .NotEmpty()
            .Matches("^(SP|SU|FA)\\d{2}$")
            .WithMessage("Semester code must use the format SP##, SU##, or FA##.");

        RuleFor(request => request.SemesterCode)
            .Must((request, code) => HasMatchingYear(code, request.StartDate))
            .WithMessage("Semester code year must match the semester start date year.");

        RuleFor(request => request.Name)
            .NotEmpty()
            .WithMessage("Semester name is required.");

        RuleFor(request => request.StartDate)
            .NotEmpty();

        RuleFor(request => request.EndDate)
            .GreaterThanOrEqualTo(request => request.StartDate)
            .WithMessage("End date must be on or after start date.");

        RuleFor(request => request.Status)
            .IsInEnum()
            .WithMessage("Invalid semester status.");
    }

    private static bool HasMatchingYear(string code, DateOnly startDate)
    {
        if (!Regex.IsMatch(code.Trim(), "^(SP|SU|FA)\\d{2}$", RegexOptions.CultureInvariant))
            return false;

        return int.TryParse(code[^2..], out var codeYear)
            && codeYear == startDate.Year % 100;
    }
}
