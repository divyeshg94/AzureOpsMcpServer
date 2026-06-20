using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.ResourceHealth;
using System.ComponentModel;
using ModelContextProtocol.Server;
using AzureOpsMcpServer.Models;

namespace AzureOpsMcpServer.Tools;

[McpServerToolType]
public class AzureOpsTools(ArmClient armClient)
{
    private readonly ArmClient _arm = armClient;
    // ─────────────────────────────────────────────────────────────────
    // TOOL 1: Deployment Status
    // ─────────────────────────────────────────────────────────────────
    [McpServerTool(Name = "get_deployment_status")]
    [Description(
        "Returns recent deployment history for an Azure resource group. " +
        "Includes deployment name, provisioning status (Succeeded/Failed/Running), " +
        "timestamp, duration, and correlation ID for tracing. " +
        "Use this to understand what changed in an environment before an incident.")]
    public async Task<DeploymentStatusResult> GetDeploymentStatusAsync(
        [Description("Azure subscription ID (GUID format)")] string subscriptionId,
        [Description("Resource group name to query")] string resourceGroup,
        [Description("Number of recent deployments to return (1–20)")] int count = 5)
    {
        var subscription = _arm.GetSubscriptionResource(
            SubscriptionResource.CreateResourceIdentifier(subscriptionId));
        var rg = await subscription.GetResourceGroupAsync(resourceGroup);
        var deployments = rg.Value.GetArmDeployments();
        var results = new List<DeploymentEntry>();
        await foreach (var d in deployments.GetAllAsync())
        {
            if (results.Count >= Math.Min(count, 20)) break;
            results.Add(new DeploymentEntry
            {
                Name            = d.Data.Name,
                Status          = d.Data.Properties.ProvisioningState?.ToString() ?? "Unknown",
                Timestamp       = d.Data.Properties.Timestamp ?? DateTimeOffset.MinValue,
                DurationSeconds = (int)(d.Data.Properties.Duration?.TotalSeconds ?? 0),
                // SystemData.CreatedBy is populated for deployments triggered via portal/CLI/CI
                // Falls back to CorrelationId for programmatic deployments
                TriggeredBy     = d.Data.SystemData?.CreatedBy
                                  ?? d.Data.Properties.CorrelationId?.ToString()
                                  ?? "unknown"
            });
        }
        var latest = results.FirstOrDefault();
        return new DeploymentStatusResult
        {
            ResourceGroup = resourceGroup,
            Deployments   = results,
            Summary       = latest is not null
                ? $"Last deployment '{latest.Name}' {latest.Status} at {latest.Timestamp:u}"
                : "No deployments found in this resource group"
        };
    }

    // ─────────────────────────────────────────────────────────────────
    // TOOL 2: Resource Tags
    // ─────────────────────────────────────────────────────────────────
    [McpServerTool(Name = "get_resource_tags")]
    [Description(
        "Fetches all tags on an Azure resource group and identifies missing required FinOps tags. " +
        "Required tags: Environment, Owner, CostCenter, Application. " +
        "Do NOT use this tool for individual resources - it operates at the resource group level.")]
    public async Task<ResourceTagsResult> GetResourceTagsAsync(
        [Description("Azure subscription ID (GUID format)")] string subscriptionId,
        [Description("Resource group name to check")] string resourceGroup)
    {
        var subscription = _arm.GetSubscriptionResource(
            SubscriptionResource.CreateResourceIdentifier(subscriptionId));
        var rg = await subscription.GetResourceGroupAsync(resourceGroup);
        var tags = rg.Value.Data.Tags ?? new Dictionary<string, string>();
        var required = new[] { "Environment", "Owner", "CostCenter", "Application" };
        var missingTags = required.Except(tags.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        return new ResourceTagsResult
        {
            ResourceId        = rg.Value.Data.Id.ToString(),
            Tags              = new Dictionary<string, string>(tags),
            MissingRequiredTags = missingTags,
            Compliant         = missingTags.Count == 0,
            ComplianceSummary = missingTags.Count == 0
                ? "Resource group is fully tagged"
                : $"Missing {missingTags.Count} required tag(s): {string.Join(", ", missingTags)}"
        };
    }

    // ─────────────────────────────────────────────────────────────────
    // TOOL 3: Resource Health
    // ─────────────────────────────────────────────────────────────────
    [McpServerTool(Name = "check_resource_health")]
    [Description(
        "Returns the current health state of an Azure resource group from Azure Resource Health. " +
        "Health states: Available, Degraded, Unavailable, Unknown. " +
        "Use this to determine whether degradation is platform-initiated or application-caused.")]
    public async Task<ResourceHealthResult> CheckResourceHealthAsync(
        [Description("Azure subscription ID (GUID format)")] string subscriptionId,
        [Description("Resource group name to check")] string resourceGroup)
    {
        var subscription = _arm.GetSubscriptionResource(
            SubscriptionResource.CreateResourceIdentifier(subscriptionId));
        var rg = await subscription.GetResourceGroupAsync(resourceGroup);
        // GetAvailabilityStatusAsync is an extension method from Azure.ResourceManager.ResourceHealth
        // It returns the current availability status for any ARM resource scope
        var statusResponse = await _arm.GetAvailabilityStatusAsync(rg.Value.Id);
        var data = statusResponse.Value;
        return new ResourceHealthResult
        {
            ResourceType      = "ResourceGroup",
            ResourceName      = resourceGroup,
            HealthState       = data.Properties?.AvailabilityState?.ToString() ?? "Unknown",
            ReasonCode        = data.Properties?.ReasonType,
            RecommendedAction = data.Properties?.RecommendedActions
                                    ?.FirstOrDefault()?.Action,
            LastChecked       = DateTimeOffset.UtcNow
        };
    }
}