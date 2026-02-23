using FluentValidation;
using LexCore.Application.DTOs.Billing;

namespace LexCore.Application.Validators;

public class CreateInvoiceRequestValidator : AbstractValidator<CreateInvoiceRequest>
{
    public CreateInvoiceRequestValidator()
    {
        RuleFor(x => x.CaseId)
            .NotEmpty().WithMessage("Case ID is required");

        RuleFor(x => x.ClientId)
            .NotEmpty().WithMessage("Client ID is required");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero");

        RuleFor(x => x.DueDate)
            .GreaterThan(DateTime.UtcNow.Date)
            .When(x => x.DueDate.HasValue)
            .WithMessage("Due date must be in the future");
    }
}
