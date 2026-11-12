using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace LunchMoney.Sdk;

public partial class LunchMoneyClient
{
    /// <summary>Creates the SDK using a supplied HTTP client and connection settings.</summary>
    /// <param name="httpClient">The HTTP client, including its configured transport and middleware.</param>
    /// <param name="options">The connection settings, validated by AddLunchMoney registration.</param>
    [ActivatorUtilitiesConstructor]
    public LunchMoneyClient(HttpClient httpClient, IOptions<LunchMoneyOptions> options)
        : this(CreateRequestAdapter(httpClient, options))
    {
    }

    private static HttpClientRequestAdapter CreateRequestAdapter(
        HttpClient httpClient, IOptions<LunchMoneyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        var settings = options.Value;
        var baseUri = httpClient.BaseAddress ?? new Uri(settings.BaseUrl.TrimEnd('/'));
        var authentication = new ApiKeyAuthenticationProvider(
            $"Bearer {settings.Token}",
            "Authorization",
            ApiKeyAuthenticationProvider.KeyLocation.Header,
            [baseUri.Host]);

        return new HttpClientRequestAdapter(authentication, httpClient: httpClient)
        {
            BaseUrl = baseUri.AbsoluteUri.TrimEnd('/')
        };
    }
}
