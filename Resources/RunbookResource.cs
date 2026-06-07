using System.ComponentModel;
using ModelContextProtocol.Server;
namespace AzureOpsMcpServer.Resources;

[McpServerResourceType]
public static class RunbookResources
{
    [McpServerResource(
        UriTemplate = "azureops://runbooks/{name}",
        Name = "Operational Runbook",
        MimeType = "text/plain")]
    [Description("Returns the operational runbook for a given incident type. " +
                 "Valid names: payment-degradation, database-failover, deployment-rollback")]
    public static string GetRunbook(string name) => name.ToLowerInvariant() switch
    {
        "payment-degradation" => """
            RUNBOOK: Payment Service Degradation
            =====================================
            Severity: P1 | Owner: Platform Team | Escalation: #p1-payments

            IMMEDIATE ACTIONS (< 5 min)
            1. Check deployment history for changes in last 4 hours
            2. Verify CosmosDB RU consumption in Azure Monitor
            3. Check Service Bus dead-letter queue depth
            4. Review Application Insights failure rate - threshold: > 0.5%

            ESCALATION PATH
            - 0-15 min: On-call engineer (PagerDuty)  
            - 15-30 min: Platform Lead + Product Manager
            - 30+ min: VP Engineering + incident bridge open

            ROLLBACK DECISION
            Trigger rollback if: failure rate > 2% sustained for 5 minutes
            Rollback command: az deployment group create --rollback-on-error
            """,

        "deployment-rollback" => """
            RUNBOOK: Deployment Rollback Procedure
            =======================================
            Severity: P2 | Owner: DevOps | Escalation: #deployments

            PREREQUISITE CHECKS
            1. Confirm the failed deployment name from get_deployment_status
            2. Identify the last known-good deployment timestamp
            3. Verify rollback target is available in deployment history

            ROLLBACK STEPS
            1. Notify #deployments Slack channel
            2. az deployment group create \
                 --resource-group <RG> \
                 --rollback-on-error
            3. Monitor health for 10 minutes post-rollback
            4. Open post-mortem within 24 hours

            SUCCESS CRITERIA
            Health state returns to 'Available' within 5 minutes of rollback
            """,

        "database-failover" => """
            RUNBOOK: Cosmos DB Failover Procedure
            ======================================
            Severity: P1 | Owner: Data Platform | Escalation: #data-incidents

            WHEN TO USE THIS RUNBOOK
            - Cosmos DB region shows Unavailable in Azure Portal
            - Write latency > 2000ms sustained for 3+ minutes
            - Health API returns 503 for > 2 minutes

            FAILOVER STEPS  
            1. Confirm the affected region in Azure Portal > Cosmos DB > Replicate data globally
            2. Trigger manual failover: Portal > Failover > Select secondary region
            3. Update connection strings in App Configuration / Key Vault if not using multi-region SDK
            4. Verify application can write to new primary region

            ESTIMATED DOWNTIME: 1-3 minutes for manual failover
            """,

        _ => $"No runbook found for '{name}'. Available runbooks: payment-degradation, database-failover, deployment-rollback"
    };
}