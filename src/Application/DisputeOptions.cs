using System.ComponentModel.DataAnnotations;
using Domain.Disputes;

namespace Application;

public sealed class DisputeOptions
{
    public const string SectionName = "Disputes";

    /// <summary>How many days after posting a transaction may still be disputed.</summary>
    [Range(1, 3650)]
    public int DisputeWindowDays { get; set; } = 120;

    /// <summary>How many days after a dispute is rejected the customer may appeal it (once).</summary>
    [Range(1, 365)]
    public int AppealWindowDays { get; set; } = 30;

    public TimeSpan DisputeWindow => TimeSpan.FromDays(DisputeWindowDays);

    public DisputeRules Rules => new(DisputeWindow, TimeSpan.FromDays(AppealWindowDays));
}
