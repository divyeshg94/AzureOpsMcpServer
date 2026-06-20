namespace AzureOpsMcpServer.Models;

public record DeploymentEntry
{
    public required string Name { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required int DurationSeconds { get; init; }
    public required string TriggeredBy { get; init; }
}

public record DeploymentStatusResult
{
    public required string ResourceGroup { get; init; }
    public required List<DeploymentEntry> Deployments { get; init; }
    public required string Summary { get; init; }
}

public record ResourceTagsResult
{
    public required string ResourceId { get; init; }
    public required Dictionary<string, string> Tags { get; init; }
    public required List<string> MissingRequiredTags { get; init; }
    public required bool Compliant { get; init; }
    public required string ComplianceSummary { get; init; }
}

public record ResourceHealthResult
{
    public required string ResourceType { get; init; }
    public required string ResourceName { get; init; }
    public required string HealthState { get; init; }
    public string? ReasonCode { get; init; }
    public string? RecommendedAction { get; init; }
    public required DateTimeOffset LastChecked { get; init; }
}