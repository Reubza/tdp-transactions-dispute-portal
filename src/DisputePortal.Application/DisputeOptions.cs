using System.ComponentModel.DataAnnotations;

namespace DisputePortal.Application;

public sealed class DisputeOptions
{
    public const string SectionName = "Disputes";

    /// <summary>How many days after posting a transaction may still be disputed.</summary>
    [Range(1, 3650)]
    public int DisputeWindowDays { get; set; } = 120;

    public TimeSpan DisputeWindow => TimeSpan.FromDays(DisputeWindowDays);
}
