using System.ComponentModel;
using ModelContextProtocol.Server;

namespace AzureOpsMcpServer.Prompts;

[McpServerPromptType]
public static class IncidentPrompts
{
    [McpServerPrompt(Name = "incident_triage_prompt", Title = "Azure Incident Triage")]
    [Description("Generates a structured incident triage prompt. " +
                 "Use when an engineer reports a potential incident and needs " +
                 "a systematic investigation starting point.")]
    public static string GetIncidentTriagePrompt(
        [Description("The service or resource experiencing issues")] string serviceName,
        [Description("Brief description of the observed symptoms")] string symptoms,
        [Description("Resource group to check for recent deployments")] string resourceGroup)
    {
        return $"""
            You are an Azure operations assistant. An engineer has reported a potential incident.
            SERVICE: {serviceName}
            SYMPTOMS: {symptoms}
            RESOURCE GROUP: {resourceGroup}

            Please investigate using the following sequence:
            1. Call get_deployment_status for resource group '{resourceGroup}' - look for recent changes
            2. Call check_resource_health for '{serviceName}'
            3. If the service is 'payments' or payment-related, read the runbook at azureops://runbooks/payment-degradation
            4. Call get_resource_tags to verify compliance - untagged resources may indicate shadow infrastructure

            Synthesize findings into:
            - LIKELY CAUSE (based on deployment history and health state)
            - IMMEDIATE ACTIONS (from runbook and health recommendations)  
            - COMPLIANCE STATUS (from tag check)
            - CONFIDENCE LEVEL (High / Medium / Low) with reasoning

            Be specific and actionable. The engineer is in an active incident.
            """;
    }
}