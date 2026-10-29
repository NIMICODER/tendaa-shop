namespace Tendaa.Core.Configurations.ConfigModels;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>How long a conversation can sit idle before the next message starts a fresh
    /// agent session instead of continuing the old one. Not secret, has a working default, so
    /// nothing new needs configuring unless this value needs tuning.</summary>
    public int StaleConversationHours { get; set; } = 12;
}
