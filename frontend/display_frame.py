import customtkinter as ctk
from tkinter import messagebox
from backend.display_manager import DisplayManager, Monitor, DisplayMode 
from backend.config import darker

try:
    import win32api
    import win32con
    import pywintypes
    PYWIN32_AVAILABLE = True
except ImportError:
    PYWIN32_AVAILABLE = False

class DisplayFrame(ctk.CTkFrame):
    def __init__(self, master, **kwargs):
        super().__init__(master, **kwargs)
        self.configure(fg_color="transparent")

        self.display_manager = DisplayManager()

        # Access the root application data
        self.root_app = master.master.master 

        self.monitors: list[Monitor] = []
        self.selected_monitor: Monitor | None = None
        # Map resolution string to a set of available refresh rates (e.g., "1920x1080": {60, 144})
        self.mode_map: dict[str, set[int]] = {}
        
        self.grid_columnconfigure(0, weight=1)
        self.grid_columnconfigure(1, weight=0)
        self.grid_rowconfigure(0, weight=1)

        # 1. Canvas Frame for monitor visualization
        canvas_frame = ctk.CTkFrame(self)
        canvas_frame.grid(row=0, column=0, padx=10, pady=10, sticky="nsew")
        canvas_frame.grid_rowconfigure(0, weight=1)
        canvas_frame.grid_columnconfigure(0, weight=1)
        
        current_mode = ctk.get_appearance_mode() # "Light" or "Dark"
        canvas_bg_color = "gray85" if current_mode == "Light" else "gray20"
        
        self.canvas = ctk.CTkCanvas(canvas_frame, bg=canvas_bg_color, highlightthickness=0)
        
        self.canvas.grid(row=0, column=0, sticky="nsew")
        # Handle monitor selection on click
        self.canvas.bind("<Button-1>", self.on_canvas_click)
        # Redraw layout on resize
        self.canvas.bind("<Configure>", self.on_canvas_resize) 

        # 2. Settings Panel
        self.settings_frame = ctk.CTkFrame(self, width=250)
        self.settings_frame.grid(row=0, column=1, padx=(0, 10), pady=10, sticky="ns")
        self.settings_frame.grid_propagate(False)
        self.settings_frame.grid_columnconfigure(0, weight=1)

        # Get accent colors from root app data
        initial_color = self.root_app.root_data.get("theme", {}).get("accent_color", "#3B8ED0")
        initial_hover = darker(initial_color, 0.85)
        
        menu_colors = {
            "fg_color": initial_color,
            "button_color": initial_color,
            "button_hover_color": initial_hover,
            "text_color": ("black", "white"),
            "dropdown_text_color": ("black", "white")
        }

        # 2a. Projection Settings
        ctk.CTkLabel(self.settings_frame, text="Projection Mode", font=ctk.CTkFont(weight="bold")).grid(row=0, column=0, padx=10, pady=(10,5), sticky="w")
        self.projection_var = ctk.StringVar(value="Extend")
        self.projection_menu = ctk.CTkOptionMenu(self.settings_frame, 
                                                 variable=self.projection_var, 
                                                 values=self.display_manager.get_projection_options(), 
                                                 command=self.on_projection_change,
                                                 **menu_colors)
        self.projection_menu.grid(row=1, column=0, padx=10, pady=(0, 10), sticky="ew")

        # 2b. Individual Monitor Settings
        self.selected_monitor_label = ctk.CTkLabel(self.settings_frame, text="Select a monitor", font=ctk.CTkFont(weight="bold"), wraplength=230)
        self.selected_monitor_label.grid(row=2, column=0, padx=10, pady=(20, 5), sticky="w")

        self.resolution_var = ctk.StringVar()
        self.resolution_menu = ctk.CTkOptionMenu(self.settings_frame, 
                                                 variable=self.resolution_var, 
                                                 values=[], 
                                                 command=self.on_resolution_change,
                                                 **menu_colors)
        self.resolution_menu.grid(row=3, column=0, padx=10, pady=5, sticky="ew")

        self.refresh_rate_var = ctk.StringVar()
        self.refresh_rate_menu = ctk.CTkOptionMenu(self.settings_frame, 
                                                 variable=self.refresh_rate_var, 
                                                 values=[],
                                                 **menu_colors)
        self.refresh_rate_menu.grid(row=4, column=0, padx=10, pady=5, sticky="ew")

        self.apply_button = ctk.CTkButton(self.settings_frame, text="Apply", 
                                          command=self.apply_settings,
                                          fg_color=initial_color,
                                          hover_color=initial_hover,
                                          text_color=("black", "white"))
        self.apply_button.grid(row=5, column=0, padx=10, pady=10, sticky="ew")


        # Initial setup check
        if not self.display_manager.is_available():
            ctk.CTkLabel(self.canvas, text="pywin32 library not found.").place(relx=0.5, rely=0.5, anchor="center")
            self.settings_frame.grid_remove()
        else:
            self.toggle_monitor_settings(False)
            # Refresh layout after a short delay to ensure canvas size is correct
            self.after(100, self.refresh_monitor_layout)

    def toggle_monitor_settings(self, enabled: bool):
        """Enable or disable the resolution/rate settings widgets."""
        state = "normal" if enabled else "disabled"
        self.resolution_menu.configure(state=state)
        self.refresh_rate_menu.configure(state=state)
        self.apply_button.configure(state=state)
        if not enabled:
            self.selected_monitor_label.configure(text="Select a monitor")
            self.resolution_var.set("Resolution")
            self.refresh_rate_var.set("Refresh Rate")

    def on_canvas_resize(self, event):
        """Handle canvas resize by redrawing the monitor layout."""
        self.draw_monitor_layout()

    def on_projection_change(self, mode_name: str):
        """Attempt to change the overall projection mode (e.g., Extend, Duplicate)."""
        success, message = self.display_manager.set_projection_mode(mode_name)
        if not success:
            messagebox.showerror("Error", message)
        # Refresh the layout after a delay to reflect changes
        self.after(1000, self.refresh_monitor_layout)

    def refresh_monitor_layout(self):
        """Get the current monitor data and redraw the canvas."""
        self.monitors = self.display_manager.get_monitor_layout()
        # Deselect monitor if it's no longer present
        if self.selected_monitor and self.selected_monitor not in self.monitors:
             self.select_monitor(None)
        self.draw_monitor_layout()

    def draw_monitor_layout(self):
        """Draw the monitor rectangles and labels on the canvas."""
        self.canvas.delete("all")
        if not self.monitors: 
            return

        # Calculate canvas scaling and offset
        all_x = [m.x for m in self.monitors]
        all_y = [m.y for m in self.monitors]
        min_x, max_x = min(all_x), max(all_x)
        min_y, max_y = min(all_y), max(all_y)
        
        total_width = max_x - min_x + max(m.width for m in self.monitors)
        total_height = max_y - min_y + max(m.height for m in self.monitors)

        canvas_w = self.canvas.winfo_width()
        canvas_h = self.canvas.winfo_height()
        # Fallback for initial sizing
        if canvas_w <= 1 or canvas_h <= 1: 
            canvas_w, canvas_h = 600, 400 

        padding = 40
        gap = 5
        
        # Determine scale factor
        if total_width <= 0 or total_height <= 0:
            scale = 1
        else:
            scale = min((canvas_w - 2 * padding) / total_width, (canvas_h - 2 * padding) / total_height) 

        current_accent = self.root_app.root_data.get("theme", {}).get("accent_color", "#3B8ED0")
        
        current_mode = ctk.get_appearance_mode()
        
        text_fill_color = "black" if current_mode == "Light" else "white"
        
        # Color configuration based on theme
        if current_mode == "Light":
            default_outline = "black"
            selected_fill = "#D0D0D0"
            default_fill = "#E0E0E0"
        else: # Dark mode
            default_outline = "white"
            selected_fill = "#5A5A5A"
            default_fill = "#4A4A4A"

        # Draw each monitor
        for i, monitor in enumerate(self.monitors):
            # Convert monitor coordinates to canvas coordinates
            x1 = (monitor.x - min_x) * scale + padding
            y1 = (monitor.y - min_y) * scale + padding
            scaled_width = max(1, (monitor.width * scale) - gap)
            scaled_height = max(1, (monitor.height * scale) - gap)
            x2 = x1 + scaled_width
            y2 = y1 + scaled_height

            outline_color = current_accent if monitor == self.selected_monitor else default_outline
            fill_color = selected_fill if monitor == self.selected_monitor else default_fill
            tag = f"monitor_{i}"
            
            # Draw rectangle
            monitor.canvas_rect_id = self.canvas.create_rectangle(x1, y1, x2, y2, fill=fill_color, outline=outline_color, width=2, tags=tag)
            # Draw label
            monitor.canvas_text_id = self.canvas.create_text(x1 + (x2-x1)/2, y1 + (y2-y1)/2, 
                                                             text=f"{i+1}\n{monitor.width}x{monitor.height}", 
                                                             fill=text_fill_color,
                                                             justify="center", tags=tag)

    def on_canvas_click(self, event):
        """Handle click events on the canvas to select a monitor."""
        item = self.canvas.find_withtag("current")
        if not item: 
            self.select_monitor(None)
            return
            
        tags = self.canvas.gettags(item[0])
        for tag in tags:
            if tag.startswith("monitor_"):
                try:
                    index = int(tag.split('_')[1])
                    if 0 <= index < len(self.monitors):
                        self.select_monitor(self.monitors[index])
                except (ValueError, IndexError):
                    pass
                return

    def select_monitor(self, monitor: Monitor | None):
        """Update the selected monitor and refresh settings panel."""
        self.selected_monitor = monitor
        self.draw_monitor_layout() # Redraw to highlight the selection

        if monitor is None:
            self.toggle_monitor_settings(False)
            return

        self.selected_monitor_label.configure(text=f"Display {self.monitors.index(monitor) + 1}: {monitor.id}")

        # Get all modes for the selected monitor
        all_modes = self.display_manager.list_display_modes(monitor.name)
        if not all_modes:
            messagebox.showwarning("Warning", "Could not retrieve display modes for this monitor.")
            self.toggle_monitor_settings(False)
            return

        # Build map of resolution strings to available refresh rates
        self.mode_map = {}
        for mode in all_modes:
            res_str = f"{mode.width}x{mode.height}"
            if res_str not in self.mode_map:
                self.mode_map[res_str] = set()
            self.mode_map[res_str].add(mode.frequency)

        # Populate resolution menu
        res_strings = sorted(self.mode_map.keys(), key=lambda r: (int(r.split('x')[0]), int(r.split('x')[1])), reverse=True)
        self.resolution_menu.configure(values=res_strings)

        # Set current resolution
        current_res_str = f"{monitor.width}x{monitor.height}"
        if current_res_str in res_strings:
            self.resolution_var.set(current_res_str)
        elif res_strings:
            self.resolution_var.set(res_strings[0])
        else:
            self.resolution_var.set("Resolution")

        # Trigger refresh rate population
        self.on_resolution_change(self.resolution_var.get())
        self.toggle_monitor_settings(True)

    def on_resolution_change(self, selected_res_str: str):
        """Update the refresh rate menu based on the selected resolution."""
        rates = self.mode_map.get(selected_res_str, set())
        hz_strings = [f"{hz} Hz" for hz in sorted(list(rates), reverse=True)]
        
        self.refresh_rate_menu.configure(values=hz_strings)
        if hz_strings:
            current_hz_str = ""
            # Try to get the monitor's current refresh rate
            if self.selected_monitor and f"{self.selected_monitor.width}x{self.selected_monitor.height}" == selected_res_str:
                try:
                    devmode = win32api.EnumDisplaySettings(self.selected_monitor.name, win32con.ENUM_CURRENT_SETTINGS)
                    current_hz_str = f"{devmode.DisplayFrequency} Hz"
                except pywintypes.error:
                    pass

            # Set the current or highest refresh rate as default
            if current_hz_str in hz_strings:
                 self.refresh_rate_var.set(current_hz_str)
            else:
                 self.refresh_rate_var.set(hz_strings[0])
        else:
            self.refresh_rate_var.set("Refresh Rate")

    def apply_settings(self):
        """Apply the selected resolution and refresh rate to the monitor."""
        if self.selected_monitor is None:
            return

        res_str = self.resolution_var.get()
        hz_str = self.refresh_rate_var.get()

        try:
            # Parse width, height, and frequency
            width, height = map(int, res_str.split('x'))
            frequency = int(hz_str.split(' ')[0])
        except (ValueError, IndexError):
            messagebox.showerror("Error", "Invalid resolution or refresh rate selected.")
            return

        success, message = self.display_manager.apply_settings(
            self.selected_monitor.name, width, height, frequency
        )

        if success:
            # Refresh layout to show updated current settings
            self.after(100, self.refresh_monitor_layout)
        else:
            messagebox.showerror("Error", message)

    def refresh_accent(self, new_accent_color):
        """Update the colors of all accent-dependent widgets and redraw the canvas."""
        hover_color = darker(new_accent_color, 0.85)
        
        menu_colors = {
            "fg_color": new_accent_color,
            "button_color": new_accent_color,
            "button_hover_color": hover_color,
            "text_color": ("black", "white"),
            "dropdown_text_color": ("black", "white")
        }
        
        # Configure option menus
        self.projection_menu.configure(**menu_colors)
        self.resolution_menu.configure(**menu_colors)
        self.refresh_rate_menu.configure(**menu_colors)

        # Configure apply button
        self.apply_button.configure(fg_color=new_accent_color, hover_color=hover_color, 
                                     text_color=("black", "white"))
        
        # Configure canvas background
        current_mode = ctk.get_appearance_mode()
        canvas_bg_color = "gray85" if current_mode == "Light" else "gray20"
        self.canvas.configure(bg=canvas_bg_color)
        
        # Redraw monitors with new accent color for selected monitor
        self.draw_monitor_layout()