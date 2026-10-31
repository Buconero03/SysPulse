namespace ProcessLens.Semantics;

internal sealed record IdentityAssessment(string FriendlyName, int Confidence, string Evidence);
internal sealed record FrameworkAssessment(string Name, int Confidence, string Evidence);
internal sealed record OwnershipAssessment(string Owner, int Confidence, string Evidence);
internal sealed record RoleAssessment(
    string Role,
    string Summary,
    string Meaning,
    string WhyRunning,
    string CloseImpact,
    string CloseRisk,
    string Category,
    int Confidence,
    string Evidence);
