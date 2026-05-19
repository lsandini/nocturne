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
    public void Render_WebServiceSplitsIntoBffAndFrontendSubgroup()
    {
        var result = ArchitectureDiagramRenderer.Render(BuildModel());
        result.Should().Contain("nocturne_web_group");
        result.Should().Contain("nocturne_web_bff");
        result.Should().Contain("nocturne_web_frontend");
        // Should NOT emit a plain service node for the web service
        result.Should().NotMatchRegex(@"service nocturne_web\(");
    }

    [Fact]
    public void Render_EdgesFromWebServiceOriginateFromBff()
    {
        // The test model has nocturne-web → nocturne-api; outbound side is the BFF
        var result = ArchitectureDiagramRenderer.Render(BuildModel());
        result.Should().Contain("nocturne_web_bff:R --> L:nocturne_api");
        // Plain web node ID must not appear as an edge endpoint
        result.Should().NotMatchRegex(@"nocturne_web:R -->|-->\s+L:nocturne_web\b");
    }

    [Fact]
    public void Render_GatewayToWebTargetsFrontend()
    {
        var model = new AspirePublishModel(
            Services:
            [
                new("gateway", ServiceKind.Gateway, [], [new("8080", "5000")]),
                new("nocturne-web", ServiceKind.Web, ["8000"], []),
            ],
            Edges: [new("gateway", "nocturne-web", EdgeKind.Reference)],
            Routes: []
        );

        var result = ArchitectureDiagramRenderer.Render(model);
        result.Should().Contain("gateway:R --> L:nocturne_web_frontend");
        result.Should().NotContain("gateway:R --> L:nocturne_web_bff");
    }

    [Fact]
    public void Render_NonGatewayToWebTargetsBff()
    {
        // Server-to-server calls (e.g. API posting alerts to SvelteKit) target the BFF,
        // not the client bundle.
        var model = new AspirePublishModel(
            Services:
            [
                new("nocturne-api", ServiceKind.Api, ["8080"], []),
                new("nocturne-web", ServiceKind.Web, ["8000"], []),
            ],
            Edges: [new("nocturne-api", "nocturne-web", EdgeKind.Reference)],
            Routes: []
        );

        var result = ArchitectureDiagramRenderer.Render(model);
        result.Should().Contain("nocturne_api:R --> L:nocturne_web_bff");
        result.Should().NotContain("nocturne_api:R --> L:nocturne_web_frontend");
    }

    [Fact]
    public void Render_WebGroupHasFrontendToBffEdge()
    {
        var result = ArchitectureDiagramRenderer.Render(BuildModel());
        result.Should().Contain("nocturne_web_frontend:R --> L:nocturne_web_bff");
    }

    [Fact]
    public void Render_ContainerServicesHaveNoEdges()
    {
        // watchtower is ServiceKind.Container in the test model — it should appear as a node
        // but have no edge connections, so it doesn't obscure the core topology.
        var result = ArchitectureDiagramRenderer.Render(BuildModel());
        result.Should().Contain("watchtower");
        result.Should().NotMatchRegex(@"watchtower:|\bL:watchtower\b");
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
