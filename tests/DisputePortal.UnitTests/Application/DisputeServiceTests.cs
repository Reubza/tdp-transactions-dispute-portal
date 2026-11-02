using DisputePortal.Application.Common;
using DisputePortal.Application.Disputes;
using DisputePortal.Application.Transactions;
using DisputePortal.Domain.Common;
using DisputePortal.Domain.Disputes;
using DisputePortal.Domain.Transactions;
using DisputePortal.Domain.Users;
using FluentValidation;

namespace DisputePortal.UnitTests.Application;

public sealed class DisputeServiceTests : IDisposable
{
    private readonly ServiceTestContext _ctx = new();
    private readonly User _customer = TestData.Customer("Thandi");
    private readonly User _otherCustomer = TestData.Customer("James");
    private readonly User _agent = new(Guid.NewGuid(), "Agent", "agent@example.com", UserRole.Agent);
    private Transaction _transaction = null!;
    private Transaction _otherCustomersTransaction = null!;

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public async Task Create_raises_dispute_for_own_transaction()
    {
        await SeedAsync();

        var (dispute, created) = await _ctx.Disputes().CreateAsync(Request(), null, default);

        Assert.True(created);
        Assert.Equal(DisputeStatus.Submitted, dispute.Summary.Status);
        Assert.Equal(_transaction.Id, dispute.Summary.TransactionId);
        Assert.Equal("Thandi", dispute.Summary.CustomerName);
        Assert.Equal([DisputeActions.Withdraw], dispute.AvailableActions);
        Assert.Single(dispute.History);
    }

    [Fact]
    public async Task Create_for_another_customers_transaction_is_not_found()
    {
        await SeedAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _ctx.Disputes().CreateAsync(Request(_otherCustomersTransaction.Id), null, default));
    }

    [Fact]
    public async Task Create_rejects_second_open_dispute_on_same_transaction()
    {
        await SeedAsync();
        await _ctx.Disputes().CreateAsync(Request(), null, default);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _ctx.Disputes().CreateAsync(Request(), null, default));

        Assert.Equal(DisputeErrorCodes.AlreadyDisputed, ex.Code);
    }

    [Fact]
    public async Task Create_allows_new_dispute_after_previous_one_was_withdrawn()
    {
        await SeedAsync();
        var (first, _) = await _ctx.Disputes().CreateAsync(Request(), null, default);
        await _ctx.Disputes().WithdrawAsync(first.Summary.Id, new WithdrawDisputeRequest(null), default);

        var (second, created) = await _ctx.Disputes().CreateAsync(Request(), null, default);

        Assert.True(created);
        Assert.NotEqual(first.Summary.Id, second.Summary.Id);
    }

    [Fact]
    public async Task Create_with_same_idempotency_key_returns_original_dispute()
    {
        await SeedAsync();
        var (first, _) = await _ctx.Disputes().CreateAsync(Request(), "retry-key", default);

        var (second, created) = await _ctx.Disputes().CreateAsync(Request(), "retry-key", default);

        Assert.False(created);
        Assert.Equal(first.Summary.Id, second.Summary.Id);
    }

    [Fact]
    public async Task Create_rejects_idempotency_key_reused_for_different_transaction()
    {
        await SeedAsync();
        Transaction another = null!;
        await _ctx.SeedAsync(db =>
        {
            another = TestData.Transaction(_transaction.AccountId);
            db.Transactions.Add(another);
        });
        await _ctx.Disputes().CreateAsync(Request(), "retry-key", default);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _ctx.Disputes().CreateAsync(Request(another.Id), "retry-key", default));

        Assert.Equal(DisputeService.IdempotencyKeyReusedCode, ex.Code);
    }

    [Fact]
    public async Task Create_validates_request()
    {
        await SeedAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _ctx.Disputes().CreateAsync(new CreateDisputeRequest(_transaction.Id, (DisputeReason)42, "short"), null, default));

        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(CreateDisputeRequest.Reason));
        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(CreateDisputeRequest.Description));
    }

    [Fact]
    public async Task List_returns_only_own_disputes_and_filters_by_open_state()
    {
        await SeedAsync();
        var (mine, _) = await _ctx.Disputes().CreateAsync(Request(), null, default);
        await _ctx.Disputes().WithdrawAsync(mine.Summary.Id, new WithdrawDisputeRequest(null), default);
        await _ctx.Disputes().CreateAsync(Request(), null, default);

        _ctx.CurrentUser.SignInAs(_otherCustomer);
        await _ctx.Disputes().CreateAsync(Request(_otherCustomersTransaction.Id), null, default);
        _ctx.CurrentUser.SignInAs(_customer);

        var all = await _ctx.Disputes().ListAsync(new DisputeQuery(), default);
        var closed = await _ctx.Disputes().ListAsync(new DisputeQuery { Open = false }, default);

        Assert.Equal(2, all.TotalCount);
        Assert.All(all.Items, d => Assert.Equal("Thandi", d.CustomerName));
        Assert.Equal(DisputeStatus.Withdrawn, Assert.Single(closed.Items).Status);
    }

    [Fact]
    public async Task Get_another_customers_dispute_is_not_found()
    {
        await SeedAsync();
        _ctx.CurrentUser.SignInAs(_otherCustomer);
        var (theirs, _) = await _ctx.Disputes().CreateAsync(Request(_otherCustomersTransaction.Id), null, default);
        _ctx.CurrentUser.SignInAs(_customer);

        await Assert.ThrowsAsync<NotFoundException>(() => _ctx.Disputes().GetAsync(theirs.Summary.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _ctx.Disputes().WithdrawAsync(theirs.Summary.Id, new WithdrawDisputeRequest(null), default));
    }

    [Fact]
    public async Task Agent_workflow_is_reflected_in_customer_history()
    {
        await SeedAsync();
        var (dispute, _) = await _ctx.Disputes().CreateAsync(Request(), null, default);

        _ctx.CurrentUser.SignInAs(_agent);
        _ctx.Clock.Advance(TimeSpan.FromHours(2));
        var reviewing = await _ctx.AgentDisputes().UpdateStatusAsync(
            dispute.Summary.Id, new UpdateDisputeStatusRequest(AgentDisputeAction.StartReview, null), default);
        Assert.Equal([DisputeActions.Uphold, DisputeActions.Reject], reviewing.AvailableActions);

        _ctx.Clock.Advance(TimeSpan.FromDays(1));
        await _ctx.AgentDisputes().UpdateStatusAsync(
            dispute.Summary.Id, new UpdateDisputeStatusRequest(AgentDisputeAction.Reject, "Card was present"), default);

        _ctx.CurrentUser.SignInAs(_customer);
        var detail = await _ctx.Disputes().GetAsync(dispute.Summary.Id, default);

        Assert.Equal(DisputeStatus.Rejected, detail.Summary.Status);
        Assert.Equal("Card was present", detail.ResolutionNote);
        Assert.Empty(detail.AvailableActions);
        Assert.Equal(
            [DisputeStatus.Submitted, DisputeStatus.UnderReview, DisputeStatus.Rejected],
            detail.History.Select(h => h.ToStatus));
    }

    [Fact]
    public async Task Agent_must_supply_note_to_resolve()
    {
        await SeedAsync();
        var (dispute, _) = await _ctx.Disputes().CreateAsync(Request(), null, default);
        _ctx.CurrentUser.SignInAs(_agent);

        await Assert.ThrowsAsync<ValidationException>(() => _ctx.AgentDisputes().UpdateStatusAsync(
            dispute.Summary.Id, new UpdateDisputeStatusRequest(AgentDisputeAction.Uphold, null), default));
    }

    [Fact]
    public async Task Transactions_report_dispute_state()
    {
        await SeedAsync();
        Transaction old = null!, credit = null!;
        await _ctx.SeedAsync(db =>
        {
            old = TestData.Transaction(_transaction.AccountId, age: TimeSpan.FromDays(200), merchant: "Old shop");
            credit = TestData.Transaction(_transaction.AccountId, TransactionDirection.Credit, merchant: "Salary");
            db.Transactions.AddRange(old, credit);
        });
        var (dispute, _) = await _ctx.Disputes().CreateAsync(Request(), null, default);

        var page = await _ctx.Transactions().ListAsync(new TransactionQuery(), default);
        var byId = page.Items.ToDictionary(t => t.Id);

        Assert.Equal(3, page.TotalCount); // other customer's transaction is excluded
        Assert.False(byId[_transaction.Id].CanDispute);
        Assert.Equal(dispute.Summary.Id, byId[_transaction.Id].LatestDisputeId);
        Assert.Equal(DisputeStatus.Submitted, byId[_transaction.Id].LatestDisputeStatus);
        Assert.False(byId[old.Id].CanDispute);
        Assert.Contains("120 days", byId[old.Id].NotDisputableReason, StringComparison.Ordinal);
        Assert.False(byId[credit.Id].CanDispute);

        var disputed = await _ctx.Transactions().ListAsync(new TransactionQuery { Disputed = true }, default);
        Assert.Equal(_transaction.Id, Assert.Single(disputed.Items).Id);

        var search = await _ctx.Transactions().ListAsync(new TransactionQuery { Search = "OLD" }, default);
        Assert.Equal(old.Id, Assert.Single(search.Items).Id);
    }

    private CreateDisputeRequest Request(Guid? transactionId = null) =>
        new(transactionId ?? _transaction.Id, DisputeReason.Unauthorised, "I did not make this purchase.");

    private async Task SeedAsync()
    {
        await _ctx.SeedAsync(db =>
        {
            var account = TestData.AccountFor(_customer);
            var otherAccount = TestData.AccountFor(_otherCustomer);
            _transaction = TestData.Transaction(account.Id);
            _otherCustomersTransaction = TestData.Transaction(otherAccount.Id);
            db.Users.AddRange(_customer, _otherCustomer, _agent);
            db.Accounts.AddRange(account, otherAccount);
            db.Transactions.AddRange(_transaction, _otherCustomersTransaction);
        });
        _ctx.CurrentUser.SignInAs(_customer);
    }
}
