using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using Tendaa.Agent.Context;
using Tendaa.Agent.Tools;
using Tendaa.Agent.Tracing;
using Tendaa.Core.Configurations.ConfigModels;

namespace Tendaa.Agent.Extensions;

public static class AgentServiceCollectionExtensions
{
    /// <summary>The chat client is a thin, thread-safe HTTP wrapper, so it's a singleton. The
    /// agent built from it is scoped: its tools (<see cref="OrderingTools"/>) read the current
    /// turn's identity from scoped <see cref="IAgentRequestContext"/>, so the agent itself can't
    /// be a singleton without leaking one customer's identity into another's request.</summary>
    public static IServiceCollection AddTendaaAgent(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MetaModelOptions>>().Value;

            return new OpenAIClient(
                    new ApiKeyCredential(options.ApiKey),
                    new OpenAIClientOptions { Endpoint = new Uri(options.BaseUrl) })
                .GetChatClient(options.ChatModel);
        });

        services.AddScoped<IAgentRequestContext, AgentRequestContext>();
        services.AddScoped<IToolTraceCollector, ToolTraceCollector>();
        services.AddScoped<OrderingTools>();

        services.AddScoped<AIAgent>(sp =>
        {
            var chat = sp.GetRequiredService<ChatClient>();
            var tools = sp.GetRequiredService<OrderingTools>();

            return chat.AsAIAgent(
                name: "Tendaa",
                instructions: AgentInstructions.Text,
                tools:
                [
                    AIFunctionFactory.Create(tools.SearchProducts),
                    AIFunctionFactory.Create(tools.GetProductDetails),
                    AIFunctionFactory.Create(tools.GetDeliveryFee),
                    AIFunctionFactory.Create(tools.GetShopInfo),
                    AIFunctionFactory.Create(tools.UpsertDraftOrder),
                    AIFunctionFactory.Create(tools.GetOrderSummary),
                    AIFunctionFactory.Create(tools.ConfirmOrderSummary),
                    AIFunctionFactory.Create(tools.GetMyOrders),
                    AIFunctionFactory.Create(tools.RequestHandoff),
                ]);
        });

        return services;
    }
}
