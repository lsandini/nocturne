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

        // Web services are split into a BFF + Frontend subgroup; edges target the BFF
        var webNames = model.Services
            .Where(s => s.Kind == ServiceKind.Web)
            .Select(s => s.Name)
            .ToHashSet();

        // Service nodes
        foreach (var svc in model.Services)
        {
            var group = svc.HostPorts.Count > 0 ? "external" : "internal";
            var portSuffix = svc.HostPorts.Count > 0
                ? " " + svc.HostPorts[0].HostPort
                : svc.InternalPorts.Count > 0
                    ? " " + svc.InternalPorts[0]
                    : string.Empty;
            var label = svc.Name.Replace("-", " ");

            if (svc.Kind == ServiceKind.Web)
            {
                // Render as a nested group with BFF and Frontend child services so the
                // SvelteKit server-side (BFF) and client bundle are architecturally distinct.
                var groupId = NodeId(svc.Name) + "_group";
                sb.AppendLine($"    group {groupId}(server)[{label}{portSuffix}] in {group}");
                sb.AppendLine($"    service {NodeId(svc.Name)}_bff(server)[BFF] in {groupId}");
                sb.AppendLine($"    service {NodeId(svc.Name)}_frontend(disk)[Frontend] in {groupId}");
            }
            else
            {
                var icon = svc.Kind switch
                {
                    ServiceKind.Database => "database",
                    ServiceKind.Gateway  => "server",
                    ServiceKind.Api      => "server",
                    _                    => "disk",
                };
                sb.AppendLine($"    service {NodeId(svc.Name)}({icon})[{label}{portSuffix}] in {group}");
            }
        }

        sb.AppendLine();

        // All service names that have visual representation in the diagram (used for edge filtering)
        var representedNames = new HashSet<string>(model.Services.Select(s => s.Name));
        representedNames.Add("internet");

        // Edge node IDs:
        // - Outbound from web service → BFF (the server process makes the call)
        // - Inbound to web service → Frontend (the user-facing entry point receives the request)
        string EdgeFromId(string name) => webNames.Contains(name) ? NodeId(name) + "_bff" : NodeId(name);
        string EdgeToId(string name)   => webNames.Contains(name) ? NodeId(name) + "_frontend" : NodeId(name);

        // Internet → gateway
        var gateway = model.Services.FirstOrDefault(s => s.Kind == ServiceKind.Gateway);
        if (gateway != null)
            sb.AppendLine($"    internet:R --> L:{EdgeToId(gateway.Name)}");

        // Reference edges only — both endpoints must be represented services (WaitFor adds noise)
        foreach (var edge in model.Edges.Where(e =>
            e.Kind == EdgeKind.Reference &&
            representedNames.Contains(e.From) &&
            representedNames.Contains(e.To)))
            sb.AppendLine($"    {EdgeFromId(edge.From)}:R --> L:{EdgeToId(edge.To)}");

        // Within each web group: frontend routes inbound requests through to the BFF
        foreach (var webSvc in model.Services.Where(s => s.Kind == ServiceKind.Web))
            sb.AppendLine($"    {NodeId(webSvc.Name)}_frontend:R --> L:{NodeId(webSvc.Name)}_bff");

        return sb.ToString().TrimEnd();
    }

    private static string NodeId(string name) => name.Replace("-", "_");
}
