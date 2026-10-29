using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Tendaa.Core;
using Tendaa.Core.Configurations.ConfigModels;
using Tendaa.Core.Models.Entities;
using Tendaa.Infrastructure.WhatsApp;

namespace Tendaa.Web.Tests;

/// <summary>Hand-written <see cref="HttpMessageHandler"/> stub — no mocking library, matching the
/// repo convention — standing in for the Graph API so a failure's response body can be asserted
/// without a real HTTP call.</summary>
public sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
{
    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json"),
        };
        return Task.FromResult(response);
    }
}

public class WhatsAppGraphApiClientTests
{
    private readonly Customer _customer = new() { Id = Guid.NewGuid(), ShopId = Guid.NewGuid(), BsuId = "US.1", PhoneNumber = "15551234567" };

    private static IOptions<WhatsAppOptions> Options() => Microsoft.Extensions.Options.Options.Create(new WhatsAppOptions
    {
        PhoneNumberId = "1234567890",
        WabaId = "test-waba",
        AccessToken = "test-token",
        AppSecret = "test-secret",
        VerifyToken = "test-verify",
    });

    [Fact]
    public async Task SendTextAsync_NonSuccessResponse_ThrowsWithMetaErrorCodeAndMessage_NoRetry()
    {
        const string metaErrorBody = """
            {"error":{"message":"(#131030) Recipient phone number not in allowed list","type":"OAuthException","code":131030,"fbtrace_id":"abc123"}}
            """;
        var handler = new StubHttpMessageHandler(HttpStatusCode.BadRequest, metaErrorBody);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") };
        var client = new WhatsAppGraphApiClient(httpClient, Options());

        var ex = await Assert.ThrowsAsync<WhatsAppApiException>(() => client.SendTextAsync(_customer, "hello"));

        Assert.Equal(131030, ex.ErrorCode);
        Assert.Contains("not in allowed list", ex.ErrorMessage);
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal(1, handler.CallCount); // a permanent 4xx must not be retried
    }

    [Fact]
    public async Task SendTextAsync_SuccessResponse_DoesNotThrow()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """{"messages":[{"id":"wamid.1"}]}""");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") };
        var client = new WhatsAppGraphApiClient(httpClient, Options());

        await client.SendTextAsync(_customer, "hello");

        Assert.Equal(1, handler.CallCount);
    }
}
