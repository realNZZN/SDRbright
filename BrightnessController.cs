using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UIAutomationClient;

namespace SDRbright
{

    // Handles interaction with the settings app to adjust brightness
    public class BrightnessController : IDisposable
    {
        // The current method of hiding the window places it offscreen.
        // The window seems to need to be able to render for the slider to be interactable
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

        private const int SW_HIDE = 0; //  Hide window
        private const uint SWP_NOSIZE = 0x0001; // Dont resize
        private const uint SWP_NOACTIVATE = 0x0010; // Dont bring to foreground

        private const string SliderAutomationId = "SystemSettings_Display_AdvancedColorBrightnessSlider_Slider";

        private readonly CUIAutomation _automation;
        private IUIAutomationElement? _CachedSlider;
        private readonly System.Timers.Timer _SettingsTimeout;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public BrightnessController()
        {
            _automation = new CUIAutomation();

            _SettingsTimeout = new System.Timers.Timer(5000);

            _SettingsTimeout.Elapsed += (s, e) => CloseSettingsWindow();
            _SettingsTimeout.AutoReset = false; // Only one timer can run
        }

        // Gets the current SDR brightness level from the Settings app.
        public async Task<int> GetCurrentBrightnessAsync()
        {
            await _lock.WaitAsync();
            try
            {
                // This runs on a background thread
                return await Task.Run(() =>
                {
                    try
                    {
                        IUIAutomationElement SettingsWindow = StartSystemSettings();
                        IUIAutomationElement Slider = FindSliderInWindow(SettingsWindow);
                        IUIAutomationRangeValuePattern rangePattern = (IUIAutomationRangeValuePattern)Slider.GetCurrentPattern(UIA_PatternIds.UIA_RangeValuePatternId);
                        int CurrentValue = (int)rangePattern.CurrentValue;
                        CloseWindow(SettingsWindow);
                        return CurrentValue;
                    }
                    catch (Exception)
                    {
                        CloseSettingsWindow();
                        return 50;
                    }
                });
            }
            finally
            {
                _lock.Release();
            }
        }

        // Use the slider to set brightness
        public async Task SetBrightnessAsync(int level)
        {
            await _lock.WaitAsync();
            try
            {
                // An adjustment was made, so stop the inactivity timer
                _SettingsTimeout.Stop();

                // Wait for the slider to be available
                await EnsureSliderIsAvailableAsync();
                if (_CachedSlider != null)
                {
                    IUIAutomationRangeValuePattern rangePattern = (IUIAutomationRangeValuePattern)_CachedSlider.GetCurrentPattern(UIA_PatternIds.UIA_RangeValuePatternId);
                    rangePattern.SetValue(level);
                    HideTooltip();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SetBrightnessAsync: {ex.Message}");
                // If an error occurs invalidate the cached slider
                _CachedSlider = null;
            }
            finally
            {
                _SettingsTimeout.Start();
                _lock.Release();
            }
        }


        // If the slider is not available, open the settings window and find it
        private async Task EnsureSliderIsAvailableAsync()
        {
            try
            {
                if (_CachedSlider != null)
                {
                    var a = _CachedSlider.CurrentName;
                    return;
                }
            }
            catch (COMException)
            {
                // Inavlidate slider since its unusable
                _CachedSlider = null;
            }

            // If slider is null, open settings and find it
            await Task.Run(() =>
            {
                IUIAutomationElement SettingsWindow = StartSystemSettings();
                _CachedSlider = FindSliderInWindow(SettingsWindow);
            });
        }

    
        // Start SystemSettings.exe at the HDR page
        private IUIAutomationElement StartSystemSettings()
        {
            // Create process info to launch the Settings URI and request it start hidden.
            var psi = new ProcessStartInfo { FileName = "ms-settings:display-hdr", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden };
            Process.Start(psi);

            // Find the window and immediately hide it more forcefully.
            IUIAutomationElement settingsWindow = FindSettingsWindow();
            HideWindow(new IntPtr(settingsWindow.CurrentNativeWindowHandle));
            return settingsWindow;
        }

        // Minimise the window and place it offscreen
        private void HideWindow(IntPtr hwnd)
        {
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, SW_HIDE);
                SetWindowPos(hwnd, IntPtr.Zero, -32000, -32000, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
            }
        }


        // Hide the tooltip that appears when the slider is adjusted
        private void HideTooltip()
        {
            IntPtr tooltipHwnd = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "tooltips_class32", null);
            if (tooltipHwnd != IntPtr.Zero)
            {
                ShowWindow(tooltipHwnd, SW_HIDE);
            }
        }

        // Wait for the settings window to become available
        private IUIAutomationElement FindSettingsWindow()
        {
            for (int i = 0; i < 1000; i++)
            {
                IUIAutomationElement rootElement = _automation.GetRootElement();
                if (rootElement != null)
                {
                    IUIAutomationCondition condition = _automation.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, "Settings");
                    IUIAutomationElement? topWindow = rootElement.FindFirst(TreeScope.TreeScope_Children, condition);
                    if (topWindow != null) return topWindow; // Success
                }
                Thread.Sleep(10);
            }
            throw new TimeoutException("Could not find settings window");
        }

   
        // Wait for the slider to become available
        private IUIAutomationElement FindSliderInWindow(IUIAutomationElement SettingsWindow)
        {
            for (int i = 0; i < 2000; i++)
            {
                IUIAutomationCondition condition = _automation.CreatePropertyCondition(UIA_PropertyIds.UIA_AutomationIdPropertyId, SliderAutomationId);
                IUIAutomationElement? slider = SettingsWindow.FindFirst(TreeScope.TreeScope_Descendants, condition);
                if (slider != null) return slider; // Success
                Thread.Sleep(5);
            }
            throw new TimeoutException("Could not find slider");
        }

        private void CloseWindow(IUIAutomationElement settingsWindow)
        {
            try
            {
                if (settingsWindow != null)
                {
                    IUIAutomationWindowPattern windowPattern = (IUIAutomationWindowPattern)settingsWindow.GetCurrentPattern(UIA_PatternIds.UIA_WindowPatternId);
                    windowPattern.Close();
                }
            }
            catch (COMException) { }
        }

        private void CloseSettingsWindow()
        {
            _CachedSlider = null;
            try
            {
                IUIAutomationElement rootElement = _automation.GetRootElement();
                IUIAutomationCondition condition = _automation.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, "Settings");
                IUIAutomationElement? SettingsWindow = rootElement.FindFirst(TreeScope.TreeScope_Children, condition);
                if (SettingsWindow != null)
                {
                    CloseWindow(SettingsWindow);
                }
            }
            catch (COMException) { }
        }

  
        public void Dispose()
        {
            _SettingsTimeout.Stop();
            _SettingsTimeout.Dispose();
            CloseSettingsWindow();
            _lock.Dispose();
        }
    }
}

