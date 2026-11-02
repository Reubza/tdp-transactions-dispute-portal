using DisputePortal.Api.Infrastructure;
using DisputePortal.Application.Common;
using DisputePortal.Application.Disputes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisputePortal.Api.Controllers;

/// <summary>Back-office endpoints for dispute agents.</summary>
[ApiController]
[Route("api/v1/agent/disputes")]
[Authorize(Policy = AuthPolicies.Agent)]
[Produces("application/json")]
public sealed class AgentDisputesController(AgentDisputeService disputes) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<DisputeSummaryDto>> List([FromQuery] DisputeQuery query, CancellationToken cancellationToken) =>
        disputes.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<DisputeDetailDto> Get(Guid id, CancellationToken cancellationToken) =>
        disputes.GetAsync(id, cancellationToken);

    /// <summary>Moves a dispute through its lifecycle: start review, uphold or reject (resolution note required).</summary>
    [HttpPost("{id:guid}/status")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<DisputeDetailDto> UpdateStatus(
        Guid id, UpdateDisputeStatusRequest request, CancellationToken cancellationToken) =>
        disputes.UpdateStatusAsync(id, request, cancellationToken);
}
