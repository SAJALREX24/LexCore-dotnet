using FluentValidation;
using LexCore.Application.DTOs.Cases;

namespace LexCore.Application.Validators;

public class CreateCaseRequestValidator : AbstractValidator<CreateCaseRequest>
{
    public CreateCaseRequestValidator()
    {
        // ── Core ────────────────────────────────────────────────
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Case title is required")
            .MaximumLength(500).WithMessage("Title cannot exceed 500 characters");

        RuleFor(x => x.CaseType)
            .MaximumLength(100).When(x => !string.IsNullOrEmpty(x.CaseType))
            .WithMessage("Case type cannot exceed 100 characters");

        RuleFor(x => x.CourtName)
            .MaximumLength(200).When(x => !string.IsNullOrEmpty(x.CourtName))
            .WithMessage("Court name cannot exceed 200 characters");

        // ── Client phone — 10-digit Indian mobile ───────────────
        RuleFor(x => x.ClientPhone)
            .Matches(@"^[6-9]\d{9}$")
            .When(x => !string.IsNullOrEmpty(x.ClientPhone))
            .WithMessage("Client phone must be a valid 10-digit Indian mobile number starting with 6-9");

        RuleFor(x => x.ClientWhatsApp)
            .Matches(@"^[6-9]\d{9}$")
            .When(x => !string.IsNullOrEmpty(x.ClientWhatsApp))
            .WithMessage("WhatsApp number must be a valid 10-digit Indian mobile number starting with 6-9");

        // ── Client age ──────────────────────────────────────────
        RuleFor(x => x.ClientAge)
            .InclusiveBetween(1, 120)
            .When(x => x.ClientAge.HasValue)
            .WithMessage("Client age must be between 1 and 120");

        // ── Fee validation ──────────────────────────────────────
        RuleFor(x => x.AgreedFees)
            .GreaterThan(0)
            .When(x => x.AgreedFees.HasValue)
            .WithMessage("Total fee must be greater than zero");

        RuleFor(x => x.AdvancePaid)
            .GreaterThanOrEqualTo(0)
            .When(x => x.AdvancePaid.HasValue)
            .WithMessage("Advance paid cannot be negative");

        RuleFor(x => x.AdvancePaid)
            .Must((request, advance) => advance <= request.AgreedFees)
            .When(x => x.AdvancePaid.HasValue && x.AgreedFees.HasValue)
            .WithMessage("Advance paid cannot exceed total agreed fees");

        // ── Date validation ─────────────────────────────────────
        RuleFor(x => x.LimitationDate)
            .GreaterThan(x => x.FiledDate!.Value)
            .When(x => x.LimitationDate.HasValue && x.FiledDate.HasValue)
            .WithMessage("Limitation date must be after filing date");

        // ── Counsel phone ───────────────────────────────────────
        RuleFor(x => x.OppositeCounselPhone)
            .Matches(@"^[6-9]\d{9}$")
            .When(x => !string.IsNullOrEmpty(x.OppositeCounselPhone))
            .WithMessage("Counsel phone must be a valid 10-digit Indian mobile number starting with 6-9");

        // ── String length limits ────────────────────────────────
        RuleFor(x => x.ClientName)
            .MaximumLength(200).When(x => !string.IsNullOrEmpty(x.ClientName));

        RuleFor(x => x.ClientAddress)
            .MaximumLength(1000).When(x => !string.IsNullOrEmpty(x.ClientAddress));

        RuleFor(x => x.NatureOfOffence)
            .MaximumLength(500).When(x => !string.IsNullOrEmpty(x.NatureOfOffence));

        RuleFor(x => x.OppositeCounselName)
            .MaximumLength(200).When(x => !string.IsNullOrEmpty(x.OppositeCounselName));
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
