using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace PbOverlay.App.Overlay;

public sealed class HotkeyService : IDisposable
{
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly HwndSourceHook _hook;
    private readonly Dictionary<int, Action> _callbacks = new();
    private int _nextId = 1;
    private bool _disposed;

    public HotkeyService(Window host)
    {
        // Ensure the HWND exists before hooking.
        var helper = new WindowInteropHelper(host);
        helper.EnsureHandle();
        _hwnd = helper.Handle;

        var src = HwndSource.FromHwnd(_hwnd)
            ?? throw new InvalidOperationException("HwndSource unavailable for host window.");
        _source = src;
        _hook = WndProc;
        _source.AddHook(_hook);
    }

    public int Register(string keyString, Action onPressed)
    {
        if (string.IsNullOrWhiteSpace(keyString))
        {
            throw new ArgumentException("Hotkey string is empty.", nameof(keyString));
        }

        (uint mods, uint vk) = Parse(keyString);
        int id = _nextId++;
        if (!NativeMethods.RegisterHotKey(_hwnd, id, mods, vk))
        {
            throw new InvalidOperationException($"RegisterHotKey failed for '{keyString}'.");
        }
        _callbacks[id] = onPressed;
        return id;
    }

    public void Unregister(int id)
    {
        if (_callbacks.Remove(id))
        {
            NativeMethods.UnregisterHotKey(_hwnd, id);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        foreach (int id in _callbacks.Keys)
        {
            NativeMethods.UnregisterHotKey(_hwnd, id);
        }
        _callbacks.Clear();
        _source.RemoveHook(_hook);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_callbacks.TryGetValue(id, out var cb))
            {
                cb();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private static (uint mods, uint vk) Parse(string keyString)
    {
        uint mods = 0;
        string? keyToken = null;

        foreach (string raw in keyString.Split('+'))
        {
            string token = raw.Trim();
            if (token.Length == 0)
            {
                continue;
            }

            switch (token.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    mods |= MOD_CONTROL;
                    break;
                case "shift":
                    mods |= MOD_SHIFT;
                    break;
                case "alt":
                    mods |= MOD_ALT;
                    break;
                case "win":
                case "windows":
                    mods |= MOD_WIN;
                    break;
                default:
                    keyToken = token;
                    break;
            }
        }

        if (keyToken is null)
        {
            throw new ArgumentException($"No key found in hotkey '{keyString}'.", nameof(keyString));
        }

        if (!Enum.TryParse<Key>(keyToken, ignoreCase: true, out var key) || key == Key.None)
        {
            throw new ArgumentException($"Unknown key '{keyToken}' in hotkey '{keyString}'.", nameof(keyString));
        }

        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0)
        {
            throw new ArgumentException($"Key '{keyToken}' has no virtual-key mapping.", nameof(keyString));
        }

        return (mods, (uint)vk);
    }
}
