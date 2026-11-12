namespace LunchMoney.Sdk;

/// <summary>Creates configured SDK clients for individual operations.</summary>
/// <remarks>The factory can be injected into singleton services. Request a new client per operation.</remarks>
public interface ILunchMoneyClientFactory
{
    /// <summary>Creates a new SDK client using the registered HTTP configuration and connection settings.</summary>
    LunchMoneyClient CreateClient();
}
