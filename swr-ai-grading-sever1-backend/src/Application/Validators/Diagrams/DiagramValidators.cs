using Application.DTOs.Diagrams;
using FluentValidation;

namespace Application.Validators.Diagrams;

public class CreateDiagramRequestValidator : AbstractValidator<CreateDiagramRequest>
{
    public CreateDiagramRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên diagram không được để trống.")
            .MaximumLength(200).WithMessage("Tên diagram không được vượt quá 200 ký tự.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Mô tả diagram không được vượt quá 2000 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}

public class UpdateDiagramRequestValidator : AbstractValidator<UpdateDiagramRequest>
{
    public UpdateDiagramRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Tên diagram không được vượt quá 200 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Mô tả diagram không được vượt quá 2000 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}

public class CreateDiagramElementRequestValidator : AbstractValidator<CreateDiagramElementRequest>
{
    public CreateDiagramElementRequestValidator()
    {
        RuleFor(x => x.ElementCode)
            .NotEmpty().WithMessage("Mã element không được để trống.")
            .MaximumLength(50).WithMessage("Mã element không được vượt quá 50 ký tự.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên element không được để trống.")
            .MaximumLength(200).WithMessage("Tên element không được vượt quá 200 ký tự.");

        RuleFor(x => x.ShapeType)
            .MaximumLength(100).WithMessage("Shape type không được vượt quá 100 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.ShapeType));

        RuleFor(x => x.ElementReferenceType)
            .MaximumLength(100).WithMessage("Element reference type không được vượt quá 100 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.ElementReferenceType));

        RuleFor(x => x.SoftwareType)
            .MaximumLength(100).WithMessage("Software type không được vượt quá 100 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.SoftwareType));
    }
}

public class UpdateDiagramElementRequestValidator : AbstractValidator<UpdateDiagramElementRequest>
{
    public UpdateDiagramElementRequestValidator()
    {
        RuleFor(x => x.ElementCode)
            .MaximumLength(50).WithMessage("Mã element không được vượt quá 50 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.ElementCode));

        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Tên element không được vượt quá 200 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.ShapeType)
            .MaximumLength(100).WithMessage("Shape type không được vượt quá 100 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.ShapeType));

        RuleFor(x => x.ElementReferenceType)
            .MaximumLength(100).WithMessage("Element reference type không được vượt quá 100 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.ElementReferenceType));

        RuleFor(x => x.SoftwareType)
            .MaximumLength(100).WithMessage("Software type không được vượt quá 100 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.SoftwareType));
    }
}
