using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tendaa.Agent;
using Tendaa.Agent.Extensions;
using Tendaa.Application.Extensions;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core;
using Tendaa.Core.Models.Entities;
using Tendaa.Infrastructure;
using Tendaa.Infrastructure.Context;
using Tendaa.Infrastructure.Data.Implementations;
using Tendaa.Infrastructure.Data.Interfaces;
using Tendaa.Infrastructure.SeedData;

namespace Tendaa.Evals;

/// <summary>One shared SQLite in-memory database and DI container for the whole eval run — real
/// Application/Agent services, a real <c>ChatClient</c> against the real Meta Model API (never a
/// fake, unlike <c>Tendaa.Agent.Tests</c>), but no real WhatsApp send and no real dashboard
/// SignalR push (<see cref="NoOpWhatsAppSender"/>/<see cref="NoOpDashboardNotifier"/>). Seeded
/// once with the same demo shop/catalog every other manual test in this repo has used.</summary>
public sealed class EvalFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    public ServiceProvider Services { get; }
    public Guid ShopId { get; private set; }
    public NoOpWhatsAppSender WhatsAppSender { get; }

    private EvalFixture(SqliteConnection connection, ServiceProvider services, NoOpWhatsAppSender whatsAppSender)
    {
        _connection = connection;
        Services = services;
        WhatsAppSender = whatsAppSender;
    }

    public static async Task<EvalFixture> CreateAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<EvalFixture>()
            .AddEnvironmentVariables()
            .Build();

        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.BindOptions(configuration);

        services.AddSingleton<ICurrentShopAccessor, CurrentShopAccessor>();
        services.AddDbContext<TendaaDbContext>(options => options.UseSqlite(connection));
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        var whatsAppSender = new NoOpWhatsAppSender();
        services.AddSingleton<IWhatsAppSender>(whatsAppSender);
        services.AddSingleton<IDashboardNotifier, NoOpDashboardNotifier>();

        services.RegisterServices();
        services.AddTendaaAgent();

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TendaaDbContext>();
            await db.Database.EnsureCreatedAsync();
            await DemoDataSeeder.SeedAsync(db, whatsAppPhoneNumberId: null);
        }

        var fixture = new EvalFixture(connection, provider, whatsAppSender);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TendaaDbContext>();
            fixture.ShopId = (await db.Shops.FirstAsync()).Id;
        }

        return fixture;
    }

    /// <summary>Resolves <paramref name="bsuId"/> to its <see cref="Customer"/> (creating it on
    /// first use), in a scope with <see cref="ICurrentShopAccessor"/> already set — the identity
    /// half of every eval case, shared by <see cref="SendMessageAsync"/> and direct-service cases
    /// (receipt bypass) that need the id without running a turn.</summary>
    public async Task<Customer> ResolveCustomerAsync(string bsuId, CancellationToken cancellationToken = default) =>
        await RunScopedAsync(async sp => (await sp.GetRequiredService<ICustomerService>()
            .FindOrCreateByBsuIdAsync(ShopId, bsuId, cancellationToken: cancellationToken)).Data!);

    /// <summary>The conversation id for <paramref name="bsuId"/> — for cases that need to assert
    /// on conversation-scoped state (e.g. <see cref="IHandoffService.HasOpenHandoffAsync"/>) after
    /// a <see cref="SendMessageAsync"/> turn.</summary>
    public Task<Guid> GetConversationIdAsync(string bsuId, CancellationToken cancellationToken = default) =>
        RunScopedAsync(async sp =>
        {
            var customer = (await sp.GetRequiredService<ICustomerService>()
                .FindOrCreateByBsuIdAsync(ShopId, bsuId, cancellationToken: cancellationToken)).Data!;
            var conversation = (await sp.GetRequiredService<IConversationService>()
                .GetOrCreateAsync(ShopId, customer.Id, cancellationToken)).Data!;
            return conversation.Id;
        });

    /// <summary>Runs one agent turn for <paramref name="bsuId"/>, same identity-resolve-then-run
    /// pattern as <c>DevController.Chat</c> — a fresh scope per call (so each turn gets its own
    /// DbContext, matching real request handling), but the same <paramref name="bsuId"/> across
    /// calls continues the same conversation via <c>FindOrCreateByBsuIdAsync</c> +
    /// <c>GetOrCreateAsync</c>'s idempotency. Give every eval case its own unique
    /// <paramref name="bsuId"/> so cases never share a conversation.</summary>
    public Task<string> SendMessageAsync(string bsuId, string message, CancellationToken cancellationToken = default) =>
        RunScopedAsync(async sp =>
        {
            var customer = (await sp.GetRequiredService<ICustomerService>()
                .FindOrCreateByBsuIdAsync(ShopId, bsuId, cancellationToken: cancellationToken)).Data!;
            var conversation = (await sp.GetRequiredService<IConversationService>()
                .GetOrCreateAsync(ShopId, customer.Id, cancellationToken)).Data!;

            return await sp.GetRequiredService<IAgentTurnRunner>().RunTurnAsync(
                ShopId, customer.Id, customer.PhoneNumber, conversation, message,
                cancellationToken: cancellationToken);
        });

    /// <summary>For receipt eval cases, which bypass real vision (see BUILDLOG.md/README eval
    /// notes) and call <see cref="IOrderService.CheckReceiptAsync"/> directly with synthetic
    /// already-extracted fields, or any other case that needs a real Application service
    /// (<typeparamref name="T"/>) with the shop identity already set — a fresh scope per call,
    /// matching real request handling.</summary>
    public async Task<TResult> RunScopedAsync<TResult>(Func<IServiceProvider, Task<TResult>> action)
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentShopAccessor>().SetShopId(ShopId);
        return await action(scope.ServiceProvider);
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
