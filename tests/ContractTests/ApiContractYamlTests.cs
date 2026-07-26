using FluentAssertions;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace Kart.Search.ContractTests;

/// <summary>Asserts the vendored <c>Fixtures/api-contract.yaml</c> (a synced copy of the approved
/// upstream contract, contracts/README.md) still names the one endpoint this service's
/// controllers must implement - a lightweight guard against the vendored copy and the actual
/// controller surface drifting apart.</summary>
public sealed class ApiContractYamlTests
{
    [Fact]
    public void ApiContract_DefinesGetV1Search()
    {
        var yaml = new YamlStream();
        using var reader = new StreamReader(Path.Combine(AppContext.BaseDirectory, "Fixtures", "api-contract.yaml"));
        yaml.Load(reader);

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        var paths = (YamlMappingNode)root.Children[new YamlScalarNode("paths")];

        paths.Children.Should().ContainKey(new YamlScalarNode("/v1/search"));

        var searchPath = (YamlMappingNode)paths.Children[new YamlScalarNode("/v1/search")];
        searchPath.Children.Should().ContainKey(new YamlScalarNode("get"));
    }
}
