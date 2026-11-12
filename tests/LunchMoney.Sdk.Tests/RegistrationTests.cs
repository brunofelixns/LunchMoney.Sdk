using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace LunchMoney.Sdk.Tests;

public sealed class RegistrationTests
{
    private const string TestToken = "synthetic-test-token";

    [Theory]
    [InlineData("https://direct.example/custom/v2")]
    [InlineData("https://direct.example/custom/v2/")]
    public async Task StandardTypedClientRegistrationUsesOptionsWithoutAnAdapterRegistration(string baseUrl)
    {
        var handler = new RecordingHandler();
        var services = new ServiceCollection();
        services.Configure<LunchMoneyOptions>(options =>
        {
            options.BaseUrl = baseUrl;
            options.Token = TestToken;
        });
        services.AddHttpClient<LunchMoneyClient>(http =>
                http.DefaultRequestHeaders.Add("X-Consumer", "typed-client"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<LunchMoneyClient>().Categories.GetAsync();

        Assert.NotNull(result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://direct.example/custom/v2/categories", request.Url);
        Assert.Equal($"Bearer {TestToken}", request.Authorization);
        Assert.Equal("typed-client", request.ConsumerHeader);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpClientBaseAddressOverrideControlsRequestUrlAndAuthenticationHost(bool useSdkFactory)
    {
        var handler = new RecordingHandler();
        var services = new ServiceCollection();
        services.AddLunchMoney(options => options.Token = TestToken)
            .ConfigureHttpClient(http =>
                http.BaseAddress = new Uri("https://override.example/custom/v2/"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();
        var client = ResolveClient(provider, useSdkFactory);

        await client.Categories.GetAsync();
        await client.Categories.WithUrl("https://api.lunchmoney.dev/v2/categories").GetAsync();

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("https://override.example/custom/v2/categories", handler.Requests[0].Url);
        Assert.Equal($"Bearer {TestToken}", handler.Requests[0].Authorization);
        Assert.Null(handler.Requests[1].Authorization);
    }

    [Fact]
    public async Task OptionsActionSendsBearerTokenToDefaultApiThroughFactory()
    {
        var handler = new RecordingHandler();
        var services = new ServiceCollection();
        services.AddLunchMoney(options => options.Token = TestToken)
            .ConfigureHttpClient(http => http.DefaultRequestHeaders.Add("X-Consumer", "configured"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<LunchMoneyClient>();
        var result = await client.Categories.GetAsync();

        Assert.NotNull(result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.lunchmoney.dev/v2/categories", request.Url);
        Assert.Equal($"Bearer {TestToken}", request.Authorization);
        Assert.Equal("configured", request.ConsumerHeader);
    }

    [Theory]
    [InlineData("https://proxy.example/custom/v2")]
    [InlineData("https://proxy.example/custom/v2/")]
    public async Task CustomBaseUrlPreservesPathAndNormalizesTrailingSlash(string baseUrl)
    {
        var handler = new RecordingHandler();
        var services = new ServiceCollection();
        services.AddLunchMoney(options =>
        {
            options.BaseUrl = baseUrl;
            options.Token = TestToken;
        }).ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<LunchMoneyClient>().Categories.GetAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://proxy.example/custom/v2/categories", request.Url);
        Assert.Equal($"Bearer {TestToken}", request.Authorization);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfigurationSectionBindsUrlAndToken(bool useSdkFactory)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["LunchMoney:BaseUrl"] = "https://configured.example/v2",
                ["LunchMoney:Token"] = TestToken
            }).Build();
        var handler = new RecordingHandler();
        var services = new ServiceCollection();
        services.AddLunchMoney(configuration.GetSection("LunchMoney"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();

        await ResolveClient(provider, useSdkFactory).Categories.GetAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://configured.example/v2/categories", request.Url);
        Assert.Equal($"Bearer {TestToken}", request.Authorization);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid\r\ntoken")]
    public void InvalidTokenFailsWhenClientIsResolved(string? token)
    {
        var services = new ServiceCollection();
        services.AddLunchMoney(options => options.Token = token!);
        using var provider = services.BuildServiceProvider();

        var error = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<LunchMoneyClient>());

        Assert.Contains("Token", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/v2")]
    [InlineData("http://api.example/v2")]
    [InlineData("ftp://api.example/v2")]
    [InlineData("https://user:password@api.example/v2")]
    [InlineData("https://api.example/v2?query=value")]
    [InlineData("https://api.example/v2#fragment")]
    public void InvalidBaseUrlFailsWhenClientIsResolved(string? baseUrl)
    {
        var services = new ServiceCollection();
        services.AddLunchMoney(options =>
        {
            options.Token = TestToken;
            options.BaseUrl = baseUrl!;
        });
        using var provider = services.BuildServiceProvider();

        var error = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<LunchMoneyClient>());

        Assert.Contains("BaseUrl", error.Message);
    }

    [Fact]
    public async Task InvalidOptionsFailAtHostStartupWithoutResolvingClient()
    {
        using var host = new HostBuilder()
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureServices(services => services.AddLunchMoney(options => { }))
            .Build();

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RawUrlForAnotherHostDoesNotReceiveToken(bool useSdkFactory)
    {
        var handler = new RecordingHandler();
        var services = new ServiceCollection();
        services.AddLunchMoney(options => options.Token = TestToken)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();

        await ResolveClient(provider, useSdkFactory).Categories
            .WithUrl("https://unrelated.example/categories").GetAsync();

        Assert.Null(Assert.Single(handler.Requests).Authorization);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FactoryClientKeepsKiotaRetryMiddleware(bool useSdkFactory)
    {
        var handler = new RecordingHandler(failFirstRequest: true);
        var services = new ServiceCollection();
        services.AddLunchMoney(options => options.Token = TestToken)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();

        var result = await ResolveClient(provider, useSdkFactory).Categories.GetAsync();

        Assert.NotNull(result);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public void FactoryTransportPreservesKiotaRedirectAndDecompressionSettings()
    {
        var services = new ServiceCollection();
        var builder = services.AddLunchMoney(options => options.Token = TestToken);
        using var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(builder.Name);
        while (handler is DelegatingHandler delegating)
            handler = delegating.InnerHandler!;

        var (autoRedirect, decompression) = handler switch
        {
            HttpClientHandler http => (http.AllowAutoRedirect, http.AutomaticDecompression),
            SocketsHttpHandler sockets => (sockets.AllowAutoRedirect, sockets.AutomaticDecompression),
            _ => throw new InvalidOperationException($"Unexpected transport: {handler.GetType()}")
        };

        Assert.False(autoRedirect);
        Assert.NotEqual(DecompressionMethods.None, decompression);
    }

    [Fact]
    public void ReturnedBuilderSupportsHttpClientConfiguration()
    {
        var services = new ServiceCollection();
        var builder = services.AddLunchMoney(options => options.Token = TestToken)
            .ConfigureHttpClient(http => http.Timeout = TimeSpan.FromSeconds(12));
        using var provider = services.BuildServiceProvider();

        using var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(builder.Name);

        Assert.Equal(TimeSpan.FromSeconds(12), http.Timeout);
    }

    [Fact]
    public async Task SingletonConsumerCreatesFreshConfiguredSdkAndHttpClientsPerOperation()
    {
        var handler = new RecordingHandler();
        var services = new ServiceCollection();
        var clientNumber = 0;
        services.AddLunchMoney(options => options.Token = TestToken)
            .ConfigureHttpClient(http =>
                http.DefaultRequestHeaders.Add("X-Consumer", (++clientNumber).ToString()))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddSingleton<SingletonClientConsumer>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        var consumer = provider.GetRequiredService<SingletonClientConsumer>();

        var firstClient = consumer.CreateClient();
        await firstClient.Categories.GetAsync();
        var secondClient = consumer.CreateClient();
        await secondClient.Categories.GetAsync();

        Assert.NotSame(firstClient, secondClient);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("1", handler.Requests[0].ConsumerHeader);
        Assert.Equal("2", handler.Requests[1].ConsumerHeader);
        foreach (var request in handler.Requests)
        {
            Assert.Equal("https://api.lunchmoney.dev/v2/categories", request.Url);
            Assert.Equal($"Bearer {TestToken}", request.Authorization);
        }
    }

    [Theory]
    [InlineData("", "https://api.lunchmoney.dev/v2")]
    [InlineData(TestToken, "http://api.example/v2")]
    public void SdkFactoryRejectsInvalidConnectionSettings(string token, string baseUrl)
    {
        var services = new ServiceCollection();
        services.AddLunchMoney(options =>
        {
            options.Token = token;
            options.BaseUrl = baseUrl;
        });
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILunchMoneyClientFactory>();

        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }

    private static LunchMoneyClient ResolveClient(IServiceProvider provider, bool useSdkFactory) =>
        useSdkFactory
            ? provider.GetRequiredService<ILunchMoneyClientFactory>().CreateClient()
            : provider.GetRequiredService<LunchMoneyClient>();

    private sealed class SingletonClientConsumer(ILunchMoneyClientFactory factory)
    {
        public LunchMoneyClient CreateClient() => factory.CreateClient();
    }

    private sealed record SentRequest(string? Url, string? Authorization, string? ConsumerHeader);

    private sealed class RecordingHandler(bool failFirstRequest = false) : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new SentRequest(
                request.RequestUri?.AbsoluteUri,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-Consumer", out var values) ? values.Single() : null));

            if (failFirstRequest && Requests.Count == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    RequestMessage = request
                };
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent("{\"categories\":[]}", Encoding.UTF8, "application/json")
            });
        }
    }
}
