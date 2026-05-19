using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Nocturne.Aspire.Publishing;

namespace Nocturne.Aspire.Host.Publishing;

internal sealed class MermaidDiagramPublisher(IOptions<PublishingOptions> options)
    : IDistributedApplicationPublisher
{
    public async Task PublishAsync(
        DistributedApplicationModel model,
        CancellationToken cancellationToken)
    {
        var outputPath = options.Value.OutputPath
            ?? throw new InvalidOperationException(
                "--output-path is required for the mermaid publisher");

        Directory.CreateDirectory(outputPath);

        var publishModel = AspireModelExtractor.Extract(model);

        var routing = RoutingDiagramRenderer.Render(publishModel);
        var architecture = ArchitectureDiagramRenderer.Render(publishModel);

        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "published-routing.mmd"),
            routing,
            cancellationToken);

        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "published-services.mmd"),
            architecture,
            cancellationToken);

        Console.WriteLine($"[mermaid-publisher] Wrote published-routing.mmd");
        Console.WriteLine($"[mermaid-publisher] Wrote published-services.mmd");
        Console.WriteLine($"[mermaid-publisher] Output: {outputPath}");
    }
}

public static class MermaidDiagramPublisherExtensions
{
    public static IDistributedApplicationBuilder AddMermaidDiagramPublisher(
        this IDistributedApplicationBuilder builder)
    {
        builder.Services.TryAddKeyedSingleton<IDistributedApplicationPublisher,
            MermaidDiagramPublisher>("mermaid");
        return builder;
    }
}
