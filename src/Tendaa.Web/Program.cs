using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Tendaa.Agent.Extensions;
using Tendaa.Application.Extensions;
using Tendaa.Core;
using Tendaa.Core.Configurations.ConfigModels;
using Tendaa.Infrastructure.Context;
using Tendaa.Infrastructure.SeedData;
using Tendaa.Web.Filters;
using Tendaa.Web.HostedServices;
using Tendaa.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

ServiceCollections.ConfigureSerilog(builder);

// Add services to the container.
builder.Services.AddControllers(options => options.Conventions.Add(new ApiAntiforgeryConvention())).AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddGlobalExceptionHandling();

builder.Services.BindOptions(builder.Configuration);
builder.Services.RegisterInfrastructure(builder.Configuration);
builder.Services.RegisterServices();
builder.Services.AddTendaaAgent();
builder.Services.AddHostedService<InboundEventProcessingService>();

// Dashboard auth (Phase 5): single-password cookie login, same-origin SPA. Redirects become
// bare 401/403s — an API client can't follow a login-page redirect.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Tendaa.Auth";
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery();
builder.Services.AddSignalR();

// Web-layer IDashboardNotifier implementation (see DashboardNotifier's own doc comment for
// why this registration lives here, not in ServiceCollections.cs).
builder.Services.AddScoped<IDashboardNotifier, DashboardNotifier>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<TendaaDbContext>();
        var whatsAppOptions = scope.ServiceProvider.GetRequiredService<IOptions<WhatsAppOptions>>().Value;
        await DemoDataSeeder.SeedAsync(db, whatsAppOptions.PhoneNumberId);
    }
}
else if (app.Environment.IsEnvironment("Testing"))
{
    // WebApplicationFactory-based tests (WebhooksTestFactory etc.) swap in a SQLite
    // DbContext and build their own schema with EnsureCreatedAsync + hand-seeded rows after
    // startup — never real Npgsql migrations. Running MigrateAsync here against that SQLite
    // swap compares a SQLite-shaped live model against a snapshot captured for Npgsql, which
    // EF's pending-model-changes check correctly flags as a mismatch (it's a provider
    // difference, not an actual missed migration) and throws. So: do nothing here: the test
    // factory owns its own database setup entirely.
}
else
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<TendaaDbContext>();
        await db.Database.MigrateAsync();

        // Single-shop MVP with no shop-creation UI: the demo shop seeds itself on first
        // boot. Idempotent — does nothing once a shop exists.
        var whatsAppOptions = scope.ServiceProvider.GetRequiredService<IOptions<WhatsAppOptions>>().Value;
        await DemoDataSeeder.SeedAsync(db, whatsAppOptions.PhoneNumberId);
    }
}

// Every /api controller carries its own [Authorize] (PLAN.md); the webhook, dev, and health
// endpoints stay anonymous by absence. Per-controller rather than a FallbackPolicy so the
// SPA fallback below keeps serving index.html to logged-out browsers.
app.MapControllers();
app.MapHub<DashboardHub>("/hubs/dashboard");

app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
