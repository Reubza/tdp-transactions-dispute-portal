using DisputePortal.Api.Infrastructure;
using DisputePortal.Application.Common;
using DisputePortal.Application.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisputePortal.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize(Policy = AuthPolicies.Customer)]
[Produces("application/json")]
public sealed class TransactionsController(TransactionService transactions) : ControllerBase
{
    /// <summary>Lists the signed-in customer's accounts.</summary>
    [HttpGet("accounts")]
    public Task<IReadOnlyList<AccountDto>> ListAccounts(CancellationToken cancellationToken) =>
        transactions.ListAccountsAsync(cancellationToken);

    /// <summary>Lists the signed-in customer's transactions, newest first.</summary>
    [HttpGet("transactions")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<TransactionDto>> List([FromQuery] TransactionQuery query, CancellationToken cancellationToken) =>
        transactions.ListAsync(query, cancellationToken);

    [HttpGet("transactions/{id:guid}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<TransactionDto> Get(Guid id, CancellationToken cancellationToken) =>
        transactions.GetAsync(id, cancellationToken);
}
