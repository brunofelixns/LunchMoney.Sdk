using System.Text.Json;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;
using Xunit;
using Xunit.Abstractions;

namespace LunchMoney.Sdk.MockTests;

[Trait("Category", "OfficialMock")]
public sealed class OfficialMockTests(OfficialMockFixture fixture, ITestOutputHelper output)
    : IClassFixture<OfficialMockFixture>
{
    public static TheoryData<string> Operations => new(MockOperations.All.Select(operation => operation.DisplayName));

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task GeneratedOperationWorksAgainstOfficialMock(string operationName)
    {
        var operation = Assert.Single(MockOperations.All, candidate => candidate.DisplayName == operationName);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try
        {
            await using var scenario = new MockScenario(fixture.CreateClient());
            output.WriteLine($"Scenario owner: {scenario.OwnerPrefix}");
            await scenario.PrepareAsync(operation, cancellation.Token);
            var targetStart = fixture.Requests.Count;
            IParsable? result = null;
            try
            {
                if (operation.ExpectedError is { } status)
                {
                    var error = await Assert.ThrowsAnyAsync<Microsoft.Kiota.Abstractions.ApiException>(async () =>
                        await operation.Send(scenario, cancellation.Token));
                    Assert.Equal(status, error.ResponseStatusCode);
                    Assert.IsType(operation.ExpectedErrorType!, error);
                    result = error as IParsable;
                }
                else result = await operation.Send(scenario, cancellation.Token);
            }
            finally
            {
                if (fixture.Requests.Count > targetStart && (int)fixture.Requests[^1].StatusCode is >= 200 and < 300)
                    scenario.RecordResponse(operation, fixture.Requests[^1].ResponseBody);
            }
            Assert.NotEmpty(fixture.Requests);
            var request = fixture.Requests[^1];
            Assert.Equal(operation.MethodName[..^5].ToUpperInvariant(), request.Method);
            Assert.Equal($"/v2{scenario.ResolvePath(operation.Path)}", request.Path);
            var query = Uri.UnescapeDataString(request.Query);
            if (operation.Route == "/summary")
            {
                Assert.Contains("start_date=2025-01-01", query);
                Assert.Contains("end_date=2025-01-31", query);
            }
            if (operation.Route == "/budgets" && operation.MethodName == "DeleteAsync")
            {
                Assert.Contains("category_id=2147483647", query);
                Assert.Contains("start_date=2025-01-01", query);
            }
            if (operation.ExpectedError is { } expectedStatus) Assert.Equal(expectedStatus, (int)request.StatusCode);
            else Assert.InRange((int)request.StatusCode, 200, 299);

            if (scenario.RequestJson is { } expectedBody)
            {
                Assert.Equal("application/json", request.ContentType);
                Assert.NotNull(request.RequestBody);
                using var expected = JsonDocument.Parse(expectedBody);
                using var actual = JsonDocument.Parse(request.RequestBody);
                AssertJsonContains(expected.RootElement, actual.RootElement);
            }
            if (operation.Path.EndsWith("/attachments", StringComparison.Ordinal))
            {
                Assert.Equal("multipart/form-data", request.ContentType);
                Assert.Contains(scenario.FileName, request.RequestBody);
            }

            if (operation.ExpectedError is not null && result is null)
            {
                if (operation.ResponseFields.Length == 0)
                    Assert.Contains("Cannot DELETE /v2/transactions/group/", request.ResponseBody);
                else
                {
                    using var error = JsonDocument.Parse(request.ResponseBody);
                    Assert.False(string.IsNullOrWhiteSpace(error.RootElement.GetProperty("message").GetString()));
                }
                return;
            }

            if (operation.ResponseFields.Length == 0)
            {
                Assert.Null(result);
                return;
            }
            Assert.NotNull(result);
            using var writer = new JsonSerializationWriter();
            writer.WriteObjectValue<IParsable>(null, result);
            using var stream = writer.GetSerializedContent();
            using var typed = await JsonDocument.ParseAsync(stream, cancellationToken: cancellation.Token);
            using var wire = JsonDocument.Parse(request.ResponseBody);
            foreach (var field in operation.ResponseFields)
            {
                var expected = Select(wire.RootElement, field);
                Assert.NotEqual(JsonValueKind.Null, expected.ValueKind);
                var actual = Select(typed.RootElement, field);
                AssertJsonContains(expected, actual);
            }
        }
        finally
        {
            foreach (var request in fixture.Requests)
            {
                output.WriteLine($"{request.Method} {request.Path}: HTTP {(int)request.StatusCode}");
                if ((int)request.StatusCode >= 400)
                    output.WriteLine(request.ResponseBody[..Math.Min(1000, request.ResponseBody.Length)]);
            }
        }
    }

    private static JsonElement Select(JsonElement root, string path)
    {
        foreach (var part in path.Split('/'))
        {
            if (root.ValueKind == JsonValueKind.Array) root = root[int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)];
            else
            {
                Assert.True(root.TryGetProperty(part, out var next), $"Missing '{path}' in response/model.");
                root = next;
            }
        }
        return root;
    }

    private static void AssertJsonContains(JsonElement expected, JsonElement actual)
    {
        Assert.Equal(expected.ValueKind, actual.ValueKind);
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in expected.EnumerateObject())
                {
                    Assert.True(actual.TryGetProperty(property.Name, out var value), $"Missing '{property.Name}' in serialized model.");
                    AssertJsonContains(property.Value, value);
                }
                break;
            case JsonValueKind.Array:
                Assert.Equal(expected.GetArrayLength(), actual.GetArrayLength());
                for (var index = 0; index < expected.GetArrayLength(); index++)
                    AssertJsonContains(expected[index], actual[index]);
                break;
            case JsonValueKind.Number:
                Assert.Equal(expected.GetDecimal(), actual.GetDecimal());
                break;
            case JsonValueKind.String:
                if (expected.TryGetDateTimeOffset(out var expectedDate) && actual.TryGetDateTimeOffset(out var actualDate))
                    Assert.Equal(expectedDate, actualDate);
                else
                    Assert.Equal(expected.GetString(), actual.GetString());
                break;
            default:
                Assert.Equal(expected.GetRawText(), actual.GetRawText());
                break;
        }
    }
}
