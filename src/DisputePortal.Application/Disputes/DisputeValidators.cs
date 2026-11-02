using DisputePortal.Application.Common;
using DisputePortal.Domain.Disputes;
using FluentValidation;

namespace DisputePortal.Application.Disputes;

public sealed class CreateDisputeRequestValidator : AbstractValidator<CreateDisputeRequest>
{
    public const int DescriptionMinLength = 10;

    public CreateDisputeRequestValidator()
    {
        RuleFor(r => r.TransactionId).NotEmpty();
        RuleFor(r => r.Reason).IsInEnum();
        RuleFor(r => r.Description)
            .NotEmpty()
            .MaximumLength(Dispute.DescriptionMaxLength)
            .Must(d => d is not null && d.Trim().Length >= DescriptionMinLength)
            .WithMessage($"Please describe the problem in at least {DescriptionMinLength} characters.");
    }
}

public sealed class WithdrawDisputeRequestValidator : AbstractValidator<WithdrawDisputeRequest>
{
    public WithdrawDisputeRequestValidator()
    {
        RuleFor(r => r.Note).MaximumLength(Dispute.NoteMaxLength);
    }
}

public sealed class UpdateDisputeStatusRequestValidator : AbstractValidator<UpdateDisputeStatusRequest>
{
    public UpdateDisputeStatusRequestValidator()
    {
        RuleFor(r => r.Action).IsInEnum();
        RuleFor(r => r.Note).MaximumLength(Dispute.NoteMaxLength);
        RuleFor(r => r.Note)
            .NotEmpty()
            .When(r => r.Action is AgentDisputeAction.Uphold or AgentDisputeAction.Reject)
            .WithMessage("A resolution note is required when resolving a dispute.");
    }
}

public sealed class DisputeQueryValidator : AbstractValidator<DisputeQuery>
{
    public DisputeQueryValidator()
    {
        this.ApplyPagingRules();
        RuleFor(q => q.Status).IsInEnum();
        RuleFor(q => q.Search).MaximumLength(100);
    }
}
