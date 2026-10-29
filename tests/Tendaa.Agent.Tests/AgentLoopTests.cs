using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Tendaa.Agent.Context;
using Tendaa.Agent.Tests.Fakes;
using Tendaa.Agent.Tools;
using Tendaa.Agent.Tracing;
using Tendaa.Application.Common;
using Tendaa.Application.Models;

namespace Tendaa.Agent.Tests;

/// <summary>End-to-end proof that the tool-calling loop actually works: a real <see cref="AIAgent"/>
/// built via <c>AsAIAgent</c>, wired to a real <see cref="OrderingTools"/> instance, but given a
/// <see cref="ScriptedChatClient"/> instead of a connection to Meta Model API — no real API call,
/// no mocking library.</summary>
public class AgentLoopTests
{
    [Fact]
    public async Task Agent_CallsTool_ThenReturnsTheFinalReply()
    {
        var context = new AgentRequestContext();
        context.SetIdentity(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid());
        var trace = new ToolTraceCollector();

        var shopService = new FakeShopService
        {
            InfoResult = TendaaResult<ShopInfo>.Success(
                new ShopInfo("Mon-Sat, 9am to 7pm", "No returns after 48 hours", "GTBank", "0123456789", "Lagos Kicks")),
        };
        var tools = new OrderingTools(
            new FakeProductService(), new FakeDeliveryService(), shopService,
            new FakeOrderService(), new FakeHandoffService(), new FakeCustomerService(), new FakeWhatsAppSender(),
            new PendingPaymentMessage(), context, trace);

        var toolCall = new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("call_1", nameof(OrderingTools.GetShopInfo), new Dictionary<string, object?>())]))
        {
            FinishReason = ChatFinishReason.ToolCalls,
        };
        var finalReply = new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [new TextContent("We're open Mon-Sat, 9am to 7pm.")]))
        {
            FinishReason = ChatFinishReason.Stop,
        };
        var chatClient = new ScriptedChatClient([toolCall, finalReply]);

        AIAgent agent = chatClient.AsAIAgent(
            name: "Tendaa",
            instructions: "test agent",
            tools: [AIFunctionFactory.Create(tools.GetShopInfo)]);

        var session = await agent.CreateSessionAsync();
        var response = await agent.RunAsync("What are your hours?", session);

        Assert.Equal("We're open Mon-Sat, 9am to 7pm.", response.Text);

        var toolCallEntry = Assert.Single(trace.Entries);
        Assert.Equal(nameof(OrderingTools.GetShopInfo), toolCallEntry.ToolName);
        Assert.Equal(context.ShopId, shopService.LastShopId);

        Assert.Equal(2, chatClient.ReceivedMessages.Count);
        Assert.Contains(
            chatClient.ReceivedMessages[1],
            m => m.Contents.OfType<FunctionResultContent>().Any(c => c.CallId == "call_1"));
    }

    /// <summary>Guards the exact bug found live: <see cref="ChatResponse.Text"/> /
    /// <see cref="AgentResponse"/>'s <c>Text</c> property concatenates every <see
    /// cref="TextContent"/> across every message in a run, so pre-tool-call narration in the
    /// same message as a function call ends up joined with the final reply. Callers (see
    /// <c>AgentTurnRunner</c>) must use the last message in <c>Messages</c>, not <c>Text</c>.</summary>
    [Fact]
    public async Task Agent_NarratesBeforeToolCall_LastMessageIsOnlyTheFinalReply()
    {
        var context = new AgentRequestContext();
        context.SetIdentity(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid());
        var trace = new ToolTraceCollector();

        var shopService = new FakeShopService
        {
            InfoResult = TendaaResult<ShopInfo>.Success(
                new ShopInfo("Mon-Sat, 9am to 7pm", "No returns after 48 hours", "GTBank", "0123456789", "Lagos Kicks")),
        };
        var tools = new OrderingTools(
            new FakeProductService(), new FakeDeliveryService(), shopService,
            new FakeOrderService(), new FakeHandoffService(), new FakeCustomerService(), new FakeWhatsAppSender(),
            new PendingPaymentMessage(), context, trace);

        var narrationAndToolCall = new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [
                new TextContent("Let me check that for you..."),
                new FunctionCallContent("call_1", nameof(OrderingTools.GetShopInfo), new Dictionary<string, object?>()),
            ]))
        {
            FinishReason = ChatFinishReason.ToolCalls,
        };
        var finalReply = new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [new TextContent("We're open Mon-Sat, 9am to 7pm.")]))
        {
            FinishReason = ChatFinishReason.Stop,
        };
        var chatClient = new ScriptedChatClient([narrationAndToolCall, finalReply]);

        AIAgent agent = chatClient.AsAIAgent(
            name: "Tendaa",
            instructions: "test agent",
            tools: [AIFunctionFactory.Create(tools.GetShopInfo)]);

        var session = await agent.CreateSessionAsync();
        var response = await agent.RunAsync("What are your hours?", session);

        // The bug: Text joins narration + final reply. The fix reads only the last message.
        Assert.Contains("Let me check", response.Text);
        Assert.Equal("We're open Mon-Sat, 9am to 7pm.", response.Messages[^1].Text);
    }
}
