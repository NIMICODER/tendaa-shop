using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Tendaa.Agent.Context;
using Tendaa.Agent.Tests.Fakes;
using Tendaa.Agent.Tools;
using Tendaa.Agent.Tracing;
using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Core.Configurations.ConfigModels;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Agent.Tests;

/// <summary>End-to-end proof, through the real <see cref="AgentTurnRunner"/>, of the three fixes
/// found during live verification: (1) narration said before a tool call never reaches the
/// customer, only the model's final message does; (2) after <c>ConfirmOrderSummary</c>, the exact
/// deterministic payment template — not anything the model typed — is appended; (3) exactly one
/// combined reply is produced per turn.</summary>
public class AgentTurnRunnerTests
{
    [Fact]
    public async Task RunTurnAsync_ConfirmOrderSummaryCalled_AppendsExactPaymentMessage_DropsNarration()
    {
        var context = new AgentRequestContext();
        var trace = new ToolTraceCollector();
        var pendingPayment = new PendingPaymentMessage();

        const string paymentMessage =
            "Order #1042 - ₦26,500\nPay to: GTBank, 0123456789 (Lagos Kicks Ventures)\nSend your receipt here once you've paid.";
        var orders = new FakeOrderService
        {
            ConfirmResult = TendaaResult<ConfirmOrderResult>.Success(
                new ConfirmOrderResult(1042, 26500m, "GTBank", "0123456789", "Lagos Kicks Ventures", "₦26,500", paymentMessage)),
        };

        var tools = new OrderingTools(
            new FakeProductService(), new FakeDeliveryService(), new FakeShopService(),
            orders, new FakeHandoffService(), new FakeCustomerService(), new FakeWhatsAppSender(),
            pendingPayment, context, trace);

        var narrationAndToolCall = new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [
                new TextContent("Great, confirming your order now..."),
                new FunctionCallContent("call_1", nameof(OrderingTools.ConfirmOrderSummary), new Dictionary<string, object?>()),
            ]))
        {
            FinishReason = ChatFinishReason.ToolCalls,
        };
        var finalReply = new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [new TextContent("Thanks Tolu! Your order is placed.")]))
        {
            FinishReason = ChatFinishReason.Stop,
        };
        var chatClient = new ScriptedChatClient([narrationAndToolCall, finalReply]);

        AIAgent agent = chatClient.AsAIAgent(
            name: "Tendaa",
            instructions: "test agent",
            tools: [AIFunctionFactory.Create(tools.ConfirmOrderSummary)]);

        var runner = new AgentTurnRunner(
            agent, context, trace, pendingPayment, new FakeConversationService(), DefaultAgentOptions());
        var conversation = new Conversation { Id = Guid.NewGuid(), ShopId = Guid.NewGuid(), CustomerId = Guid.NewGuid() };

        var reply = await runner.RunTurnAsync(
            conversation.ShopId, conversation.CustomerId, "+2340000000", conversation, "Yes, that's right");

        Assert.DoesNotContain("Great, confirming", reply);
        Assert.Contains("Thanks Tolu! Your order is placed.", reply);
        Assert.Contains(paymentMessage, reply);
        Assert.Contains("0123456789", reply);

        // One combined reply per turn, nothing left pending for the next turn.
        Assert.Null(pendingPayment.TakeAndClear());
    }

    [Fact]
    public async Task RunTurnAsync_NoConfirmOrderSummaryCall_ReturnsOnlyFinalReply()
    {
        var context = new AgentRequestContext();
        var trace = new ToolTraceCollector();
        var pendingPayment = new PendingPaymentMessage();

        var shopService = new FakeShopService
        {
            InfoResult = TendaaResult<ShopInfo>.Success(
                new ShopInfo("Mon-Sat, 9am to 7pm", "No returns after 48 hours", "GTBank", "0123456789", "Lagos Kicks")),
        };
        var tools = new OrderingTools(
            new FakeProductService(), new FakeDeliveryService(), shopService,
            new FakeOrderService(), new FakeHandoffService(), new FakeCustomerService(), new FakeWhatsAppSender(),
            pendingPayment, context, trace);

        var reply = new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [new TextContent("We're open Mon-Sat, 9am to 7pm.")]))
        {
            FinishReason = ChatFinishReason.Stop,
        };
        var chatClient = new ScriptedChatClient([reply]);

        AIAgent agent = chatClient.AsAIAgent(
            name: "Tendaa", instructions: "test agent", tools: [AIFunctionFactory.Create(tools.GetShopInfo)]);

        var runner = new AgentTurnRunner(
            agent, context, trace, pendingPayment, new FakeConversationService(), DefaultAgentOptions());
        var conversation = new Conversation { Id = Guid.NewGuid(), ShopId = Guid.NewGuid(), CustomerId = Guid.NewGuid() };

        var result = await runner.RunTurnAsync(
            conversation.ShopId, conversation.CustomerId, "+2340000000", conversation, "What are your hours?");

        Assert.Equal("We're open Mon-Sat, 9am to 7pm.", result);
    }

    [Fact]
    public async Task RunTurnAsync_ConversationStale_CreatesFreshSession_NeverLoadsOldOne()
    {
        var context = new AgentRequestContext();
        var trace = new ToolTraceCollector();
        var pendingPayment = new PendingPaymentMessage();
        var conversationService = new FakeConversationService();

        var reply = new ChatResponse(new ChatMessage(ChatRole.Assistant, [new TextContent("Hi again!")]))
        {
            FinishReason = ChatFinishReason.Stop,
        };
        var chatClient = new ScriptedChatClient([reply]);
        AIAgent agent = chatClient.AsAIAgent(name: "Tendaa", instructions: "test agent", tools: []);

        var runner = new AgentTurnRunner(agent, context, trace, pendingPayment, conversationService, DefaultAgentOptions());
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(), ShopId = Guid.NewGuid(), CustomerId = Guid.NewGuid(),
            SessionJson = "{\"old\":\"session\"}",
            LastMessageAt = DateTimeOffset.UtcNow.AddHours(-13), // older than the 12h default
        };

        await runner.RunTurnAsync(conversation.ShopId, conversation.CustomerId, null, conversation, "Hello");

        Assert.Equal(0, conversationService.LoadSessionCallCount);
    }

    [Fact]
    public async Task RunTurnAsync_ConversationRecent_LoadsExistingSession()
    {
        var context = new AgentRequestContext();
        var trace = new ToolTraceCollector();
        var pendingPayment = new PendingPaymentMessage();
        var conversationService = new FakeConversationService();

        var reply = new ChatResponse(new ChatMessage(ChatRole.Assistant, [new TextContent("Welcome back!")]))
        {
            FinishReason = ChatFinishReason.Stop,
        };
        var chatClient = new ScriptedChatClient([reply]);
        AIAgent agent = chatClient.AsAIAgent(name: "Tendaa", instructions: "test agent", tools: []);

        var runner = new AgentTurnRunner(agent, context, trace, pendingPayment, conversationService, DefaultAgentOptions());
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(), ShopId = Guid.NewGuid(), CustomerId = Guid.NewGuid(),
            SessionJson = "{\"old\":\"session\"}",
            LastMessageAt = DateTimeOffset.UtcNow.AddHours(-1), // within the 12h default
        };

        await runner.RunTurnAsync(conversation.ShopId, conversation.CustomerId, null, conversation, "Hello");

        Assert.Equal(1, conversationService.LoadSessionCallCount);
    }

    private static IOptions<AgentOptions> DefaultAgentOptions() => Options.Create(new AgentOptions());
}
