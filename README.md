# SDRbright
A lightweight background utility for Windows 11 that provides laptop-style hotkeys with OSD to adjust the "SDR content brightness" on HDR-enabled displays

## Usage
Go to the Releases page

Download the latest SDRbright.exe

Run it. The application will live in your system tray.



`Ctrl+Win+=` Increase brightness by 10

`Ctrl+Win+-` Decrease brightness by 10

## How it works
The official SDR Brightness slider in Windows configures the SDR white level through a direct undocumented interface with the graphics driver and d3d12. Because there are no publicly available APIs for this purpose, this program has to use a workaround.
To adjust brightness, an instance of SystemSettings.exe is created in the background, and a UIAutomation interfaces with the brightness slider. The process is kept alive for 5 seconds after adjustment in case another is needed.

This is a very roundabout way of doing things, and any Windows update could break it, but it's the most reliable and only working method unless Microsoft can improve how poorly HDR is implemented in Windows right now.

## Requirements
x64 Windows 11
.NET 8.0 Runtime

## Known issues
- When adjusting brightness, the current level is shown in the top left of the screen for a few seconds. This is a tooltip created by SystemSettings
- After adjusting brightness, the SystemSettings.exe icon is shown in the taskbar
- SystemSettings can sometimes crash, causing brightness adjustments to not apply unless you kill its process.
As of right now there doesn't seem to be anything that can be done about these.

## Planned future functionality
- Run at startup option (right now you can use Task Scheduler)
- Config file (hotkeys, step size, settings process timeout, OSD scale/location)
- White level display in Nits (optional)

## Contributing
Use Visual Studio 2026 with the .NET 8.0 SDK

# Licensing
This project is licensed under the GNU GPLv3 License.
Icons adapted from microsoft/fluentui-system-icons
