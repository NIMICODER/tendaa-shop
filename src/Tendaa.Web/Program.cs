using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Tendaa.Agent.Extensions;
using Tendaa.Application.Extensions;
using Tendaa.Core.Configurations.ConfigModels;
using Tendaa.Infrastructure.Context;
using Tendaa.Infrastructure.SeedData;
using Tendaa.Web.HostedServices;

var builder = WebApplication.CreateBuilder(args);

ServiceCollections.ConfigureSerilog(builder);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddGlobalExceptionHandling();

builder.Services.BindOptions(builder.Configuration);
builder.Services.RegisterInfrastructure(builder.Configuration);
builder.Services.RegisterServices();
builder.Services.AddTendaaAgent();
builder.Services.AddHostedService<InboundEventProcessingService>();

// TODO (Phase 5): AddAuthentication/AddAuthorization (cookie, admin password), AddAntiforgery,
// AddSignalR. [Authorize] goes on every /api route and the hub once those exist.

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

// TODO (Phase 5): [Authorize] on every /api route; map the SignalR hub at /hubs/dashboard.
app.MapControllers();

app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
