using Domain.Common;
using Domain.Disputes;
using Domain.Transactions;

namespace UnitTests.DomainTests;

public class DisputeEligibilityTests
{
    private static readonly DateTimeOffset DecidedAt = TestData.Now.AddDays(-5);

    [Fact]
    public void First_dispute_within_window_is_allowed_and_is_not_an_appeal()
    {
        var result = Check([]);

        Assert.True(result.IsEligible);
        Assert.False(result.IsAppeal);
    }

    [Fact]
    public void Withdrawn_disputes_are_ignored()
    {
        var result = Check([Prior(DisputeStatus.Withdrawn), Prior(DisputeStatus.Withdrawn)]);

        Assert.True(result.IsEligible);
        Assert.False(result.IsAppeal);
    }

    [Theory]
    [InlineData(DisputeStatus.Submitted)]
    [InlineData(DisputeStatus.UnderReview)]
    public void Open_dispute_blocks_another(DisputeStatus status) =>
        AssertRefused(Check([Prior(status)]), DisputeErrorCodes.AlreadyDisputed);

    [Fact]
    public void Upheld_dispute_blocks_any_further_dispute_to_prevent_a_double_refund() =>
        AssertRefused(Check([Prior(DisputeStatus.Upheld)]), DisputeErrorCodes.AlreadyResolved);

    [Fact]
    public void Upheld_appeal_also_blocks_further_disputes()
    {
        var original = Prior(DisputeStatus.Rejected);

        AssertRefused(
            Check([original, Prior(DisputeStatus.Upheld, appealOf: original.Id)]), DisputeErrorCodes.AlreadyResolved);
    }

    [Fact]
    public void Rejected_dispute_can_be_appealed_once_and_the_appeal_links_to_it()
    {
        var original = Prior(DisputeStatus.Rejected);

        var result = Check([original]);

        Assert.True(result.IsEligible);
        Assert.Equal(original.Id, result.AppealOfDisputeId);
    }

    [Fact]
    public void Appeal_ignores_the_posting_window_but_respects_the_appeal_window()
    {
        var oldTransaction = TestData.Transaction(Guid.NewGuid(), age: TestData.Window + TimeSpan.FromDays(10));

        var justInTime = Prior(DisputeStatus.Rejected, resolvedAt: TestData.Now - TestData.AppealWindow);
        Assert.True(DisputeEligibility.Check(oldTransaction, [justInTime], TestData.Now, TestData.Rules).IsAppeal);

        var tooLate = Prior(
            DisputeStatus.Rejected, resolvedAt: TestData.Now - TestData.AppealWindow - TimeSpan.FromMinutes(1));
        AssertRefused(
            DisputeEligibility.Check(oldTransaction, [tooLate], TestData.Now, TestData.Rules),
            DisputeErrorCodes.AppealWindowExpired);
    }

    [Fact]
    public void Withdrawn_appeal_can_be_raised_again()
    {
        var original = Prior(DisputeStatus.Rejected);

        var result = Check([original, Prior(DisputeStatus.Withdrawn, appealOf: original.Id)]);

        Assert.Equal(original.Id, result.AppealOfDisputeId);
    }

    [Fact]
    public void Rejected_appeal_is_final()
    {
        var original = Prior(DisputeStatus.Rejected);

        AssertRefused(
            Check([original, Prior(DisputeStatus.Rejected, appealOf: original.Id)]), DisputeErrorCodes.AppealExhausted);
    }

    [Fact]
    public void Transaction_type_rules_still_apply_to_appeals()
    {
        var credit = TestData.Transaction(Guid.NewGuid(), TransactionDirection.Credit);

        AssertRefused(
            DisputeEligibility.Check(credit, [Prior(DisputeStatus.Rejected)], TestData.Now, TestData.Rules),
            DisputeErrorCodes.NotADebit);
    }

    [Fact]
    public void Raise_records_the_appeal_link()
    {
        var tx = TestData.Transaction(Guid.NewGuid());
        var original = Prior(DisputeStatus.Rejected);

        var appeal = Dispute.Raise(
            tx, [original], Guid.NewGuid(), DisputeReason.Unauthorised, "New evidence attached", null,
            TestData.Now, TestData.Rules);

        Assert.True(appeal.IsAppeal);
        Assert.Equal(original.Id, appeal.AppealOfDisputeId);
    }

    [Fact]
    public void Raise_refuses_after_upheld()
    {
        var tx = TestData.Transaction(Guid.NewGuid());

        var ex = Assert.Throws<DomainException>(() => Dispute.Raise(
            tx, [Prior(DisputeStatus.Upheld)], Guid.NewGuid(), DisputeReason.Duplicate, "Charged twice again", null,
            TestData.Now, TestData.Rules));

        Assert.Equal(DisputeErrorCodes.AlreadyResolved, ex.Code);
    }

    private static EligibilityResult Check(IEnumerable<PriorDispute> prior) =>
        DisputeEligibility.Check(TestData.Transaction(Guid.NewGuid()), prior, TestData.Now, TestData.Rules);

    private static PriorDispute Prior(DisputeStatus status, Guid? appealOf = null, DateTimeOffset? resolvedAt = null) =>
        new(
            Guid.NewGuid(),
            status,
            status is DisputeStatus.Upheld or DisputeStatus.Rejected ? resolvedAt ?? DecidedAt : null,
            appealOf);

    private static void AssertRefused(EligibilityResult result, string expectedCode)
    {
        Assert.False(result.IsEligible);
        Assert.Equal(expectedCode, result.Error!.Code);
    }
}
