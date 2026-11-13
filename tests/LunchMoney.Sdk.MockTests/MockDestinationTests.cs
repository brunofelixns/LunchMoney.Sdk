using Xunit;

namespace LunchMoney.Sdk.MockTests;

public sealed class MockDestinationTests
{
    [Theory]
    [InlineData("https://api.lunchmoney.dev/v2/categories")]
    [InlineData("http://mock.lunchmoney.dev/v2/categories")]
    [InlineData("https://mock.lunchmoney.dev:444/v2/categories")]
    [InlineData("https://mock.lunchmoney.dev/v1/categories")]
    [InlineData("https://example.com/v2/categories")]
    [InlineData("https://user:password@mock.lunchmoney.dev/v2/categories")]
    public void RejectsDestinationsOutsideTheOfficialHttpsMock(string uri) =>
        Assert.Throws<InvalidOperationException>(() => OfficialMockFixture.EnsureMockDestination(new Uri(uri)));
}
