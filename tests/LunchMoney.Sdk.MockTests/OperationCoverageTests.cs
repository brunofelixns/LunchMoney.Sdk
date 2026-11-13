using System.Reflection;
using Microsoft.Kiota.Abstractions;
using Xunit;

namespace LunchMoney.Sdk.MockTests;

public sealed class OperationCoverageTests
{
    [Fact]
    public void EveryGeneratedHttpOperationHasExactlyOneMockCase()
    {
        var operations = typeof(LunchMoneyClient).Assembly.GetTypes()
            .Where(type => typeof(BaseRequestBuilder).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.Name is "GetAsync" or "PostAsync" or "PutAsync" or "PatchAsync" or "DeleteAsync" or "HeadAsync" or "OptionsAsync"
                && typeof(Task).IsAssignableFrom(method.ReturnType))
            .Select(method => $"{method.DeclaringType!.FullName}.{method.Name}")
            .Order(StringComparer.Ordinal).ToArray();
        var covered = MockOperations.All.Select(operation => operation.OperationId)
            .Order(StringComparer.Ordinal).ToArray();

        Assert.NotEmpty(operations);
        Assert.Equal(covered.Length, covered.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(covered.Length, MockOperations.All.Select(operation => operation.DisplayName).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(operations, covered);
    }
}
