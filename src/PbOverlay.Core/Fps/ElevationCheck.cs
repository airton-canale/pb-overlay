using System.Security.Principal;

namespace PbOverlay.Core.Fps;

public static class ElevationCheck
{
    public static bool IsProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
