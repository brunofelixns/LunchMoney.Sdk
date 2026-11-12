using Microsoft.Extensions.Options;

namespace LunchMoney.Sdk;

internal sealed class LunchMoneyClientFactory(
    IHttpClientFactory httpClientFactory,
    IOptions<LunchMoneyOptions> options) : ILunchMoneyClientFactory
{
    internal const string HttpClientName = "LunchMoney";

    public LunchMoneyClient CreateClient() =>
        new(httpClientFactory.CreateClient(HttpClientName), options);
}
