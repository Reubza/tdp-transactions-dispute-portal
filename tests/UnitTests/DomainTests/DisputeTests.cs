using Domain.Common;
using Domain.Disputes;
using Domain.Transactions;
using Domain.Users;

namespace UnitTests.DomainTests;

public class DisputeTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid AgentId = Guid.NewGuid();

    [Fact]
    public void Raise_creates_submitted_dispute_with_initial_history_entry()
    {
        var tx = TestData.Transaction(Guid.NewGuid());

        var dispute = Dispute.Raise(tx, CustomerId, DisputeReason.Duplicate, "  Charged twice  ", "key-1", TestData.Now, TestData.Window);

        Assert.Equal(DisputeStatus.Submitted, dispute.Status);
        Assert.Equal(tx.Id, dispute.TransactionId);
        Assert.Equal("Charged twice", dispute.Description);
        Assert.Equal("key-1", dispute.IdempotencyKey);
        Assert.True(dispute.IsOpen);
        var entry = Assert.Single(dispute.History);
        Assert.Null(entry.FromStatus);
        Assert.Equal(DisputeStatus.Submitted, entry.ToStatus);
        Assert.Equal(UserRole.Customer, entry.ChangedByRole);
        Assert.Equal(TestData.Now, entry.ChangedAt);
    }

    [Theory]
    [InlineData(TransactionDirection.Credit, TransactionStatus.Posted, 3, DisputeErrorCodes.NotADebit)]
    [InlineData(TransactionDirection.Debit, TransactionStatus.Pending, 0, DisputeErrorCodes.NotPosted)]
    [InlineData(TransactionDirection.Debit, TransactionStatus.Reversed, 3, DisputeErrorCodes.NotPosted)]
    [InlineData(TransactionDirection.Debit, TransactionStatus.Posted, 121, DisputeErrorCodes.WindowExpired)]
    public void Raise_rejects_transactions_that_are_not_disputable(
        TransactionDirection direction, TransactionStatus status, int ageDays, string expectedCode)
    {
        var tx = TestData.Transaction(Guid.NewGuid(), direction, status, TimeSpan.FromDays(ageDays));

        var ex = Assert.Throws<DomainException>(() =>
            Dispute.Raise(tx, CustomerId, DisputeReason.Other, "Something went wrong", null, TestData.Now, TestData.Window));

        Assert.Equal(expectedCode, ex.Code);
    }

    [Fact]
    public void Transaction_on_the_last_day_of_the_window_is_disputable()
    {
        var tx = TestData.Transaction(Guid.NewGuid(), age: TestData.Window);

        Assert.Null(tx.CheckDisputable(TestData.Now, TestData.Window));
    }

    [Fact]
    public void Full_lifecycle_records_every_transition_in_order()
    {
        var dispute = NewDispute();

        dispute.StartReview(AgentId, "Looking into it", TestData.Now.AddHours(1));
        dispute.Resolve(AgentId, upheld: true, "Refunded", TestData.Now.AddDays(2));

        Assert.Equal(DisputeStatus.Upheld, dispute.Status);
        Assert.False(dispute.IsOpen);
        Assert.Equal(TestData.Now.AddDays(2), dispute.ResolvedAt);
        Assert.Equal("Refunded", dispute.ResolutionNote);
        Assert.Equal(
            [DisputeStatus.Submitted, DisputeStatus.UnderReview, DisputeStatus.Upheld],
            dispute.History.Select(h => h.ToStatus));
        Assert.Equal(DisputeStatus.UnderReview, dispute.History[2].FromStatus);
        Assert.Equal(UserRole.Agent, dispute.History[2].ChangedByRole);
    }

    [Fact]
    public void Cannot_resolve_without_review()
    {
        var dispute = NewDispute();

        var ex = Assert.Throws<DomainException>(() => dispute.Resolve(AgentId, upheld: false, "No", TestData.Now));

        Assert.Equal(DisputeErrorCodes.InvalidTransition, ex.Code);
        Assert.Equal(DisputeStatus.Submitted, dispute.Status);
        Assert.Single(dispute.History);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Cannot_change_a_resolved_dispute(bool upheld)
    {
        var dispute = NewDispute();
        dispute.StartReview(AgentId, null, TestData.Now);
        dispute.Resolve(AgentId, upheld, "Done", TestData.Now);

        Assert.Throws<DomainException>(() => dispute.Withdraw(CustomerId, null, TestData.Now));
        Assert.Throws<DomainException>(() => dispute.StartReview(AgentId, null, TestData.Now));
    }

    [Fact]
    public void Customer_can_withdraw_an_open_dispute()
    {
        var dispute = NewDispute();
        dispute.StartReview(AgentId, null, TestData.Now);

        dispute.Withdraw(CustomerId, "Merchant refunded me", TestData.Now.AddDays(1));

        Assert.Equal(DisputeStatus.Withdrawn, dispute.Status);
        Assert.Null(dispute.ResolvedAt);
        Assert.Equal("Merchant refunded me", dispute.History[^1].Note);
    }

    [Fact]
    public void Another_customer_cannot_withdraw_the_dispute()
    {
        var dispute = NewDispute();

        Assert.Throws<InvalidOperationException>(() => dispute.Withdraw(Guid.NewGuid(), null, TestData.Now));
    }

    [Fact]
    public void Resolution_requires_a_note()
    {
        var dispute = NewDispute();
        dispute.StartReview(AgentId, null, TestData.Now);

        Assert.Throws<ArgumentException>(() => dispute.Resolve(AgentId, upheld: true, "  ", TestData.Now));
        Assert.Equal(DisputeStatus.UnderReview, dispute.Status);
    }

    private static Dispute NewDispute() =>
        Dispute.Raise(
            TestData.Transaction(Guid.NewGuid()),
            CustomerId,
            DisputeReason.Unauthorised,
            "I did not make this purchase",
            null,
            TestData.Now,
            TestData.Window);
}
