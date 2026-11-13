using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LunchMoney.Sdk.MockTests;

public sealed class MockScenarioSafetyTests
{
    [Fact]
    public async Task BalanceHistoryUpdateUsesAnAccountWithStoredHistory()
    {
        var transport = new BalanceHistoryTransport();
        var services = new ServiceCollection();
        services.AddLunchMoney(options => { options.BaseUrl = OfficialMockFixture.BaseUrl; options.Token = "synthetic-safety-token"; })
            .ConfigurePrimaryHttpMessageHandler(() => transport);
        using var provider = services.BuildServiceProvider();
        await using var scenario = new MockScenario(provider.GetRequiredService<LunchMoneyClient>());
        var operation = Assert.Single(MockOperations.All,
            item => item.Route == "/balance_history/{account_type}/{account_id}" && item.MethodName == "PutAsync");

        await scenario.PrepareAsync(operation, CancellationToken.None);
        await operation.Send(scenario, CancellationToken.None);

        Assert.Equal("/v2/balance_history/manual/220001", transport.PutPath);
        using var body = JsonDocument.Parse(Assert.IsType<string>(transport.PutBody));
        var balance = Assert.Single(body.RootElement.GetProperty("balances").EnumerateArray());
        Assert.Equal("2026-08", balance.GetProperty("month").GetString());
        Assert.Equal("41000", balance.GetProperty("balance").GetString());
        Assert.Equal("usd", balance.GetProperty("currency").GetString());
        Assert.False(balance.TryGetProperty("type", out _));
    }

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

    private sealed class BalanceHistoryTransport : HttpMessageHandler
    {
        private const string CurrentOnly = """
            {"source":{"type":"manual","manual_account_id":219807},"balances":[
              {"type":"current","month":"2026-11","balance":"41211.8","currency":"usd","to_base":41211.8,"crypto_balance":null}]}
            """;
        private const string WithHistory = """
            {"source":{"type":"manual","manual_account_id":220001},"balances":[
              {"type":"current","month":"2026-11","balance":"42000","currency":"usd","to_base":42000,"crypto_balance":null},
              {"type":"historical","id":101,"month":"2026-08","balance":"41000","currency":"usd","to_base":41000,"crypto_balance":null}]}
            """;

        public string? PutPath { get; private set; }
        public string? PutBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.AbsolutePath;
            string response;
            if (request.Method == HttpMethod.Put)
            {
                PutPath = path;
                PutBody = await request.Content!.ReadAsStringAsync(token);
                response = WithHistory;
            }
            else if (path == "/v2/balance_history")
                response = "{\"balance_history\":[" + CurrentOnly + "," + WithHistory + "]}";
            else if (path == "/v2/balance_history/manual/219807")
                response = "{\"balance_history\":[" + CurrentOnly + "]}";
            else throw new InvalidOperationException($"Unexpected request: {request.Method} {path}");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        }
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
