using Tendaa.Application.WhatsApp;

namespace Tendaa.Application.Tests.WhatsApp;

/// <summary>Confirms the audio/image webhook shapes documented in CLAUDE.md's "WhatsApp Cloud
/// API" section parse into the Phase 4a/4b message types without throwing, carrying the same
/// contact fields a text message gets.</summary>
public sealed class WhatsAppWebhookParserTests
{
    private const string AudioPayload = """
        {"object":"whatsapp_business_account","entry":[{"id":"waba-1","changes":[{"value":{"messaging_product":"whatsapp","metadata":{"display_phone_number":"15551977073","phone_number_id":"1234567890"},"contacts":[{"profile":{"name":"Tolu"},"wa_id":"15550001234","user_id":"US.audio-parse-test"}],"messages":[{"from":"15550001234","from_user_id":"US.audio-parse-test","id":"wamid.audio-parse-test","timestamp":"1793298823","type":"audio","audio":{"id":"media-audio-1","mime_type":"audio/ogg; codecs=opus","voice":true}}]},"field":"messages"}]}]}
        """;

    private const string ImagePayload = """
        {"object":"whatsapp_business_account","entry":[{"id":"waba-1","changes":[{"value":{"messaging_product":"whatsapp","metadata":{"display_phone_number":"15551977073","phone_number_id":"1234567890"},"contacts":[{"profile":{"name":"Tolu"},"wa_id":"15550001234","user_id":"US.image-parse-test"}],"messages":[{"from":"15550001234","from_user_id":"US.image-parse-test","id":"wamid.image-parse-test","timestamp":"1793298823","type":"image","image":{"id":"media-image-1","mime_type":"image/jpeg","caption":"Paid, see receipt"}}]},"field":"messages"}]}]}
        """;

    [Fact]
    public void Parse_AudioMessage_ReturnsParsedVoiceMessageWithContactFields()
    {
        var result = WhatsAppWebhookParser.Parse(AudioPayload);

        var message = Assert.Single(result.Messages);
        var voice = Assert.IsType<ParsedVoiceMessage>(message);
        Assert.Equal("wamid.audio-parse-test", voice.WhatsAppMessageId);
        Assert.Equal("US.audio-parse-test", voice.Bsuid);
        Assert.Equal("media-audio-1", voice.MediaId);
        Assert.Equal("15550001234", voice.Phone);
        Assert.Equal("Tolu", voice.Name);
    }

    [Fact]
    public void Parse_ImageMessage_ReturnsParsedImageMessageWithCaptionAndContactFields()
    {
        var result = WhatsAppWebhookParser.Parse(ImagePayload);

        var message = Assert.Single(result.Messages);
        var image = Assert.IsType<ParsedImageMessage>(message);
        Assert.Equal("wamid.image-parse-test", image.WhatsAppMessageId);
        Assert.Equal("US.image-parse-test", image.Bsuid);
        Assert.Equal("media-image-1", image.MediaId);
        Assert.Equal("Paid, see receipt", image.Caption);
        Assert.Equal("15550001234", image.Phone);
        Assert.Equal("Tolu", image.Name);
    }
}
