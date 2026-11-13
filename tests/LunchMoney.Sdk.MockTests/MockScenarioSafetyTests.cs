using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LunchMoney.Sdk.MockTests;

public sealed class MockScenarioSafetyTests
{
    [Theory]
    [InlineData("PutAsync")]
    [InlineData("DeleteAsync")]
    public async Task CannedCreateResponseCannotAuthorizeMutatingAnUnownedTag(string method)
    {
        var transport = new CannedTagTransport();
        var services = new ServiceCollection();
        services.AddLunchMoney(options => { options.BaseUrl = OfficialMockFixture.BaseUrl; options.Token = "synthetic-safety-token"; })
            .ConfigurePrimaryHttpMessageHandler(() => transport);
        using var provider = services.BuildServiceProvider();
        var scenario = new MockScenario(provider.GetRequiredService<LunchMoneyClient>());
        var operation = Assert.Single(MockOperations.All, item => item.Route == "/tags/{id}" && item.MethodName == method);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await scenario.PrepareAsync(operation, CancellationToken.None);
                await operation.Send(scenario, CancellationToken.None);
            });
        }
        finally
        {
            await Assert.ThrowsAsync<AggregateException>(async () => await scenario.DisposeAsync());
        }
        Assert.DoesNotContain(transport.Methods, item => item is "PUT" or "DELETE");
    }

    private sealed class CannedTagTransport : HttpMessageHandler
    {
        public List<string> Methods { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Methods.Add(request.Method.Method);
            string? name = "Shared demo tag";
            if (request.Method == HttpMethod.Post)
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                name = body.RootElement.GetProperty("name").GetString();
            }
            return new HttpResponseMessage(request.Method == HttpMethod.Delete ? HttpStatusCode.NoContent : HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(JsonSerializer.Serialize(new { id = 94319, name }), Encoding.UTF8, "application/json")
            };
        }
    }
}
