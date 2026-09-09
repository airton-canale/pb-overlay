using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PbOverlay.Core.Capture;

/// <summary>
/// Enumerates top-level windows and looks up their owning process. Uses
/// user32/kernel32 APIs that only read from the OS window/process table —
/// no <c>OpenProcess</c>, no handle to any other process is ever opened.
/// </summary>
public static class WindowEnumerator
{
    public sealed record WindowInfo(IntPtr Hwnd, string Title, int ProcessId, string ProcessName);

    public static IReadOnlyList<WindowInfo> EnumerateVisibleWindows()
    {
        var results = new List<WindowInfo>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
                return true;

            var length = GetWindowTextLength(hwnd);
            if (length <= 0)
                return true;

            var buf = new StringBuilder(length + 1);
            GetWindowText(hwnd, buf, buf.Capacity);
            var title = buf.ToString();
            if (string.IsNullOrWhiteSpace(title))
                return true;

            // Pure OS lookup: window -> owning PID. Opens no process handle.
            GetWindowThreadProcessId(hwnd, out var pid);

            string procName;
            try
            {
                // Process.GetProcessById reads from the system snapshot; it does
                // not require PROCESS_VM_READ or PROCESS_QUERY_INFORMATION on
                // protected processes for the name field on modern Windows.
                using var p = Process.GetProcessById((int)pid);
                procName = p.ProcessName;
            }
            catch
            {
                procName = string.Empty;
            }

            results.Add(new WindowInfo(hwnd, title, (int)pid, procName));
            return true;
        }, IntPtr.Zero);

        return results;
    }

    /// <summary>
    /// Match by case-insensitive title substring AND exact process name. Both
    /// must match, so a browser tab or Discord channel titled the same as the
    /// game cannot be picked up by accident.
    /// </summary>
    public static WindowInfo? FindTarget(IEnumerable<WindowInfo> windows, string titleSubstring, string processName)
    {
        foreach (var w in windows)
        {
            if (!w.Title.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(w.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                continue;
            return w;
        }
        return null;
    }

    public static bool IsForeground(IntPtr hwnd) => GetForegroundWindow() == hwnd;

    #region P/Invoke
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    #endregion
}
