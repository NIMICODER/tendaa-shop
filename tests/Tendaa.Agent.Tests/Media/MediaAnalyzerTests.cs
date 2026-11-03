using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using OpenAI;
using OpenAI.Chat;
using Tendaa.Agent.Media;
using Tendaa.Application.Models;

namespace Tendaa.Agent.Tests.Media;

/// <summary>Hand-written <see cref="HttpMessageHandler"/> stub — no mocking library, matching the
/// repo convention — standing in for Muse Spark's chat-completions endpoint via
/// <see cref="HttpClientPipelineTransport"/>, which lets a real <see cref="ChatClient"/> run
/// against a fake transport instead of a fake <c>IChatClient</c> (that fake only works for the
/// <c>Microsoft.Extensions.AI</c> agent wrapper, not the raw OpenAI SDK type <see cref="MediaAnalyzer"/> uses).</summary>
public sealed class StubChatCompletionsHandler(string responseBody) : HttpMessageHandler
{
    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
        };
    }
}

public sealed class MediaAnalyzerTests
{
    private static ChatClient CreateChatClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://api.meta.ai/v1"),
            Transport = new HttpClientPipelineTransport(httpClient),
        };
        return new OpenAIClient(new ApiKeyCredential("test-key"), options).GetChatClient("test-model");
    }

    private static string BuildCompletionResponse(string analysisJson)
    {
        // Real chat-completions shape — the model's reply content is itself the json_schema
        // string, double-encoded here as a JSON string value.
        var escaped = System.Text.Json.JsonSerializer.Serialize(analysisJson);
        return $$"""
            {"id":"chatcmpl-1","object":"chat.completion","created":1,"model":"test-model",
             "choices":[{"index":0,"message":{"role":"assistant","content":{{escaped}}},"finish_reason":"stop"}]}
            """;
    }

    [Fact]
    public async Task AnalyzeAsync_SendsImagePartAndJsonSchemaFormat_ParsesReceiptFields()
    {
        const string analysisJson = """
            {"category":"receipt","amount":37500,"bankName":"GTBank","senderName":"Tolu",
             "transferDate":"2026-10-30","reference":"ref-123","confidence":0.92}
            """;
        var handler = new StubChatCompletionsHandler(BuildCompletionResponse(analysisJson));
        var analyzer = new MediaAnalyzer(CreateChatClient(handler));

        var result = await analyzer.AnalyzeAsync([1, 2, 3], "image/jpeg");

        Assert.Equal("receipt", result.Category);
        Assert.Equal(37500m, result.Amount);
        Assert.Equal("GTBank", result.BankName);
        Assert.Equal("Tolu", result.SenderName);
        Assert.Equal("2026-10-30", result.TransferDate);
        Assert.Equal("ref-123", result.Reference);
        Assert.Equal(0.92, result.Confidence);

        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("\"type\":\"image_url\"", handler.LastRequestBody);
        Assert.Contains("\"type\":\"json_schema\"", handler.LastRequestBody);
        Assert.Contains("data:image/jpeg;base64,", handler.LastRequestBody);
    }

    [Fact]
    public async Task AnalyzeAsync_NonReceiptCategory_ParsesWithNullReceiptFields()
    {
        const string analysisJson = """
            {"category":"product_photo","amount":null,"bankName":null,"senderName":null,
             "transferDate":null,"reference":null,"confidence":0.8}
            """;
        var handler = new StubChatCompletionsHandler(BuildCompletionResponse(analysisJson));
        var analyzer = new MediaAnalyzer(CreateChatClient(handler));

        var result = await analyzer.AnalyzeAsync([1, 2, 3], "image/jpeg");

        Assert.Equal("product_photo", result.Category);
        Assert.Null(result.Amount);
        Assert.Null(result.BankName);
        Assert.Null(result.Reference);
    }

    [Fact]
    public async Task MatchProductPhotoAsync_SendsCatalogInPromptAndImagePart_ParsesCandidates()
    {
        var productId = Guid.NewGuid();
        var matchJson = $$"""
            {"candidates":[{"productId":"{{productId}}","confidence":0.87}]}
            """;
        var handler = new StubChatCompletionsHandler(BuildCompletionResponse(matchJson));
        var analyzer = new MediaAnalyzer(CreateChatClient(handler));
        var catalog = new List<ProductCatalogEntry>
        {
            new(productId, "Ankle Socks", "Socks", ["Black", "White"]),
        };

        var candidates = await analyzer.MatchProductPhotoAsync([1, 2, 3], "image/jpeg", catalog);

        Assert.Single(candidates);
        Assert.Equal(productId, candidates[0].ProductId);
        Assert.Equal(0.87, candidates[0].Confidence);

        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("\"type\":\"image_url\"", handler.LastRequestBody);
        Assert.Contains("\"type\":\"json_schema\"", handler.LastRequestBody);
        Assert.Contains("Ankle Socks", handler.LastRequestBody);
        Assert.Contains(productId.ToString(), handler.LastRequestBody);
    }

    [Fact]
    public async Task MatchProductPhotoAsync_HallucinatedId_IsSkipped()
    {
        var matchJson = """
            {"candidates":[{"productId":"not-a-real-guid","confidence":0.9}]}
            """;
        var handler = new StubChatCompletionsHandler(BuildCompletionResponse(matchJson));
        var analyzer = new MediaAnalyzer(CreateChatClient(handler));
        var catalog = new List<ProductCatalogEntry> { new(Guid.NewGuid(), "Ankle Socks", "Socks", []) };

        var candidates = await analyzer.MatchProductPhotoAsync([1, 2, 3], "image/jpeg", catalog);

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task MatchProductPhotoAsync_EmptyCatalog_ReturnsEmptyWithoutCallingTheModel()
    {
        var handler = new StubChatCompletionsHandler(BuildCompletionResponse("""{"candidates":[]}"""));
        var analyzer = new MediaAnalyzer(CreateChatClient(handler));

        var candidates = await analyzer.MatchProductPhotoAsync([1, 2, 3], "image/jpeg", []);

        Assert.Empty(candidates);
        Assert.Null(handler.LastRequestBody);
    }
}
