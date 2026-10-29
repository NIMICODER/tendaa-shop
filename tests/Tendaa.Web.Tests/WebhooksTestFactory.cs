using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Tendaa.Core;
using Tendaa.Infrastructure.Context;
using Tendaa.Web.HostedServices;
using Tendaa.Web.Tests.Fakes;

namespace Tendaa.Web.Tests;

/// <summary>Real <see cref="TendaaDbContext"/> against SQLite in-memory, swapped in for Postgres
/// — same reasoning as <c>Tendaa.Application.Tests/SqliteApplicationTestBase.cs</c>: the app's
/// services call EF Core's async LINQ extensions directly, which a hand-rolled fake can't support.
/// All config values are explicit dummies, never the real `dotnet user-secrets` on this machine —
/// these tests must also pass in CI, which has no secrets configured at all. The background
/// <see cref="InboundEventProcessingService"/> is removed: these tests check controller-level
/// behavior (signature, dedupe, parsing) and must never risk a real call to Muse Spark or the
/// Graph API.</summary>
public sealed class WebhooksTestFactory : WebApplicationFactory<Program>
{
    public const string AppSecret = "test-app-secret";
    public const string VerifyToken = "test-verify-token";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public FakeWhatsAppSender WhatsAppSender { get; } = new();

    public WebhooksTestFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": Program.cs's own `if (app.Environment.IsDevelopment())` block runs
        // DemoDataSeeder against this DbContext during host startup — before this factory gets a
        // chance to create the SQLite schema, racing it ("no such table"). Skipping that block
        // entirely and seeding ourselves in InitializeDatabaseAsync avoids the race outright.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MetaModel:ApiKey"] = "test-api-key",
                ["MetaModel:BaseUrl"] = "https://example.invalid/v1",
                ["MetaModel:ChatModel"] = "test-model",
                ["MetaModel:TranscribeModel"] = "test-transcribe-model",
                ["WhatsApp:PhoneNumberId"] = "1234567890",
                ["WhatsApp:WabaId"] = "test-waba",
                ["WhatsApp:AccessToken"] = "test-access-token",
                ["WhatsApp:AppSecret"] = AppSecret,
                ["WhatsApp:VerifyToken"] = VerifyToken,
                ["Dashboard:AdminPassword"] = "test-admin-password",
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
            });
        });

        builder.ConfigureServices(services =>
        {
            // RemoveAll<DbContextOptions<TendaaDbContext>>() alone isn't enough: AddDbContext
            // registers its configure delegate via TryAddEnumerable, so a second AddDbContext
            // call adds a second delegate instead of replacing the first — both UseNpgsql and
            // UseSqlite then apply to the same builder, which is exactly this conflict.
            services.RemoveAll<DbContextOptions<TendaaDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<TendaaDbContext>>();
            services.AddDbContext<TendaaDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<IWhatsAppSender>();
            services.AddSingleton<IWhatsAppSender>(WhatsAppSender);

            var hostedService = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IHostedService)
                && d.ImplementationType == typeof(InboundEventProcessingService));
            if (hostedService is not null)
            {
                services.Remove(hostedService);
            }
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TendaaDbContext>();
        await db.Database.EnsureCreatedAsync();

        db.Shops.Add(new Core.Models.Entities.Shop
        {
            Id = Guid.NewGuid(),
            Name = "Test Shop",
            Country = "Nigeria",
            Currency = "NGN",
            TimeZone = "Africa/Lagos",
            WhatsAppPhoneNumberId = "1234567890",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
