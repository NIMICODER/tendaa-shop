using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using Tendaa.Agent.Configuration;

namespace Tendaa.Agent;

public static class AgentServiceCollectionExtensions
{
    public static IServiceCollection AddTendaaAgent(this IServiceCollection services)
    {
        services.AddSingleton<AIAgent>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MetaModelOptions>>().Value;

            var chat = new OpenAIClient(
                    new ApiKeyCredential(options.ApiKey),
                    new OpenAIClientOptions { Endpoint = new Uri(options.BaseUrl) })
                .GetChatClient(options.ChatModel);

            return chat.AsAIAgent(
                name: "Tendaa",
                instructions: "You are Tendaa, a WhatsApp sales assistant for an African shop. Keep replies short.");
        });

        return services;
    }
}
