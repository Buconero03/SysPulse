namespace ProcessLens.Core;

public sealed record ProcessInsight
{
    public required string FriendlyName { get; init; }
    public required string Category { get; init; }
    public required string OwnerApp { get; init; }
    public required string Summary { get; init; }
    public required string Role { get; init; }
    public required string WhatIsIt { get; init; }
    public required string WhyRunning { get; init; }
    public required string CloseImpact { get; init; }
    public required string CloseRisk { get; init; }
    public required string DetectionLabel { get; init; }
    public required string DetectionBasis { get; init; }
    public required int ConfidencePercent { get; init; }
    public string Framework { get; init; } = "Non determinato";
    public int IdentityConfidencePercent { get; init; } = 0;
    public int OwnershipConfidencePercent { get; init; } = 0;
    public int RoleConfidencePercent { get; init; } = 0;
    public string IdentityConfidenceLabel => ProcessTerminology.EvidenceStrength(IdentityConfidencePercent);
    public string OwnershipConfidenceLabel => ProcessTerminology.EvidenceStrength(OwnershipConfidencePercent);
    public string RoleConfidenceLabel => ProcessTerminology.EvidenceStrength(RoleConfidencePercent);

    public string? ProjectRoot { get; init; }
    public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();
}
