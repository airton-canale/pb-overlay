using System.Threading;

namespace PbOverlay.App;

/// <summary>
/// Named-mutex based single-instance guard. Cross-session global so an
/// elevated and a non-elevated copy don't both start.
/// </summary>
internal static class SingleInstance
{
    public static bool TryAcquire(string mutexName, out Mutex? mutex)
    {
        var m = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out var createdNew);
        if (!createdNew)
        {
            m.Dispose();
            mutex = null;
            return false;
        }
        mutex = m;
        return true;
    }
}
