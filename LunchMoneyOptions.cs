namespace LunchMoney.Sdk;

/// <summary>Connection settings for the Lunch Money API.</summary>
public sealed class LunchMoneyOptions
{
    /// <summary>The absolute HTTPS base URL, including the API version.</summary>
    public string BaseUrl { get; set; } = "https://api.lunchmoney.dev/v2";

    /// <summary>The API access token, without the Bearer prefix.</summary>
    public string Token { get; set; } = string.Empty;
}
