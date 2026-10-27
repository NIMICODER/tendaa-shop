using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OpenAI.Chat;
using Tendaa.Agent.Configuration;

namespace Tendaa.Agent;

public static class AgentServiceCollectionExtensions
{
    public static IServiceCollection AddTendaaAgent(this IServiceCollection services, MetaModelOptions options)
    {
        var chat = new OpenAIClient(
                new ApiKeyCredential(options.ApiKey),
                new OpenAIClientOptions { Endpoint = new Uri(options.BaseUrl) })
            .GetChatClient(options.ChatModel);

        var agent = chat.AsAIAgent(
            name: "Tendaa",
            instructions: "You are Tendaa, a WhatsApp sales assistant for an African shop. Keep replies short.");

        services.AddSingleton(agent);

        return services;
    }
}
