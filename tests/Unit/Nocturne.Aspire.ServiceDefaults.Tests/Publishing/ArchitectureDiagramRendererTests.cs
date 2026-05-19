using FluentAssertions;
using Nocturne.Aspire.Publishing;
using Xunit;

namespace Nocturne.Aspire.ServiceDefaults.Tests.Publishing;

public class ArchitectureDiagramRendererTests
{
    private static AspirePublishModel BuildModel() => new(
        Services:
        [
            new("gateway", ServiceKind.Gateway, [], [new("8080", "5000")]),
            new("nocturne-api", ServiceKind.Api, ["8080"], []),
            new("nocturne-web", ServiceKind.Web, ["8000"], []),
            new("nocturne-postgres-server", ServiceKind.Database, ["5432"], []),
            new("watchtower", ServiceKind.Container, [], []),
        ],
        Edges:
        [
            new("nocturne-web", "nocturne-api", EdgeKind.Reference),
            new("nocturne-api", "nocturne-postgres-server", EdgeKind.Reference),
            new("nocturne-web", "nocturne-postgres-server", EdgeKind.Reference),
        ],
        Routes: []
    );

    [Fact]
    public void Render_StartsWithArchitectureBeta()
    {
        var result = ArchitectureDiagramRenderer.Render(BuildModel());
        result.Should().StartWith("architecture-beta");
    }

    [Fact]
    public void Render_ContainsExternalAndInternalGroups()
    {
        var result = ArchitectureDiagramRenderer.Render(BuildModel());
        result.Should().Contain("group external");
        result.Should().Contain("group internal");
    }

    [Fact]
    public void Render_GatewayDeclaredInExternalGroup()
    {
        var result = ArchitectureDiagramRenderer.Render(BuildModel());
        // architecture-beta service declaration: "service <id>(<icon>)[<label>] in external"
        result.Should().MatchRegex(@"service\s+\S+\s*\([^)]+\)\s*\[[^\]]+\]\s+in external");
    }

    [Fact]
    public void Render_DatabaseUsesDataBaseIcon()
    {
        var result = ArchitectureDiagramRenderer.Render(BuildModel());
        result.Should().Contain("(database)");
    }

    [Fact]
    public void Render_AllServicesPresent()
    {
        var result = ArchitectureDiagramRenderer.Render(BuildModel());
        result.Should().Contain("gateway");
        result.Should().Contain("nocturne_api");
        result.Should().Contain("nocturne_web");
        result.Should().Contain("nocturne_postgres_server");
        result.Should().Contain("watchtower");
    }

    [Fact]
    public void Render_EdgesPresent()
    {
        var result = ArchitectureDiagramRenderer.Render(BuildModel());
        result.Should().Contain("-->");
    }

    [Fact]
    public void Render_InternetNodePresent()
    {
        var result = ArchitectureDiagramRenderer.Render(BuildModel());
        result.Should().Contain("internet");
    }

    [Fact]
    public void Render_EdgesBothEndpointsMustBeDeclaredServices()
    {
        // Model with edges where one endpoint is not a declared service (simulates a
        // ParameterResource that produces an edge but no service node)
        var model = new AspirePublishModel(
            Services:
            [
                new("gateway", ServiceKind.Gateway, [], [new("8080", "5000")]),
                new("nocturne-api", ServiceKind.Api, ["8080"], []),
            ],
            Edges:
            [
                new("nocturne-api", "nocturne-postgres-server", EdgeKind.Reference), // target not in Services
                new("instance-key", "nocturne-api", EdgeKind.Reference),             // source not in Services
            ],
            Routes: []
        );

        var result = ArchitectureDiagramRenderer.Render(model);
        result.Should().NotContain("nocturne_postgres_server");
        result.Should().NotContain("instance_key");
    }
}
