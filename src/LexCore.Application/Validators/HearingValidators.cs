using FluentValidation;
using LexCore.Application.DTOs.Hearings;

namespace LexCore.Application.Validators;

public class CreateHearingRequestValidator : AbstractValidator<CreateHearingRequest>
{
    public CreateHearingRequestValidator()
    {
        RuleFor(x => x.CaseId)
            .NotEmpty().WithMessage("Case ID is required");

        RuleFor(x => x.HearingDate)
            .NotEmpty().WithMessage("Hearing date is required")
            .GreaterThan(DateTime.UtcNow.Date).WithMessage("Hearing date must be in the future");

        RuleFor(x => x.HearingTime)
            .NotEmpty().WithMessage("Hearing time is required");

        RuleFor(x => x.CourtName)
            .MaximumLength(200).When(x => !string.IsNullOrEmpty(x.CourtName))
            .WithMessage("Court name cannot exceed 200 characters");

        RuleFor(x => x.JudgeName)
            .MaximumLength(200).When(x => !string.IsNullOrEmpty(x.JudgeName))
            .WithMessage("Judge name cannot exceed 200 characters");
    }
}

public class UpdateHearingRequestValidator : AbstractValidator<UpdateHearingRequest>
{
    public UpdateHearingRequestValidator()
    {
        RuleFor(x => x.HearingDate)
            .GreaterThan(DateTime.UtcNow.Date)
            .When(x => x.HearingDate.HasValue)
            .WithMessage("Hearing date must be in the future");

        RuleFor(x => x.CourtName)
            .MaximumLength(200).When(x => !string.IsNullOrEmpty(x.CourtName))
            .WithMessage("Court name cannot exceed 200 characters");

        RuleFor(x => x.JudgeName)
            .MaximumLength(200).When(x => !string.IsNullOrEmpty(x.JudgeName))
            .WithMessage("Judge name cannot exceed 200 characters");
    }
}
