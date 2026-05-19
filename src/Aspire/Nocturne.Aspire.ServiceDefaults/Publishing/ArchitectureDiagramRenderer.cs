using System.Text;

namespace Nocturne.Aspire.Publishing;

public static class ArchitectureDiagramRenderer
{
    public static string Render(AspirePublishModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("architecture-beta");

        // Groups
        sb.AppendLine("    group external(cloud)[External]");
        sb.AppendLine("    group internal(server)[aspire network]");
        sb.AppendLine();

        // Internet node (always present — the external entry point)
        sb.AppendLine("    service internet(cloud)[Internet] in external");

        // Service nodes
        foreach (var svc in model.Services)
        {
            var group = svc.HostPorts.Count > 0 ? "external" : "internal";
            var icon = svc.Kind switch
            {
                ServiceKind.Database => "database",
                ServiceKind.Gateway  => "server",
                ServiceKind.Api      => "server",
                ServiceKind.Web      => "server",
                _                    => "disk",
            };
            var portSuffix = svc.HostPorts.Count > 0
                ? " :" + svc.HostPorts[0].HostPort
                : svc.InternalPorts.Count > 0
                    ? " :" + svc.InternalPorts[0]
                    : string.Empty;
            sb.AppendLine($"    service {NodeId(svc.Name)}({icon})[{svc.Name}{portSuffix}] in {group}");
        }

        sb.AppendLine();

        // Only emit edges where both endpoints are declared service nodes
        var declaredIds = new HashSet<string>(model.Services.Select(s => NodeId(s.Name)));
        declaredIds.Add("internet");

        // Internet → gateway
        var gateway = model.Services.FirstOrDefault(s => s.Kind == ServiceKind.Gateway);
        if (gateway != null && declaredIds.Contains(NodeId(gateway.Name)))
            sb.AppendLine($"    internet:R --> L:{NodeId(gateway.Name)}");

        // Reference edges only — both endpoints must be declared services (WaitFor adds noise)
        foreach (var edge in model.Edges.Where(e =>
            e.Kind == EdgeKind.Reference &&
            declaredIds.Contains(NodeId(e.From)) &&
            declaredIds.Contains(NodeId(e.To))))
            sb.AppendLine($"    {NodeId(edge.From)}:R --> L:{NodeId(edge.To)}");

        return sb.ToString().TrimEnd();
    }

    private static string NodeId(string name) => name.Replace("-", "_");
}
