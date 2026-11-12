using LunchMoney.Sdk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the Lunch Money SDK with dependency injection.</summary>
public static class LunchMoneyServiceCollectionExtensions
{
    /// <summary>
    /// Registers a factory-backed client and binds its settings to a configuration section.
    /// </summary>
    /// <param name="services">The application's services.</param>
    /// <param name="configuration">The section containing BaseUrl and Token.</param>
    /// <returns>The HTTP builder for timeout, transport and middleware configuration.</returns>
    public static IHttpClientBuilder AddLunchMoney(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AddValidatedOptions(services).Bind(configuration);
        return AddClient(services);
    }

    /// <summary>Registers a factory-backed client with explicit connection settings.</summary>
    /// <param name="services">The application's services.</param>
    /// <param name="configureOptions">Configures the API base URL and token.</param>
    /// <returns>The HTTP builder for timeout, transport and middleware configuration.</returns>
    public static IHttpClientBuilder AddLunchMoney(
        this IServiceCollection services, Action<LunchMoneyOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        AddValidatedOptions(services).Configure(configureOptions);
        return AddClient(services);
    }

    private static OptionsBuilder<LunchMoneyOptions> AddValidatedOptions(IServiceCollection services) =>
        services.AddOptions<LunchMoneyOptions>()
            .Validate(options =>
                Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps
                && string.IsNullOrEmpty(uri.UserInfo)
                && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment),
                "LunchMoney:BaseUrl must be an absolute HTTPS URL without credentials, a query or a fragment.")
            .Validate(options =>
                !string.IsNullOrWhiteSpace(options.Token)
                && !options.Token.Any(char.IsWhiteSpace),
                "LunchMoney:Token is required and must not contain whitespace. Supply the token without the Bearer prefix.")
            .ValidateOnStart();

    private static IHttpClientBuilder AddClient(IServiceCollection services)
    {
        services.TryAddSingleton<ILunchMoneyClientFactory, LunchMoneyClientFactory>();

        var builder = services.AddHttpClient<LunchMoneyClient>(LunchMoneyClientFactory.HttpClientName, (provider, httpClient) =>
        {
            var options = provider.GetRequiredService<IOptions<LunchMoneyOptions>>().Value;
            httpClient.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
        }).ConfigurePrimaryHttpMessageHandler(() => KiotaClientFactory.GetDefaultHttpMessageHandler());

        // Supplying a factory-created HttpClient bypasses Kiota's default HTTP pipeline.
        // Register that middleware explicitly, in Kiota's original order.
        foreach (var handler in KiotaClientFactory.GetDefaultHandlerActivatableTypes())
        {
            services.TryAddTransient(handler.Type);
            builder.AddHttpMessageHandler(provider =>
                (DelegatingHandler)provider.GetRequiredService(handler.Type));
        }

        return builder;
    }
}
