import subprocess
from dataclasses import dataclass

try:
    import win32api
    import win32con
    import pywintypes
    if not hasattr(win32con, 'DISPLAY_DEVICE_ACTIVE'):
        win32con.DISPLAY_DEVICE_ACTIVE = 0x00000001
    PYWIN32_AVAILABLE = True
except ImportError:
    PYWIN32_AVAILABLE = False

@dataclass
class DisplayMode:
    width: int
    height: int
    frequency: int

    def __hash__(self):
        return hash((self.width, self.height, self.frequency))

@dataclass
class Monitor:
    name: str
    id: str
    width: int
    height: int
    x: int
    y: int
    is_primary: bool
    canvas_rect_id: int = None
    canvas_text_id: int = None

class DisplayManager:
    def __init__(self):
        self.projection_modes = {
            "PC screen only": "/internal",
            "Duplicate": "/clone",
            "Extend": "/extend",
            "Second screen only": "/external"
        }

    def is_available(self) -> bool:
        """Check if pywin32 module is available."""
        return PYWIN32_AVAILABLE

    def get_projection_options(self) -> list[str]:
        """Return the names of the projection modes for the GUI."""
        return list(self.projection_modes.keys())

    def set_projection_mode(self, mode_name: str) -> tuple[bool, str]:
        """
        Set the projection mode by calling DisplaySwitch.exe.
        Returns a (success, message) tuple.
        """
        switch = self.projection_modes.get(mode_name)
        if not switch:
            return False, "Unknown projection mode."
        try:
            # Run DisplaySwitch.exe with the appropriate switch
            subprocess.run(["DisplaySwitch.exe", switch], 
                           check=True, 
                           capture_output=True, 
                           text=True, 
                           creationflags=subprocess.CREATE_NO_WINDOW)
            return True, "Projection mode changed successfully."
        except (subprocess.CalledProcessError, FileNotFoundError) as e:
            return False, f"Could not switch display mode.\n{e}"

    def get_monitor_layout(self) -> list[Monitor]:
        """
        Retrieve all active monitors and their layout information.
        """
        if not PYWIN32_AVAILABLE:
            return []
            
        monitors = []
        i = 0
        while True:
            try:
                # Enumerate display devices
                device = win32api.EnumDisplayDevices(None, i, 0)
                if device.StateFlags & win32con.DISPLAY_DEVICE_ACTIVE:
                    # Get current display settings for active device
                    devmode = win32api.EnumDisplaySettings(device.DeviceName, win32con.ENUM_CURRENT_SETTINGS)
                    monitors.append(Monitor(
                        name=device.DeviceName,
                        id=device.DeviceString,
                        width=devmode.PelsWidth,
                        height=devmode.PelsHeight,
                        x=devmode.Position_x,
                        y=devmode.Position_y,
                        # Primary monitor is at (0, 0)
                        is_primary=(devmode.Position_x == 0 and devmode.Position_y == 0)
                    ))
                i += 1
            except pywintypes.error:
                # Stop enumeration when pywintypes.error is raised
                break
        return monitors

    def list_display_modes(self, device_name: str) -> list[DisplayMode]:
        """Get all available display modes (resolution, frequency) for a given monitor."""
        if not PYWIN32_AVAILABLE:
            return []
            
        modes = []
        i = 0
        while True:
            try:
                # Enumerate all possible display settings
                devmode = win32api.EnumDisplaySettingsEx(device_name, i, 0)
                modes.append(DisplayMode(
                    width=devmode.PelsWidth,
                    height=devmode.PelsHeight,
                    frequency=devmode.DisplayFrequency
                ))
                i += 1
            except pywintypes.error:
                # Stop enumeration when pywintypes.error is raised
                break
        # Filter duplicates and sort
        return sorted(list(set(modes)), key=lambda m: (m.width, m.height, m.frequency), reverse=True)

    def apply_settings(self, monitor_name: str, width: int, height: int, frequency: int) -> tuple[bool, str]:
        """
        Apply the selected display settings (resolution and frequency) to a specific monitor.
        """
        if not PYWIN32_AVAILABLE:
            return False, "pywin32 is not available."

        # Create a new DEVMODE structure
        devmode = pywintypes.DEVMODEType()
        devmode.PelsWidth = width
        devmode.PelsHeight = height
        devmode.DisplayFrequency = frequency
        # Specify which fields to change
        devmode.Fields = win32con.DM_PELSWIDTH | win32con.DM_PELSHEIGHT | win32con.DM_DISPLAYFREQUENCY

        # Apply the new settings
        result = win32api.ChangeDisplaySettingsEx(monitor_name, devmode, 0)

        if result == win32con.DISP_CHANGE_SUCCESSFUL:
            return True, "Display settings changed successfully."
        else:
            return False, f"Failed to change display settings. Error code: {result}"