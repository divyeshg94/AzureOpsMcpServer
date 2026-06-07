using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        // Stateless mode: recommended unless you need server-to-client
        // requests like sampling or elicitation. Stateless is safe to 
        // run behind a load balancer - important when we deploy in Part 3.
        options.Stateless = true;
    })
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly()
    .WithPromptsFromAssembly();

// CORS is required for Copilot Chat (browser-based) to reach your local server
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();
app.MapMcp("/mcp");   // MCP endpoint lives at /mcp
app.MapGet("/health", () => Results.Ok(new { status = "healthy", server = "AzureOps MCP" }));

app.Run("http://localhost:5100");