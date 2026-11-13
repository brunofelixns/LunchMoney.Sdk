using System.Text.Json;
using Api = LunchMoney.Sdk.Models;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;

namespace LunchMoney.Sdk.MockTests;

// The mock mixes canned responses with shared mutable fixtures. Reads use known
// demo IDs; updates preserve existing values; destructive cases use absent IDs.
internal sealed class MockScenario(LunchMoneyClient client) : IAsyncDisposable
{
    private readonly List<Func<CancellationToken, Task>> cleanup = [];
    private readonly string prefix = $"sdk-mock-{Guid.NewGuid():N}";
    public LunchMoneyClient Client { get; } = client;
    public int CategoryId => 315162;
    public int TagId { get; private set; } = 94319;
    public int ManualId => 219807;
    public int BalanceHistoryManualId { get; private set; } = 219807;
    public int CryptoId => 22001;
    public int DeletedId => 91;
    public int FileId => 1234567890;
    public long TransactionId => 2112150655L;
    public long SecondTransactionId => 2112150654L;
    public string? RequestJson { get; private set; }
    public string FileName => $"{prefix}.png";
    public string OwnerPrefix => prefix;

    public async Task PrepareAsync(MockOperation operation, CancellationToken token)
    {
        RequestJson = operation.RequestTemplate?.Replace("{name}", prefix, StringComparison.Ordinal)
            .Replace("{category}", CategoryId.ToString(), StringComparison.Ordinal)
            .Replace("{transaction}", TransactionId.ToString(), StringComparison.Ordinal)
            .Replace("{secondTransaction}", SecondTransactionId.ToString(), StringComparison.Ordinal);

        if (operation.Route == "/tags/{id}" && operation.MethodName != "GetAsync")
        {
            var tag = await Client.Tags.PostAsync(new Api.CreateTagRequestObject { Name = prefix }, cancellationToken: token);
            RequireOwnership(tag!.Name);
            TagId = tag.Id!.Value;
            cleanup.Add(ct => DeleteOwnedTagAsync(TagId, ct));
            RequireOwnership((await Client.Tags[TagId].GetAsync(cancellationToken: token))!.Name);
        }
        if (operation.MethodName != "PutAsync" || operation.ExpectedError is not null) return;
        switch (operation.Route)
        {
            case "/categories/{id}":
                RequestJson = JsonSerializer.Serialize(new { name = (await Client.Categories[CategoryId].GetAsync(cancellationToken: token))!.Name }); break;
            case "/manual_accounts/{id}":
                RequestJson = JsonSerializer.Serialize(new { exclude_from_transactions = (await Client.Manual_accounts[ManualId].GetAsync(cancellationToken: token))!.ExcludeFromTransactions }); break;
            case "/crypto/manual/{id}":
                RequestJson = JsonSerializer.Serialize(new { balance = (await Client.Crypto.Manual[CryptoId].GetAsync(cancellationToken: token))!.Balance }); break;
            case "/transactions/{id}":
                RequestJson = JsonSerializer.Serialize(new { payee = (await Client.Transactions[TransactionId].GetAsync(cancellationToken: token))!.Payee }); break;
            case "/transactions":
                var first = await Client.Transactions[TransactionId].GetAsync(cancellationToken: token);
                var second = await Client.Transactions[SecondTransactionId].GetAsync(cancellationToken: token);
                RequestJson = JsonSerializer.Serialize(new { transactions = new[] { new { id = TransactionId, payee = first!.Payee }, new { id = SecondTransactionId, payee = second!.Payee } } }); break;
            case "/me/account/settings":
                RequestJson = JsonSerializer.Serialize(new { display_name = (await Client.Me.Account.Settings.GetAsync(cancellationToken: token))!.DisplayName }); break;
            case "/me/user/settings":
                RequestJson = JsonSerializer.Serialize(new { show_debits_as_negative = (await Client.Me.User.Settings.GetAsync(cancellationToken: token))!.ShowDebitsAsNegative }); break;
            case "/me/user/account/settings":
                RequestJson = JsonSerializer.Serialize(new { auto_review_transaction_on_creation = (await Client.Me.User.Account.Settings.GetAsync(cancellationToken: token))!.AutoReviewTransactionOnCreation }); break;
            case "/balance_history/deleted/{account_id}/details":
                var all = await Client.Balance_history.GetAsync(cancellationToken: token);
                using (var parsed = ToJson(all!))
                {
                    var source = parsed.RootElement.GetProperty("balance_history").EnumerateArray().Select(row => row.GetProperty("source"))
                        .Single(row => row.GetProperty("type").GetString() == "deleted" && row.GetProperty("deleted_account_id").GetInt32() == DeletedId);
                    RequestJson = JsonSerializer.Serialize(new { name = source.GetProperty("name").GetString() });
                }
                break;
            case "/balance_history/{account_type}/{account_id}":
            case "/balance_history/crypto_synced/{account_id}/{symbol}":
                var isSyncedCrypto = operation.Route.Contains("crypto_synced", StringComparison.Ordinal);
                var current = isSyncedCrypto
                    ? await Client.Balance_history.Crypto_synced[33004]["btc"].GetAsync(cancellationToken: token)
                    : await Client.Balance_history.GetAsync(cancellationToken: token);
                using (var parsed = ToJson(current!))
                {
                    var account = parsed.RootElement.GetProperty("balance_history").EnumerateArray().First(row =>
                        (isSyncedCrypto || row.GetProperty("source").GetProperty("type").GetString() == "manual")
                        && row.GetProperty("balances").EnumerateArray().Any(balance => balance.GetProperty("type").GetString() == "historical"));
                    if (!isSyncedCrypto) BalanceHistoryManualId = account.GetProperty("source").GetProperty("manual_account_id").GetInt32();
                    var entry = account.GetProperty("balances").EnumerateArray().First(row => row.GetProperty("type").GetString() == "historical");
                    Dictionary<string, string?> value = new() { ["month"] = entry.GetProperty("month").GetString(), ["balance"] = entry.GetProperty("balance").GetString() };
                    if (entry.TryGetProperty("currency", out var currency) && currency.ValueKind == JsonValueKind.String) value["currency"] = currency.GetString();
                    if (entry.TryGetProperty("crypto_balance", out var crypto) && crypto.ValueKind == JsonValueKind.String) value["crypto_balance"] = crypto.GetString();
                    RequestJson = JsonSerializer.Serialize(new { balances = new[] { value } });
                }
                break;
        }
    }

    public string ResolvePath(string route) => route.Replace("{category}", CategoryId.ToString(), StringComparison.Ordinal)
        .Replace("{balanceHistoryManual}", BalanceHistoryManualId.ToString(), StringComparison.Ordinal)
        .Replace("{tag}", TagId.ToString(), StringComparison.Ordinal).Replace("{manual}", ManualId.ToString(), StringComparison.Ordinal)
        .Replace("{crypto}", CryptoId.ToString(), StringComparison.Ordinal).Replace("{deleted}", DeletedId.ToString(), StringComparison.Ordinal)
        .Replace("{file}", FileId.ToString(), StringComparison.Ordinal).Replace("{transaction}", TransactionId.ToString(), StringComparison.Ordinal);

    public void RecordResponse(MockOperation operation, string responseBody)
    {
        if (operation.MethodName != "PostAsync" || string.IsNullOrWhiteSpace(responseBody)) return;
        using var json = JsonDocument.Parse(responseBody);
        var root = json.RootElement;
        switch (operation.Route)
        {
            case "/tags":
                RequireOwnership(root.GetProperty("name").GetString());
                var tag = root.GetProperty("id").GetInt32();
                cleanup.Add(ct => DeleteOwnedTagAsync(tag, ct)); break;
            case "/categories":
                RequireOwnership(root.GetProperty("name").GetString());
                var category = root.GetProperty("id").GetInt32();
                cleanup.Add(async ct => { var resource = await Client.Categories[category].GetAsync(cancellationToken: ct); RequireOwnership(resource!.Name); await Client.Categories[category].DeleteAsync(r => r.QueryParameters.Force = true, ct); }); break;
            case "/manual_accounts":
                RequireOwnership(root.GetProperty("name").GetString());
                var account = root.GetProperty("id").GetInt32();
                cleanup.Add(async ct => { var resource = await Client.Manual_accounts[account].GetAsync(cancellationToken: ct); RequireOwnership(resource!.Name); await Client.Manual_accounts[account].DeleteAsync(r => { r.QueryParameters.DeleteItems = true; r.QueryParameters.DeleteBalanceHistory = true; }, ct); }); break;
            case "/crypto/manual":
                RequireOwnership(root.GetProperty("name").GetString());
                var crypto = root.GetProperty("id").GetInt32();
                cleanup.Add(async ct => { var resource = await Client.Crypto.Manual[crypto].GetAsync(cancellationToken: ct); RequireOwnership(resource!.Name); await Client.Crypto.Manual[crypto].DeleteAsync(r => r.QueryParameters.KeepHistory = false, ct); }); break;
            case "/transactions":
                foreach (var row in root.GetProperty("transactions").EnumerateArray())
                {
                    RequireOwnership(row.GetProperty("payee").GetString());
                    var transaction = row.GetProperty("id").GetInt64();
                    cleanup.Add(async ct => { var resource = await Client.Transactions[transaction].GetAsync(cancellationToken: ct); RequireOwnership(resource!.Payee); await Client.Transactions[transaction].DeleteAsync(cancellationToken: ct); });
                }
                break;
        }
    }

    private async Task DeleteOwnedTagAsync(int id, CancellationToken token)
    {
        var resource = await Client.Tags[id].GetAsync(cancellationToken: token);
        RequireOwnership(resource!.Name);
        await Client.Tags[id].DeleteAsync(r => r.QueryParameters.Force = true, token);
    }

    public async ValueTask DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        List<Exception> failures = [];
        foreach (var action in cleanup.AsEnumerable().Reverse())
        {
            try { await action(timeout.Token); }
            catch (ApiException error) when (error.ResponseStatusCode == 404) { }
            catch (Exception error) { failures.Add(error); }
        }
        if (failures.Count > 0) throw new AggregateException("Could not clean up owned mock-test resources.", failures);
    }

    internal static T Body<T>(string json) where T : IParsable, new()
    {
        using var document = JsonDocument.Parse(json);
        return new JsonParseNode(document.RootElement).GetObjectValue<T>(_ => new T())!;
    }
    private void RequireOwnership(string? name)
    {
        if (name is null || !name.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Mock returned a resource not owned by this scenario; refusing to modify or delete it.");
    }
    internal static JsonDocument ToJson(IParsable model)
    {
        using var writer = new JsonSerializationWriter();
        writer.WriteObjectValue<IParsable>(null, model);
        using var stream = writer.GetSerializedContent();
        return JsonDocument.Parse(stream);
    }
}
