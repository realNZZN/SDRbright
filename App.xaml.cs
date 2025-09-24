using SDRbright;
using System;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

namespace SDRbright
{
    // Handle startup, hotkeys, and system tray icon
    public partial class App : System.Windows.Application
    {
        private NotifyIcon? _NotifIcon;
        private HotkeyHandler? _HotkeyHandler;
        private BrightnessController? _BrightnessController;
        private OSD? _OSD;

        private int _CurrentBrightness = 50; // defaults to 50
        private int _StepSize = 10; // how much a hotkey will adjust brightness


        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _BrightnessController = new BrightnessController();

            // Try to get the current brighness
            try
            {
                _CurrentBrightness = await _BrightnessController.GetCurrentBrightnessAsync();
            }
            catch (Exception)
            {
                // if failed, the default is used
            }

            _HotkeyHandler = new HotkeyHandler();
            _HotkeyHandler.HotkeyPressed += OnHotkeyPressed;

            // Tray icon
            _NotifIcon = new NotifyIcon
            {
                Icon = LoadEmbeddedIcon(),
                Text = "SDRbright",
                Visible = true
            };
            // Context menu
            _NotifIcon.ContextMenuStrip = new ContextMenuStrip();
            _NotifIcon.ContextMenuStrip.Items.Add("Exit", null, OnExitClicked);

            // Register hotkeys
            _HotkeyHandler.Register(Key.OemPlus, ModifierKeys.Control | ModifierKeys.Windows);
            _HotkeyHandler.Register(Key.OemMinus, ModifierKeys.Control | ModifierKeys.Windows);
        }

        private Icon LoadEmbeddedIcon()
        {
            try
            {
                // Get the current running application
                var assembly = Assembly.GetExecutingAssembly();
                // Load the icon from the resource stream
                using (var stream = assembly.GetManifestResourceStream("SDRbright.icon.ico"))
                {
                    if (stream != null)
                    {
                        return new Icon(stream);
                    }
                }
            }
            catch (Exception ex)
            {

            }
            // Fall back to a default system icon
            return SystemIcons.Application;
        }


        private async void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
        {
            if (e.Key == Key.OemPlus)
            {
                _CurrentBrightness = Math.Min(100, _CurrentBrightness + _StepSize);
            }
            else if (e.Key == Key.OemMinus)
            {
                _CurrentBrightness = Math.Max(0, _CurrentBrightness - _StepSize);
            }

            // Create the OSD if it doesn't exist
            if (_OSD == null)
            {
                _OSD = new OSD();
                _OSD.Closed += (s, a) => _OSD = null;
            }
            _OSD.ShowOSD(_CurrentBrightness);

            // Apply brightness
            if (_BrightnessController != null)
            {
                // Await so rapid inputs are queued
                await _BrightnessController.SetBrightnessAsync(_CurrentBrightness);
            }
        }

        private void OnExitClicked(object? sender, EventArgs e)
        {
            _BrightnessController?.Dispose();
            _HotkeyHandler?.Dispose();
            _NotifIcon?.Dispose();
            this.Shutdown();
        }
    }
}
