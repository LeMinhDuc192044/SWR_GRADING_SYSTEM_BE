using Application.DTOs.Semesters;
using FluentValidation;
using System.Text.RegularExpressions;

namespace Application.Validators.Semesters;

public sealed class UpdateSemesterRequestValidator : AbstractValidator<UpdateSemesterRequest>
{
    public UpdateSemesterRequestValidator()
    {
        RuleFor(request => request.SemesterCode)
            .Must(code => string.IsNullOrWhiteSpace(code) || IsValidCode(code))
            .WithMessage("Semester code must use the format SP##, SU##, or FA##.");

        RuleFor(request => request)
            .Must(request => request.SemesterCode is null
                || request.StartDate is null
                || HasMatchingYear(request.SemesterCode, request.StartDate.Value))
            .WithMessage("Semester code year must match the semester start date year.");

        RuleFor(request => request.Name)
            .Must(name => name is null || !string.IsNullOrWhiteSpace(name))
            .WithMessage("Semester name is required.");

        RuleFor(request => request)
            .Must(request => request.StartDate is null
                || request.EndDate is null
                || request.EndDate.Value >= request.StartDate.Value)
            .WithMessage("End date must be on or after start date.");

        RuleFor(request => request.Status)
            .IsInEnum()
            .When(request => request.Status.HasValue)
            .WithMessage("Invalid semester status.");
    }

    private static bool IsValidCode(string code)
        => Regex.IsMatch(code.Trim(), "^(SP|SU|FA)\\d{2}$", RegexOptions.CultureInvariant);

    private static bool HasMatchingYear(string code, DateOnly startDate)
        => IsValidCode(code)
            && int.TryParse(code.Trim()[^2..], out var codeYear)
            && codeYear == startDate.Year % 100;
}
