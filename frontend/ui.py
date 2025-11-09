import webbrowser
import customtkinter as ctk
from vcolorpicker import getColor, useLightTheme
from collections import defaultdict
from tkinter import messagebox

from backend.config import (
    darker,
    ACCENT_COLOR,
    save_tweaks,
)
from .display_frame import DisplayFrame
from .dashboard_frame import InfoFrame

class TweakItemControl(ctk.CTkFrame):
    def __init__(self, master, item, all_data, **kwargs):
        super().__init__(master, **kwargs)
        self.item = item
        self.all_data = all_data
        self.grid_columnconfigure(0, weight=1)

        # Configures the name and description of the command
        ctk.CTkLabel(self, text=item['name'], font=ctk.CTkFont(weight="bold", size=14),
                     text_color=("black","white")).grid(row=0,column=0,padx=15,pady=(5,0),sticky="w")
        ctk.CTkLabel(self, text=item.get('purpose','No description.'), wraplength=450,
                     justify="left", fg_color="transparent", text_color=("gray30","gray70")
        ).grid(row=1,column=0,padx=15,pady=(0,5),sticky="w")

        # Configures the tweak slider 
        self.tweak_var = ctk.BooleanVar(value=item.get('enabled',False))
        self.tweak_switch = ctk.CTkSwitch(self, text="", variable=self.tweak_var,
                                          command=self.toggle_tweak, progress_color=ACCENT_COLOR)
        self.tweak_switch.grid(row=0,column=1,rowspan=2,padx=20,pady=10,sticky="e")

    def toggle_tweak(self):
        # Runs PowerShell command depending on toggle state (on/off)
        is_on = self.tweak_var.get()
        command = self.item.get(is_on,'')
        from backend.config import run_powershell_as_admin
        run_powershell_as_admin(command)
        self.item['enabled'] = is_on
        save_tweaks(self.all_data)


# Makes subtabs in the main tabs by category names
class SubTabView(ctk.CTkTabview):
    def __init__(self, master, categories_data, root_data, feature_name, **kwargs):
        super().__init__(master, **kwargs)
        hover_col = darker(ACCENT_COLOR,0.85)
        # Configures the tabs 
        self.configure(
            text_color=("black","white"),
            segmented_button_selected_color=(ACCENT_COLOR,ACCENT_COLOR),
            segmented_button_selected_hover_color=(hover_col,hover_col),
            segmented_button_unselected_color=("#E5E5E5","#2B2B2B"),
            segmented_button_unselected_hover_color=("#D5D5D5","#3B3B3B")
        )
        
        # Group items by category and create a tab for each category
        category_map = defaultdict(list)
        for cat_entry in categories_data:
            category_map[cat_entry['category']].extend(cat_entry.get('items',[]))
        for category_name, items in category_map.items():
            self.add(category_name)
            label = ctk.CTkLabel(self.tab(category_name), 
                                text="Please restart your computer after desired tweaks are set.",
                                text_color=("black","white")) # Changes color based on system theme
            label.pack(pady=(10,0))
            
            # Puts the commands inside their own category tab
            scroll_frame = ctk.CTkScrollableFrame(master=self.tab(category_name))
            scroll_frame.pack(fill="both", expand=True, padx=10, pady=10)
            for item in items:
                TweakItemControl(scroll_frame,item=item,all_data=root_data,
                                 fg_color=("white","gray15")).pack(fill="x",pady=5,padx=5)


class MainTabView(ctk.CTkTabview):
    def __init__(self, master, all_data, **kwargs):
        super().__init__(master, **kwargs)
        hover_col = darker(ACCENT_COLOR,0.85)
        # Configures the top-level tabs (main tweak categories)
        self.configure(
            text_color=("black","white"),
            segmented_button_selected_color=(ACCENT_COLOR,ACCENT_COLOR),
            segmented_button_selected_hover_color=(hover_col,hover_col),
            segmented_button_unselected_color=("#E5E5E5","#2B2B2B"),
            segmented_button_unselected_hover_color=("#D5D5D5","#3B3B3B")
        )
        # Loop through all tweak sections and create tabs for each
        for main_tab in all_data.get('tweaks',[]):
            tab_name = main_tab.get('feature')
            if not tab_name: continue
            self.add(tab_name)
            categories = main_tab.get('categories',[])
            if tab_name == "Display":
                display_frame = DisplayFrame(self.tab(tab_name))
                display_frame.pack(fill="both", expand=True)
            elif tab_name == "Dashboard":
                InfoFrame(self.tab(tab_name), dashboard_data=main_tab).pack(fill="both", expand=True, padx=5, pady=5)
            elif categories:
                # If there are categories, create subtabs for them
                SubTabView(self.tab(tab_name),categories,all_data,tab_name).pack(fill="both",expand=True,padx=5,pady=5)
            else:
                # Placeholder for upcoming sections
                ctk.CTkLabel(self.tab(tab_name),text=f"'{tab_name}' content coming soon...",
                             text_color=("black","white")).pack(pady=20,padx=20)

class AddTweakWindow(ctk.CTkToplevel):
    def __init__(self, parent):
        super().__init__(parent)
        self.title("Add tweaks")
        self.grab_set()
        self.geometry("500x255")
        self.resizable(False,False)

        self.update_idletasks()
        x = parent.winfo_x() + (parent.winfo_width() - self.winfo_width()) // 2
        y = parent.winfo_y() + (parent.winfo_height() - self.winfo_height()) // 2
        self.geometry(f"+{x}+{y}")

        hover_col = darker(ACCENT_COLOR,0.85)

        ctk.CTkLabel(self, text="Select category:").place(x=10, y=10)

        self.category_var = ctk.StringVar(value="Performance")
        self.category_menu = ctk.CTkOptionMenu(self,
            values=["Performance", "Security & Privacy", "Explorer & UI", "Extra"], 
            variable=self.category_var,
            width=180,
            fg_color=ACCENT_COLOR,
            button_color=ACCENT_COLOR,
            button_hover_color=hover_col
        )
        self.category_menu.place(x=150, y=10)

        self.input_name = self._create_entry(
            "Tweak name", 40, placeholder="Enter tweak name"
        )
        self.input_command = self._create_entry(
            "PowerShell command", 70, placeholder="Enter the full command to execute"
        )
        self.input_turn_off_command = self._create_entry(
            "Turn off command", 100, placeholder="Enter the command to undo the tweak"
        )

        ctk.CTkLabel(self, text="Command's purpose").place(x=10, y=130)
        
        self.input_description = ctk.CTkTextbox(self, width=340, height=85)
        self.input_description.place(x=150, y=130)
        self.input_description.insert("0.0", "Enter a brief description...")

        ctk.CTkButton(self, text="Add", fg_color=ACCENT_COLOR, hover_color=hover_col).place(x=175, y=220)

    def _create_entry(self, label, y, placeholder=""):
        ctk.CTkLabel(self,text=label).place(x=10,y=y)
        entry = ctk.CTkEntry(self, width=340, placeholder_text=placeholder)
        entry.place(x=150,y=y)
        return entry

class PowerTimer(ctk.CTkToplevel):
    def __init__(self,parent):
        super().__init__(parent)
        self.title("Power Scheduler")
        self.geometry("335x150")
        self.grab_set()
        self.resizable(False,False)
        
        # Center on parent
        self.update_idletasks()
        x = parent.winfo_x() + (parent.winfo_width() - self.winfo_width()) // 2
        y = parent.winfo_y() + (parent.winfo_height() - self.winfo_height()) // 2
        self.geometry(f"+{x}+{y}")

        # Create time input fields
        self.input_hour = self._create_entry("Hours",10)
        self.input_min = self._create_entry("Minutes",40)
        self.input_sec = self._create_entry("Seconds",70)

        # Create power management buttons
        hover_col = darker(ACCENT_COLOR,0.85)
        for text, cmd, x in [("Shutdown",self.shutdown,10),
                              ("Restart",self.restart,90),
                              ("BIOS",self.bios,170),
                              ("Cancel",self.destroy,250)]:
            ctk.CTkButton(self,text=text,command=cmd,width=75,
                          fg_color=ACCENT_COLOR, hover_color=hover_col).place(x=x,y=110)

    def _create_entry(self,label,y):
        # Creates label-entry pair for time input
        ctk.CTkLabel(self,text=label).place(x=10,y=y)
        entry = ctk.CTkEntry(self,width=255)
        entry.insert(0,"0")
        entry.place(x=70,y=y)
        return entry

    def get_total_seconds(self):
        # Returns total seconds from hour/min/sec fields
        try:
            return int(self.input_hour.get())*3600 + int(self.input_min.get())*60 + int(self.input_sec.get())
        except ValueError:
            messagebox.showerror("Error","Please enter valid numbers.")
            return None

    # Power control actions
    def shutdown(self): self._do("'-s','-f'")
    def restart(self): self._do("'-r','-f'")
    def bios(self): self._do("'-r','-fw'")

    def _do(self,args):
        # Executes the system shutdown/restart command after given delay
        total = self.get_total_seconds()
        if total is not None:
            from backend.config import run_powershell_as_admin
            run_powershell_as_admin(f"Start-Process shutdown -ArgumentList {args},'-t {total}'")
            self.destroy()


# --- Tooltip class ---
class Tooltip(ctk.CTkToplevel):
    def __init__(self,parent,text):
        super().__init__(parent)
        self.wm_overrideredirect(True)
        self.attributes("-topmost",True)
        self.configure(fg_color=("white","#333333"), corner_radius=12)
        # Tooltip text styling
        self.label = ctk.CTkLabel(self,text=text,text_color=("black","white"),
                                   fg_color=("white","#333333"))
        self.label.pack(padx=8,pady=4)
        self.withdraw()
    def show(self,x,y):
        # Displays tooltip at given coordinates
        self.geometry(f"+{x}+{y}")
        self.deiconify()
    def hide(self):
        # Hides tooltip
        self.withdraw()


def add_tooltip(widget,text):
    # Binds hover events to show/hide tooltip
    tip = Tooltip(widget,text)
    def on_enter(event):
        x = widget.winfo_rootx() + widget.winfo_width() + 5
        y = widget.winfo_rooty()
        tip.show(x,y)
    def on_leave(event):
        tip.hide()
    widget.bind("<Enter>",on_enter)
    widget.bind("<Leave>",on_leave)


class Winsane(ctk.CTk):
    def __init__(self, tweak_data):
        super().__init__()
        global ACCENT_COLOR
        if not tweak_data or 'tweaks' not in tweak_data:
            self.destroy(); return

        # Load theme data and accent color
        theme_data = tweak_data.get("theme", {})
        if isinstance(theme_data, dict):
            self.current_theme = theme_data.get("mode", "system")
            ACCENT_COLOR = theme_data.get("accent_color", ACCENT_COLOR)
        else:
            self.current_theme = theme_data if theme_data in ["dark", "light", "system"] else "system"

        # Apply theme
        ctk.set_appearance_mode(self.current_theme)
        useLightTheme(ctk.get_appearance_mode() == "Light")

        self.root_data = tweak_data
        self.title("Winsane")
        
        # Smooth startup animation
        self.attributes('-alpha', 0.0)
        self.update()
        self.state("zoomed")
        
        # Configure grid layout (sidebar + main area)
        self.grid_columnconfigure(0, weight=0, minsize=60)
        self.grid_columnconfigure(1, weight=1)
        self.grid_rowconfigure(0, weight=1)

        # --- Sidebar setup ---
        sidebar = ctk.CTkFrame(self, width=60, fg_color=("#EBEBEB","#242424"))
        sidebar.grid(row=0, column=0, sticky="nsw")
        sidebar.grid_propagate(False)
        sidebar.grid_rowconfigure(0, weight=1)
        sidebar.grid_rowconfigure(6, weight=1)

        # Sidebar buttons configuration
        btn_cfg = dict(width=40, height=40, font=ctk.CTkFont(size=14),
                       text_color=("black","white"), corner_radius=8,
                       fg_color=("#d0d0d0","#333333"), hover_color=("#c0c0c0","#444444"))

        # Sidebar buttons
        b_theme = ctk.CTkButton(sidebar, text="☼", command=self.toggle_theme, **btn_cfg)
        b_color = ctk.CTkButton(sidebar, text="🎨", command=self.pick_color, **btn_cfg)
        b_power = ctk.CTkButton(sidebar, text="⏻", command=lambda: PowerTimer(self), **btn_cfg)
        b_github = ctk.CTkButton(sidebar, text="🐱", command=self.open_github, **btn_cfg)
        b_add_tweaks = ctk.CTkButton(sidebar, text="➕", command =lambda: AddTweakWindow(self), **btn_cfg)

        # Place buttons vertically
        buttons = [b_theme, b_color, b_power, b_github, b_add_tweaks]
        for i, btn in enumerate(buttons, start=1):
            btn.grid(row=i, column=0, pady=5, padx=10)

        # Add tooltips to sidebar buttons
        add_tooltip(b_theme, "Theme")
        add_tooltip(b_color, "Accent Color")
        add_tooltip(b_power, "Power Scheduler")
        add_tooltip(b_github, "GitHub")
        add_tooltip(b_add_tweaks, "Add more Tweaks manually")

        # Create main tweak tab area
        MainTabView(self, tweak_data).grid(row=0, column=1, padx=(3, 60), pady=(10, 30), sticky="nsew")
        
        # Fade in animation
        self.fade_in()

    def fade_in(self):
        # Gradually increase window opacity (fade effect)
        for i in range(0, 11):
            self.attributes('-alpha', i/10)
            self.update()
            self.after(20)

    def toggle_theme(self):
        # Smooth fade-out before theme change
        for i in range(0, 11):
            self.attributes('-alpha', 1.0 - (i/10))
            self.update()
            self.after(40)
        
        # Switch theme mode
        self.current_theme = {"system":"dark","dark":"light","light":"system"}[self.current_theme]
        ctk.set_appearance_mode(self.current_theme)
        useLightTheme(ctk.get_appearance_mode() == "Light")
        useLightTheme(self.current_theme == "light")
        
        # Fade back in with new theme
        for i in range(0, 11):
            self.attributes('-alpha', i/10)
            self.update()
            self.after(40)
        
        # Save new theme settings
        if "theme" not in self.root_data:
            self.root_data["theme"] = {}
        self.root_data["theme"]["mode"] = self.current_theme
        save_tweaks(self.root_data)

    def pick_color(self):
        # Opens color picker and updates accent color
        global ACCENT_COLOR
        color = getColor()
        if not color or color == (0,0,0):
            return
        if isinstance(color, tuple) and len(color) == 3:
            ACCENT_COLOR = "#%02x%02x%02x" % tuple(map(int, color))
            if "theme" not in self.root_data or not isinstance(self.root_data.get("theme"), dict):
                self.root_data["theme"] = {}
            self.root_data["theme"]["accent_color"] = ACCENT_COLOR
            save_tweaks(self.root_data)
            self.refresh_accent()

    def refresh_accent(self):
        # Recursively update accent color on all widgets
        hover_col = darker(ACCENT_COLOR, 0.85)
        def update(widget):
            if isinstance(widget, ctk.CTkSwitch):
                widget.configure(progress_color=ACCENT_COLOR)
            elif isinstance(widget, ctk.CTkTabview):
                widget.configure(segmented_button_selected_color=(ACCENT_COLOR, ACCENT_COLOR),
                                 segmented_button_selected_hover_color=(hover_col, hover_col))
            elif isinstance(widget, ctk.CTkButton) and widget.cget("text") in ["Shutdown","Restart","BIOS"]:
                widget.configure(fg_color=ACCENT_COLOR, hover_color=hover_col)
            for w in widget.winfo_children():
                update(w)
        update(self)

    def open_github(self): 
        # Opens GitHub repo in browser
        webbrowser.open_new_tab("https://github.com/wirekurosastak/Winsane")