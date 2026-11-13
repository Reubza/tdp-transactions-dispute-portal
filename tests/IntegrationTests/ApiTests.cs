using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Auth;
using Application.Common;
using Application.Disputes;
using Application.Transactions;
using Domain.Common;
using Domain.Disputes;
using Domain.Transactions;
using Domain.Users;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IntegrationTests.DisputePortalApiFactory;

namespace IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class ApiTests(DisputePortalApiFactory factory)
{
    [Fact]
    public async Task Health_endpoints_report_healthy()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Login_returns_token_and_profile()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest("Thandi@Example.com", DemoPassword), Json);

        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        Assert.False(string.IsNullOrEmpty(login!.AccessToken));
        Assert.Equal(UserRole.Customer, login.User.Role);
        Assert.Equal("Thandi Nkosi", login.User.FullName);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorised_problem()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(DemoDataSeeder.CustomerEmail, "wrong-password"), Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Api_requires_authentication_and_correct_role()
    {
        var anonymous = factory.CreateClient();
        var agent = await factory.CreateClientForAsync(DemoDataSeeder.AgentEmail);
        var customer = await factory.CreateClientForAsync(DemoDataSeeder.CustomerEmail);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/transactions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync("/api/v1/transactions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/agent/disputes")).StatusCode);
    }

    [Fact]
    public async Task Responses_carry_security_headers_and_correlation_id()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-ID", "test-correlation-id");

        var response = await client.SendAsync(request);

        Assert.Equal("test-correlation-id", response.Headers.GetValues("X-Correlation-ID").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task Customer_sees_only_their_own_transactions()
    {
        var thandi = await factory.CreateClientForAsync(DemoDataSeeder.CustomerEmail);
        var james = await factory.CreateClientForAsync(DemoDataSeeder.SecondCustomerEmail);

        var thandiAccounts = await thandi.GetFromJsonAsync<List<AccountDto>>("/api/v1/accounts", Json);
        var jamesPage = await james.GetFromJsonAsync<PagedResult<TransactionDto>>("/api/v1/transactions?pageSize=100", Json);
        var thandiPage = await thandi.GetFromJsonAsync<PagedResult<TransactionDto>>("/api/v1/transactions?page=1&pageSize=10", Json);

        Assert.Equal(2, thandiAccounts!.Count);
        Assert.All(thandiAccounts, a => Assert.StartsWith("****", a.MaskedAccountNumber, StringComparison.Ordinal));
        Assert.Equal(10, thandiPage!.Items.Count);
        Assert.True(thandiPage.TotalCount > 10);
        Assert.Equal(thandiPage.Items.OrderByDescending(t => t.PostedAt).Select(t => t.Id), thandiPage.Items.Select(t => t.Id));
        Assert.DoesNotContain(jamesPage!.Items, t => thandiAccounts.Any(a => a.Id == t.AccountId));

        var jamesTransactionId = jamesPage.Items[0].Id;
        Assert.Equal(HttpStatusCode.NotFound, (await thandi.GetAsync($"/api/v1/transactions/{jamesTransactionId}")).StatusCode);
    }

    [Fact]
    public async Task Invalid_query_is_a_validation_problem()
    {
        var client = await factory.CreateClientForAsync(DemoDataSeeder.CustomerEmail);

        var response = await client.GetAsync("/api/v1/transactions?pageSize=1000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(problem.GetProperty("errors").TryGetProperty("PageSize", out _));
    }

    [Fact]
    public async Task Customer_can_raise_dispute_and_see_it_in_history()
    {
        var client = await factory.CreateClientForAsync(DemoDataSeeder.CustomerEmail);
        var transaction = await FindDisputableTransactionAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/disputes", NewDispute(transaction.Id), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<DisputeDetailDto>(Json);
        Assert.Equal(DisputeStatus.Submitted, created!.Summary.Status);
        Assert.EndsWith($"/api/v1/disputes/{created.Summary.Id}", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        var history = await client.GetFromJsonAsync<PagedResult<DisputeSummaryDto>>("/api/v1/disputes?open=true", Json);
        Assert.Contains(history!.Items, d => d.Id == created.Summary.Id);

        var updated = await client.GetFromJsonAsync<TransactionDto>($"/api/v1/transactions/{transaction.Id}", Json);
        Assert.False(updated!.CanDispute);
        Assert.Equal(created.Summary.Id, updated.LatestDisputeId);

        var duplicate = await client.PostAsJsonAsync("/api/v1/disputes", NewDispute(transaction.Id), Json);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(DisputeErrorCodes.AlreadyDisputed, await ProblemCodeAsync(duplicate));
    }

    [Fact]
    public async Task Repeating_create_with_same_idempotency_key_returns_original()
    {
        var client = await factory.CreateClientForAsync(DemoDataSeeder.CustomerEmail);
        var transaction = await FindDisputableTransactionAsync(client);
        var key = Guid.NewGuid().ToString();

        var first = await PostWithKeyAsync(client, NewDispute(transaction.Id), key);
        var second = await PostWithKeyAsync(client, NewDispute(transaction.Id), key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var a = await first.Content.ReadFromJsonAsync<DisputeDetailDto>(Json);
        var b = await second.Content.ReadFromJsonAsync<DisputeDetailDto>(Json);
        Assert.Equal(a!.Summary.Id, b!.Summary.Id);
    }

    [Fact]
    public async Task Invalid_dispute_requests_are_rejected()
    {
        var client = await factory.CreateClientForAsync(DemoDataSeeder.CustomerEmail);
        var transaction = await FindDisputableTransactionAsync(client);

        var tooShort = await client.PostAsJsonAsync("/api/v1/disputes", new { transactionId = transaction.Id, reason = "Duplicate", description = "bad" }, Json);
        var unknownReason = await client.PostAsJsonAsync("/api/v1/disputes", new { transactionId = transaction.Id, reason = "Bored", description = "A long enough description" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownReason.StatusCode);
    }

    [Fact]
    public async Task Transactions_outside_the_dispute_window_cannot_be_disputed()
    {
        var client = await factory.CreateClientForAsync(DemoDataSeeder.CustomerEmail);
        var page = await client.GetFromJsonAsync<PagedResult<TransactionDto>>(
            $"/api/v1/transactions?pageSize=100&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-130).ToString("O"))}", Json);
        var old = page!.Items.First(t => t.Direction == TransactionDirection.Debit && t.LatestDisputeId is null);

        var response = await client.PostAsJsonAsync("/api/v1/disputes", NewDispute(old.Id), Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(DisputeErrorCodes.WindowExpired, await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task Other_customers_cannot_see_or_withdraw_a_dispute()
    {
        var thandi = await factory.CreateClientForAsync(DemoDataSeeder.CustomerEmail);
        var james = await factory.CreateClientForAsync(DemoDataSeeder.SecondCustomerEmail);
        var dispute = await RaiseAsync(thandi);

        Assert.Equal(HttpStatusCode.NotFound, (await james.GetAsync($"/api/v1/disputes/{dispute.Summary.Id}")).StatusCode);
        var withdraw = await james.PostAsJsonAsync($"/api/v1/disputes/{dispute.Summary.Id}/withdraw", new WithdrawDisputeRequest(null), Json);
        Assert.Equal(HttpStatusCode.NotFound, withdraw.StatusCode);
    }

    [Fact]
    public async Task Customer_can_withdraw_an_open_dispute_only_once()
    {
        var client = await factory.CreateClientForAsync(DemoDataSeeder.CustomerEmail);
        var dispute = await RaiseAsync(client);

        var first = await client.PostAsJsonAsync($"/api/v1/disputes/{dispute.Summary.Id}/withdraw", new WithdrawDisputeRequest("Sorted with merchant"), Json);
        var second = await client.PostAsJsonAsync($"/api/v1/disputes/{dispute.Summary.Id}/withdraw", new WithdrawDisputeRequest(null), Json);

        first.EnsureSuccessStatusCode();
        var withdrawn = await first.Content.ReadFromJsonAsync<DisputeDetailDto>(Json);
        Assert.Equal(DisputeStatus.Withdrawn, withdrawn!.Summary.Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        Assert.Equal(DisputeErrorCodes.InvalidTransition, await ProblemCodeAsync(second));
    }

    [Fact]
    public async Task Agent_resolution_appears_in_customer_timeline()
    {
        var customer = await factory.CreateClientForAsync(DemoDataSeeder.CustomerEmail);
        var agent = await factory.CreateClientForAsync(DemoDataSeeder.AgentEmail);
        var dispute = await RaiseAsync(customer);

        var queue = await agent.GetFromJsonAsync<PagedResult<DisputeSummaryDto>>("/api/v1/agent/disputes?status=Submitted&pageSize=100", Json);
        Assert.Contains(queue!.Items, d => d.Id == dispute.Summary.Id && d.CustomerName == "Thandi Nkosi");

        var review = await agent.PostAsJsonAsync($"/api/v1/agent/disputes/{dispute.Summary.Id}/status", new UpdateDisputeStatusRequest(AgentDisputeAction.StartReview, null), Json);
        var noNote = await agent.PostAsJsonAsync($"/api/v1/agent/disputes/{dispute.Summary.Id}/status", new UpdateDisputeStatusRequest(AgentDisputeAction.Uphold, null), Json);
        var uphold = await agent.PostAsJsonAsync($"/api/v1/agent/disputes/{dispute.Summary.Id}/status", new UpdateDisputeStatusRequest(AgentDisputeAction.Uphold, "Refund issued"), Json);

        review.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, noNote.StatusCode);
        uphold.EnsureSuccessStatusCode();

        var detail = await customer.GetFromJsonAsync<DisputeDetailDto>($"/api/v1/disputes/{dispute.Summary.Id}", Json);
        Assert.Equal(DisputeStatus.Upheld, detail!.Summary.Status);
        Assert.Equal("Refund issued", detail.ResolutionNote);
        Assert.NotNull(detail.Summary.ResolvedAt);
        Assert.Empty(detail.AvailableActions);
        Assert.Equal(
            [DisputeStatus.Submitted, DisputeStatus.UnderReview, DisputeStatus.Upheld],
            detail.History.Select(h => h.ToStatus));
        Assert.Equal([UserRole.Customer, UserRole.Agent, UserRole.Agent], detail.History.Select(h => h.ActorRole));
    }

    [Fact]
    public async Task Seeded_history_contains_disputes_in_every_closed_state()
    {
        var client = await factory.CreateClientForAsync(DemoDataSeeder.CustomerEmail);

        var closed = await client.GetFromJsonAsync<PagedResult<DisputeSummaryDto>>("/api/v1/disputes?open=false&pageSize=100", Json);

        var statuses = closed!.Items.Select(d => d.Status).ToHashSet();
        Assert.Contains(DisputeStatus.Upheld, statuses);
        Assert.Contains(DisputeStatus.Rejected, statuses);
        Assert.Contains(DisputeStatus.Withdrawn, statuses);
    }

    [Fact]
    public async Task Upheld_dispute_cannot_be_raised_again()
    {
        var customer = await factory.CreateClientForAsync(DemoDataSeeder.CustomerEmail);
        var agent = await factory.CreateClientForAsync(DemoDataSeeder.AgentEmail);
        var dispute = await RaiseAsync(customer);
        await DecideAsync(agent, dispute.Summary.Id, AgentDisputeAction.Uphold);

        var again = await customer.PostAsJsonAsync("/api/v1/disputes", NewDispute(dispute.Summary.TransactionId), Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);
        Assert.Equal(DisputeErrorCodes.AlreadyResolved, await ProblemCodeAsync(again));
        var transaction = await customer.GetFromJsonAsync<TransactionDto>($"/api/v1/transactions/{dispute.Summary.TransactionId}", Json);
        Assert.False(transaction!.CanDispute);
    }

    [Fact]
    public async Task Rejected_dispute_can_be_appealed_once()
    {
        var customer = await factory.CreateClientForAsync(DemoDataSeeder.CustomerEmail);
        var agent = await factory.CreateClientForAsync(DemoDataSeeder.AgentEmail);
        var original = await RaiseAsync(customer);
        await DecideAsync(agent, original.Summary.Id, AgentDisputeAction.Reject);

        var transaction = await customer.GetFromJsonAsync<TransactionDto>($"/api/v1/transactions/{original.Summary.TransactionId}", Json);
        Assert.True(transaction!.CanDispute);
        Assert.True(transaction.DisputeIsAppeal);

        var appealResponse = await customer.PostAsJsonAsync("/api/v1/disputes", NewDispute(original.Summary.TransactionId), Json);
        Assert.Equal(HttpStatusCode.Created, appealResponse.StatusCode);
        var appeal = await appealResponse.Content.ReadFromJsonAsync<DisputeDetailDto>(Json);
        Assert.Equal(original.Summary.Id, appeal!.Summary.AppealOfDisputeId);

        await DecideAsync(agent, appeal.Summary.Id, AgentDisputeAction.Reject);

        var third = await customer.PostAsJsonAsync("/api/v1/disputes", NewDispute(original.Summary.TransactionId), Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, third.StatusCode);
        Assert.Equal(DisputeErrorCodes.AppealExhausted, await ProblemCodeAsync(third));
    }

    [Fact]
    public async Task Database_allows_only_one_appeal_per_rejected_dispute()
    {
        var customer = await factory.CreateClientForAsync(DemoDataSeeder.CustomerEmail);
        var agent = await factory.CreateClientForAsync(DemoDataSeeder.AgentEmail);
        var original = await RaiseAsync(customer);
        await DecideAsync(agent, original.Summary.Id, AgentDisputeAction.Reject);
        var appealResponse = await customer.PostAsJsonAsync("/api/v1/disputes", NewDispute(original.Summary.TransactionId), Json);
        var appeal = await appealResponse.Content.ReadFromJsonAsync<DisputeDetailDto>(Json);
        await DecideAsync(agent, appeal!.Summary.Id, AgentDisputeAction.Reject);

        // Bypass the application check, as a race would: a second appeal of the same rejected dispute.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tx = await db.Transactions.SingleAsync(t => t.Id == original.Summary.TransactionId);
        var customerId = await db.Disputes.Where(d => d.Id == original.Summary.Id).Select(d => d.CustomerId).SingleAsync();
        var duplicate = Dispute.Raise(
            tx,
            [new PriorDispute(original.Summary.Id, DisputeStatus.Rejected, DateTimeOffset.UtcNow, null)],
            customerId,
            DisputeReason.Unauthorised,
            "Second appeal of the same dispute",
            null,
            DateTimeOffset.UtcNow,
            new DisputeRules(TimeSpan.FromDays(3650), TimeSpan.FromDays(3650)));
        db.Disputes.Add(duplicate);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => db.SaveChangesAsync());
        Assert.Equal(DisputeErrorCodes.AppealExhausted, ex.Code);
    }

    [Fact]
    public async Task Seeded_rejected_dispute_is_open_for_appeal()
    {
        var client = await factory.CreateClientForAsync(DemoDataSeeder.CustomerEmail);

        var page = await client.GetFromJsonAsync<PagedResult<TransactionDto>>("/api/v1/transactions?pageSize=100&disputed=true", Json);

        Assert.Contains(page!.Items, t => t.LatestDisputeStatus == DisputeStatus.Rejected && t.CanDispute && t.DisputeIsAppeal);
        Assert.Contains(page.Items, t => t.LatestDisputeStatus == DisputeStatus.Upheld && !t.CanDispute);
    }

    [Fact]
    public async Task Unknown_api_route_is_not_found()
    {
        var client = await factory.CreateClientForAsync(DemoDataSeeder.CustomerEmail);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/does-not-exist")).StatusCode);
    }

    private static CreateDisputeRequest NewDispute(Guid transactionId) =>
        new(transactionId, DisputeReason.Unauthorised, "I do not recognise this transaction.");

    private static async Task DecideAsync(HttpClient agent, Guid disputeId, AgentDisputeAction decision)
    {
        (await agent.PostAsJsonAsync($"/api/v1/agent/disputes/{disputeId}/status", new UpdateDisputeStatusRequest(AgentDisputeAction.StartReview, null), Json))
            .EnsureSuccessStatusCode();
        (await agent.PostAsJsonAsync($"/api/v1/agent/disputes/{disputeId}/status", new UpdateDisputeStatusRequest(decision, "Decision recorded"), Json))
            .EnsureSuccessStatusCode();
    }

    private static async Task<DisputeDetailDto> RaiseAsync(HttpClient client)
    {
        var transaction = await FindDisputableTransactionAsync(client);
        var response = await client.PostAsJsonAsync("/api/v1/disputes", NewDispute(transaction.Id), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DisputeDetailDto>(Json))!;
    }

    // Tests share one seeded database, so each picks a transaction no other test has disputed yet.
    private static readonly SemaphoreSlim PickLock = new(1, 1);
    private static readonly HashSet<Guid> Picked = [];

    private static async Task<TransactionDto> FindDisputableTransactionAsync(HttpClient client)
    {
        await PickLock.WaitAsync();
        try
        {
            var page = await client.GetFromJsonAsync<PagedResult<TransactionDto>>("/api/v1/transactions?pageSize=100&disputed=false", Json);
            var transaction = page!.Items.First(t => t.CanDispute && !Picked.Contains(t.Id));
            Picked.Add(transaction.Id);
            return transaction;
        }
        finally
        {
            PickLock.Release();
        }
    }

    private static async Task<HttpResponseMessage> PostWithKeyAsync(HttpClient client, CreateDisputeRequest body, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/disputes") { Content = JsonContent.Create(body, options: Json) };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return problem.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
