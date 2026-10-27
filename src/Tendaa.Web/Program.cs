using Microsoft.Agents.AI;
using Tendaa.Agent;
using Tendaa.Agent.Configuration;
using Tendaa.Infrastructure.Configuration;
using Tendaa.Web.Components;
using Tendaa.Web.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOptions<MetaModelOptions>()
    .Bind(builder.Configuration.GetSection(MetaModelOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<WhatsAppOptions>()
    .Bind(builder.Configuration.GetSection(WhatsAppOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<DashboardOptions>()
    .Bind(builder.Configuration.GetSection(DashboardOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var metaModelOptions = builder.Configuration.GetSection(MetaModelOptions.SectionName).Get<MetaModelOptions>()
    ?? new MetaModelOptions();
builder.Services.AddTendaaAgent(metaModelOptions);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/dev/ping-model", async (AIAgent agent) =>
    {
        var response = await agent.RunAsync("Reply with a short greeting so I know you're working.");
        return Results.Text(response.Text);
    });
}

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
