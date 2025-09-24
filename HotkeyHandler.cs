using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

public class HotkeyHandler : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Hotkey identifier
    private const int WM_HOTKEY = 0x0312;

    // Become available to recieve hotkey signals
    private readonly HwndSource _source;
    private int _HotkeyIdCounter = 9000;
    private readonly Dictionary<int, (Key Key, ModifierKeys Modifiers)> _RegisteredHotkeys = new();

    public event EventHandler<HotkeyEventArgs>? HotkeyPressed;

    public HotkeyHandler()
    {
        // Create and hook into signal reciever
        _source = new HwndSource(new HwndSourceParameters());
        _source.AddHook(HwndHook);
    }

    public void Register(Key key, ModifierKeys modifiers)
    {
        _HotkeyIdCounter++;
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (RegisterHotKey(_source.Handle, _HotkeyIdCounter, (uint)modifiers, vk))
        {
            // Store the hotkey pressed
            _RegisteredHotkeys.Add(_HotkeyIdCounter, (key, modifiers));
        }
    }


    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            // If registered hotkey
            if (_RegisteredHotkeys.TryGetValue(id, out var hotkey))
            {
                HotkeyPressed?.Invoke(this, new HotkeyEventArgs(hotkey.Key, hotkey.Modifiers));
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _RegisteredHotkeys.Keys)
        {
            UnregisterHotKey(_source.Handle, id);
        }

        _source.RemoveHook(HwndHook);
        _source.Dispose();
    }
}

public class HotkeyEventArgs : EventArgs
{
    public Key Key { get; }
    public ModifierKeys Modifiers { get; }
    public HotkeyEventArgs(Key key, ModifierKeys modifiers)
    {
        Key = key;
        Modifiers = modifiers;
    }
}

