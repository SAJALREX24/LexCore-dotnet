using FluentValidation;
using LexCore.Application.DTOs;

namespace LexCore.Application.Validators;

public class ChatRequestValidator : AbstractValidator<ChatRequest>
{
    public ChatRequestValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required")
            .MaximumLength(2000).WithMessage(
                "Message cannot exceed 2000 characters");

        RuleFor(x => x.Language)
            .Must(l => l == "hi" || l == "en" || l == "hinglish")
            .When(x => !string.IsNullOrEmpty(x.Language))
            .WithMessage("Language must be hi, en, or hinglish");
    }
}

public class DraftRequestValidator : AbstractValidator<DraftRequest>
{
    public DraftRequestValidator()
    {
        RuleFor(x => x.DocumentType)
            .NotEmpty().WithMessage("Document type is required")
            .MaximumLength(100).WithMessage(
                "Document type cannot exceed 100 characters");

        RuleFor(x => x.Instructions)
            .MaximumLength(1000).WithMessage(
                "Instructions cannot exceed 1000 characters")
            .When(x => x.Instructions != null);
    }
}

public class ResearchRequestValidator : AbstractValidator<ResearchRequest>
{
    public ResearchRequestValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("Query is required")
            .MaximumLength(1000).WithMessage(
                "Query cannot exceed 1000 characters");

        RuleFor(x => x.IpcSections)
            .MaximumLength(500).WithMessage(
                "IPC sections cannot exceed 500 characters")
            .When(x => x.IpcSections != null);
    }
}
