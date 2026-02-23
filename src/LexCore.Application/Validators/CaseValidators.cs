using FluentValidation;
using LexCore.Application.DTOs.Cases;

namespace LexCore.Application.Validators;

public class CreateCaseRequestValidator : AbstractValidator<CreateCaseRequest>
{
    public CreateCaseRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Case title is required")
            .MaximumLength(500).WithMessage("Title cannot exceed 500 characters");

        RuleFor(x => x.CaseType)
            .MaximumLength(100).When(x => !string.IsNullOrEmpty(x.CaseType))
            .WithMessage("Case type cannot exceed 100 characters");

        RuleFor(x => x.CourtName)
            .MaximumLength(200).When(x => !string.IsNullOrEmpty(x.CourtName))
            .WithMessage("Court name cannot exceed 200 characters");
    }
}

public class UpdateCaseRequestValidator : AbstractValidator<UpdateCaseRequest>
{
    public UpdateCaseRequestValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Title))
            .WithMessage("Title cannot exceed 500 characters");

        RuleFor(x => x.CaseType)
            .MaximumLength(100).When(x => !string.IsNullOrEmpty(x.CaseType))
            .WithMessage("Case type cannot exceed 100 characters");

        RuleFor(x => x.CourtName)
            .MaximumLength(200).When(x => !string.IsNullOrEmpty(x.CourtName))
            .WithMessage("Court name cannot exceed 200 characters");
    }
}
