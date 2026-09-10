using System.Reflection;
using Helpdesk.Shared.Build;

namespace HelpDesk.NewWeb.Components.Layout;

public static class AppBarVersionResolver
{
    public static BuildInfo ResolveDetails(Assembly? assembly = null, string environment = "unknown")
    {
        return BuildInfoProvider.FromAssembly(assembly ?? typeof(AppBarVersionResolver).Assembly, environment);
    }
}
