using Microsoft.Agents.AI;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tendaa.Agent.Context;
using Tendaa.Agent.Tests.Fakes;
using Tendaa.Agent.Tracing;
using Tendaa.Application.Services.Implementations;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core;
using Tendaa.Core.Configurations.ConfigModels;
using Tendaa.Core.Models.Entities;
using Tendaa.Infrastructure;
using Tendaa.Infrastructure.Context;
using Tendaa.Infrastructure.Data.Implementations;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Agent.Tests;

/// <summary>Protects SPEC.md rule 4 (one customer's data never reaches another's conversation).
/// Uses a real SQLite-backed <see cref="ConversationService"/>/<see cref="CustomerService"/> (not
/// faked — session isolation is exactly what's under test) and a real <see cref="AgentTurnRunner"/>
/// driven by the existing <see cref="ScriptedChatClient"/> fake, so these tests prove what the
/// model actually receives, not just that the right ids were passed around.</summary>
public sealed class CustomerIsolationTests
{
    private static ChatResponse Reply(string text) =>
        new(new ChatMessage(ChatRole.Assistant, [new TextContent(text)])) { FinishReason = ChatFinishReason.Stop };

    private sealed class Harness : IDisposable
    {
        private readonly SqliteConnection _connection;
        public ServiceProvider Provider { get; }
        public ScriptedChatClient ChatClient { get; }

        public Harness(IReadOnlyList<ChatResponse> script)
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            ChatClient = new ScriptedChatClient(script);

            var services = new ServiceCollection();
            services.AddSingleton<ICurrentShopAccessor, CurrentShopAccessor>();
            services.AddDbContext<TendaaDbContext>(o => o.UseSqlite(_connection));
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IConversationService, ConversationService>();
            services.AddScoped<IAgentRequestContext, AgentRequestContext>();
            services.AddScoped<IToolTraceCollector, ToolTraceCollector>();
            services.AddScoped<IPendingPaymentMessage, PendingPaymentMessage>();
            services.AddSingleton(ChatClient);
            services.AddScoped<AIAgent>(sp =>
                sp.GetRequiredService<ScriptedChatClient>().AsAIAgent(name: "Tendaa", instructions: "test agent", tools: []));
            services.AddScoped<IAgentTurnRunner, AgentTurnRunner>();
            services.AddSingleton<IOptions<AgentOptions>>(Options.Create(new AgentOptions()));

            Provider = services.BuildServiceProvider();
            using var scope = Provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<TendaaDbContext>().Database.EnsureCreated();
        }

        public async Task<(Customer Customer, Conversation Conversation)> CreateCustomerAsync(
            IServiceProvider scopedServices, Guid shopId, string bsuid)
        {
            var customer = (await scopedServices.GetRequiredService<ICustomerService>()
                .FindOrCreateByBsuIdAsync(shopId, bsuid)).Data!;
            var conversation = (await scopedServices.GetRequiredService<IConversationService>()
                .GetOrCreateAsync(shopId, customer.Id)).Data!;
            return (customer, conversation);
        }

        public void Dispose()
        {
            Provider.Dispose();
            _connection.Dispose();
        }
    }

    [Fact]
    public async Task TwoCustomers_SequentialMessages_SecondCustomerSessionContainsNothingFromFirst()
    {
        using var harness = new Harness([
            Reply("Nice to meet you Alice! What would you like to order today?"),
            Reply("Hi there! What would you like to order today?"),
        ]);
        var shopId = Guid.NewGuid();

        using var scopeA = harness.Provider.CreateScope();
        var (customerA, conversationA) = await harness.CreateCustomerAsync(scopeA.ServiceProvider, shopId, "US.A");
        await scopeA.ServiceProvider.GetRequiredService<IAgentTurnRunner>()
            .RunTurnAsync(shopId, customerA.Id, null, conversationA, "My name is Alice, hi!");

        using var scopeB = harness.Provider.CreateScope();
        var (customerB, conversationB) = await harness.CreateCustomerAsync(scopeB.ServiceProvider, shopId, "US.B");
        await scopeB.ServiceProvider.GetRequiredService<IAgentTurnRunner>()
            .RunTurnAsync(shopId, customerB.Id, null, conversationB, "Hello");

        var callForB = harness.ChatClient.ReceivedMessages.Single(call => call.Any(m => m.Text.Contains("Hello")));
        Assert.DoesNotContain(callForB, m => m.Text.Contains("Alice"));
    }

    [Fact]
    public async Task TwoCustomers_ConcurrentMessages_DifferentPartitions_NoCrossContamination()
    {
        using var harness = new Harness([
            Reply("Nice to meet you Alice! What would you like to order today?"),
            Reply("Hi there! What would you like to order today?"),
        ]);
        var shopId = Guid.NewGuid();

        // Resolve both customers/conversations up front, mirroring how InboundEventProcessingService's
        // partition workers each get their own DI scope before running concurrently.
        using var scopeA = harness.Provider.CreateScope();
        using var scopeB = harness.Provider.CreateScope();
        var (customerA, conversationA) = await harness.CreateCustomerAsync(scopeA.ServiceProvider, shopId, "US.A");
        var (customerB, conversationB) = await harness.CreateCustomerAsync(scopeB.ServiceProvider, shopId, "US.B");

        await Task.WhenAll(
            scopeA.ServiceProvider.GetRequiredService<IAgentTurnRunner>()
                .RunTurnAsync(shopId, customerA.Id, null, conversationA, "My name is Alice, hi!"),
            scopeB.ServiceProvider.GetRequiredService<IAgentTurnRunner>()
                .RunTurnAsync(shopId, customerB.Id, null, conversationB, "Hello"));

        var callForA = harness.ChatClient.ReceivedMessages.Single(call => call.Any(m => m.Text.Contains("Alice, hi")));
        var callForB = harness.ChatClient.ReceivedMessages.Single(call => call.Any(m => m.Text == "Hello"));

        Assert.DoesNotContain(callForB, m => m.Text.Contains("Alice"));
        Assert.DoesNotContain(callForA, m => m.Text == "Hello");
    }

    [Fact]
    public async Task FirstTimeCustomer_GetsFreshSessionWithNoPriorHistory()
    {
        using var harness = new Harness([Reply("Hello! Welcome, what would you like to order?")]);
        var shopId = Guid.NewGuid();

        using var scope = harness.Provider.CreateScope();
        var (customer, conversation) = await harness.CreateCustomerAsync(scope.ServiceProvider, shopId, "US.new");

        Assert.Null(conversation.SessionJson);

        await scope.ServiceProvider.GetRequiredService<IAgentTurnRunner>()
            .RunTurnAsync(shopId, customer.Id, null, conversation, "Hi");

        var onlyCall = Assert.Single(harness.ChatClient.ReceivedMessages);
        Assert.DoesNotContain(onlyCall, m => m.Role == ChatRole.Assistant);
    }
}
