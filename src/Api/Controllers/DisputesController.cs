using Api.Common;
using Application.Common;
using Application.Disputes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/disputes")]
[Authorize(Policy = AuthPolicies.Customer)]
[Produces("application/json")]
public sealed class DisputesController(DisputeService disputes) : ControllerBase
{
    /// <summary>
    /// Raises a dispute against one of the customer's transactions. Supply an <c>Idempotency-Key</c> header to make
    /// retries safe: repeating a request with the same key returns the original dispute with 200 OK.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<DisputeDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<DisputeDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DisputeDetailDto>> Create(
        CreateDisputeRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var (dispute, created) = await disputes.CreateAsync(request, idempotencyKey, cancellationToken);
        return created
            ? CreatedAtAction(nameof(Get), new { id = dispute.Summary.Id }, dispute)
            : Ok(dispute);
    }

    /// <summary>The customer's dispute history, newest first.</summary>
    [HttpGet]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<DisputeSummaryDto>> List([FromQuery] DisputeQuery query, CancellationToken cancellationToken) =>
        disputes.ListAsync(query, cancellationToken);

    /// <summary>A dispute with its full status timeline.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<DisputeDetailDto> Get(Guid id, CancellationToken cancellationToken) =>
        disputes.GetAsync(id, cancellationToken);

    [HttpPost("{id:guid}/withdraw")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<DisputeDetailDto> Withdraw(Guid id, WithdrawDisputeRequest request, CancellationToken cancellationToken) =>
        disputes.WithdrawAsync(id, request, cancellationToken);
}
