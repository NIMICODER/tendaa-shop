using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tendaa.Agent;
using Tendaa.Agent.Configuration;
using Tendaa.Core;
using Tendaa.Infrastructure;
using Tendaa.Infrastructure.Configuration;
using Tendaa.Infrastructure.Data;
using Tendaa.Infrastructure.Seed;
using Tendaa.Web.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

// TODO (Phase 5): AddAuthentication/AddAuthorization (cookie, admin password), AddAntiforgery,
// AddSignalR. [Authorize] goes on every /api route and the hub once those exist.

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

builder.Services.AddTendaaAgent();

builder.Services.AddScoped<ICurrentShopAccessor, CurrentShopAccessor>();
builder.Services.AddDbContext<TendaaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapGet("/dev/ping-model", async ([FromServices] AIAgent agent) =>
    {
        var response = await agent.RunAsync("Reply with a short greeting so I know you're working.");
        return Results.Text(response.Text);
    });

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<TendaaDbContext>();
        await DemoDataSeeder.SeedAsync(db);
    }
}

// TODO (Phase 5): [Authorize] on every /api route; map the SignalR hub at /hubs/dashboard.
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", timeUtc = DateTimeOffset.UtcNow }));

app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
