using Azure.Identity;
using Azure.ResourceManager;

var builder = WebApplication.CreateBuilder(args);

// Register ArmClient as singleton - resolves to az login locally,
// Managed Identity in Azure automatically
builder.Services.AddSingleton(_ => new ArmClient(new DefaultAzureCredential()));
builder.Services.AddMcpServer()
    .WithHttpTransport(options => { options.Stateless = true; })
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly()
    .WithPromptsFromAssembly();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(
                "https://vscode.dev",
                "https://*.vscode.dev",
                "vscode-file://vscode-app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();
app.UseCors();
app.MapMcp("/mcp");
app.MapGet("/health", () => Results.Ok(new { status = "healthy", server = "AzureOps MCP" }));

// app.Run() reads ASPNETCORE_HTTP_PORTS from the environment.
// Local: set to 5100 in launchSettings.json. Container: set to 8080 in Dockerfile.
app.Run();