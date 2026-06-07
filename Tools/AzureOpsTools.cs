using System.ComponentModel;
using AzureOpsMcpServer.Models;
using ModelContextProtocol.Server;

namespace AzureOpsMcpServer.Tools;

[McpServerToolType]
public static class AzureOpsTools
{
    [McpServerTool(Name = "get_deployment_status")]
    [Description("Returns recent deployment history for an Azure resource group. " +
                 "Includes deployment name, status (Succeeded/Failed/Running), " +
                 "timestamp, and duration in seconds. Use this to understand " +
                 "what changed recently before an incident.")]
    public static DeploymentStatusResult GetDeploymentStatus(
        [Description("The Azure resource group name to query")] string resourceGroup,
        [Description("Number of recent deployments to return (max 20)")] int count = 5)
    {
        // In Part 3 we'll wire this to the real Azure Management SDK.
        // For local testing, we return realistic simulated data.
        var deployments = Enumerable.Range(1, Math.Min(count, 20))
            .Select(i => new DeploymentEntry
            {
                Name = $"deploy-payments-v1.{10 + i}",
                Status = i == 1 ? "Failed" : "Succeeded",
                Timestamp = DateTimeOffset.UtcNow.AddHours(-i * 2),
                DurationSeconds = Random.Shared.Next(45, 320),
                DeployedBy = i % 2 == 0 ? "github-actions[bot]" : "divyesh@contoso.com"
            }).ToList();

        return new DeploymentStatusResult
        {
            ResourceGroup = resourceGroup,
            Deployments = deployments,
            Summary = $"Last deployment '{deployments[0].Name}' {deployments[0].Status} " +
                      $"at {deployments[0].Timestamp:u}"
        };
    }

    [McpServerTool(Name = "get_resource_tags")]
    [Description("Fetches all tags on an Azure resource and identifies any missing " +
                 "required tags for FinOps and governance compliance. Required tags: " +
                 "Environment, Owner, CostCenter, Application.")]
    public static ResourceTagsResult GetResourceTags(
        [Description("Full Azure resource ID (e.g. /subscriptions/{sub}/resourceGroups/{rg}/providers/...)")]
        string resourceId)
    {
        var requiredTags = new[] { "Environment", "Owner", "CostCenter", "Application" };

        // Simulated: in production this calls Azure Resource Manager via SDK
        var presentTags = new Dictionary<string, string>
        {
            { "Environment", "Production" },
            { "Application", "payments-service" },
            { "Owner", "platform-team@contoso.com" }
            // Note: CostCenter intentionally missing - realistic scenario
        };

        var missingTags = requiredTags.Except(presentTags.Keys).ToList();

        return new ResourceTagsResult
        {
            ResourceId = resourceId,
            Tags = presentTags,
            MissingRequiredTags = missingTags,
            Compliant = missingTags.Count == 0,
            ComplianceSummary = missingTags.Count == 0
                ? "Resource is fully tagged"
                : $"Missing {missingTags.Count} required tag(s): {string.Join(", ", missingTags)}"
        };
    }

    [McpServerTool(Name = "check_resource_health")]
    [Description("Returns the current health state of an Azure resource. " +
                 "Health states: Available, Degraded, Unavailable, Unknown. " +
                 "Also returns the reason code and recommended action if unhealthy.")]
    public static ResourceHealthResult CheckResourceHealth(
        [Description("The resource type (e.g. 'AppService', 'CosmosDB', 'ServiceBus')")]
        string resourceType,
        [Description("The resource name as it appears in the Azure portal")]
        string resourceName)
    {
        // Simulated health response - Part 3 wires to Azure Resource Health API
        var isHealthy = !resourceName.Contains("payments", StringComparison.OrdinalIgnoreCase)
                        || Random.Shared.Next(0, 3) != 0;

        return new ResourceHealthResult
        {
            ResourceType = resourceType,
            ResourceName = resourceName,
            HealthState = isHealthy ? "Available" : "Degraded",
            ReasonCode = isHealthy ? null : "PlatformInitiated",
            RecommendedAction = isHealthy ? null :
                "Check recent deployments and review Application Insights live metrics. " +
                "If degradation persists > 15 minutes, escalate to platform team.",
            LastChecked = DateTimeOffset.UtcNow
        };
    }
}