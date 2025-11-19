import webbrowser
import customtkinter as ctk
from vcolorpicker import getColor, useLightTheme
from collections import defaultdict
from tkinter import messagebox
 
from backend.config import (
    darker,
    ACCENT_COLOR,
    save_config,
    add_user_tweak,
    delete_user_tweak # Import for deletion
)
from .dashboard_frame import InfoFrame
 
class TweakItemControl(ctk.CTkFrame):
    """A single Tweak item row, with a name, description, and switch."""
    def __init__(self, master, item, config_data, is_user_tweak=False, **kwargs):
        super().__init__(master, **kwargs)
        self.item = item
        self.config_data = config_data
        self.is_user_tweak = is_user_tweak # Store flag
        self.grid_columnconfigure(0, weight=1)
 
        # Name and description
        ctk.CTkLabel(self, text=item['name'], font=ctk.CTkFont(weight="bold", size=14),
                     text_color=("black","white")).grid(row=0,column=0,padx=15,pady=(5,0),sticky="w")
        ctk.CTkLabel(self, text=item.get('purpose','No description.'), wraplength=450,
                     justify="left", fg_color="transparent", text_color=("gray30","gray70")
        ).grid(row=1,column=0,padx=15,pady=(0,5),sticky="w")
 
        # Tweak switch
        self.tweak_var = ctk.BooleanVar(value=item.get('enabled',False))
        self.tweak_switch = ctk.CTkSwitch(self, text="", variable=self.tweak_var,
                                         command=self.toggle_tweak, progress_color=ACCENT_COLOR)
        
        # Add delete button if 'is_user_tweak' is true
        if self.is_user_tweak:
            # User tweak switch takes less space
            self.tweak_switch.grid(row=0, column=1, rowspan=2, padx=(20, 5), pady=10, sticky="e")
            
            # Delete button (trash icon)
            self.delete_button = ctk.CTkButton(
                self, 
                text="Delete",
                height=35,
                fg_color="transparent", 
                hover_color=("#F0D0D0", "#402020"), # Light red hover
                text_color=("#D00000", "#FF4040"), # Red text/icon
                font=ctk.CTkFont(size=16),
                command=self.on_delete_press # Call new method
            )
            self.delete_button.grid(row=0, column=2, rowspan=2, padx=(0, 15), pady=10, sticky="e")
        else:
            # Original layout for built-in tweaks
            self.tweak_switch.grid(row=0,column=1,rowspan=2,padx=20,pady=10,sticky="e")
 
    def toggle_tweak(self):
        # Run PowerShell command on toggle
        is_on = self.tweak_var.get()
        # Get command from item using boolean key (True/False)
        command = self.item.get(is_on,'')
        from backend.config import run_powershell_as_admin
        run_powershell_as_admin(command)
        self.item['enabled'] = is_on
        save_config(self.config_data)
        
    # New method to handle delete button
    def on_delete_press(self):
        tweak_name = self.item.get('name')
        if not tweak_name:
            messagebox.showerror("Error", "Cannot find tweak name to delete.")
            return

        # Ask for confirmation
        if not messagebox.askyesno("Confirm Deletion", f"Are you sure you want to delete the tweak '{tweak_name}'?"):
            return
        
        try:
            # Call backend to delete
            delete_user_tweak(self.config_data, tweak_name)
            
            # Save configuration (same as in toggle_tweak)
            save_config(self.config_data)
            
            # Remove widget from UI after successful deletion
            self.destroy()
            
        except Exception as e:
            messagebox.showerror("Error", f"Failed to delete tweak:\n{e}")
 
 
class AddTweakFrame(ctk.CTkFrame):
    """
    A frame containing the form to add new custom tweaks.
    This is embedded inside the 'User' tab's scrollable frame.
    """
    def __init__(self, master, config_data, **kwargs):
        super().__init__(master, **kwargs)
        self.config_data = config_data
        
        # References for dynamically adding the widget
        self.scroll_frame = None
        self.no_tweaks_label = None
        
        # Transparent background
        self.configure(fg_color="transparent")
        hover_col = darker(ACCENT_COLOR,0.85)
 
        ctk.CTkLabel(self, text="Add Custom Tweak", font=ctk.CTkFont(weight="bold", size=16)).pack(pady=(10, 5), padx=10, fill="x")
 
        # Center form elements
        form_frame = ctk.CTkFrame(self, fg_color="transparent")
        form_frame.pack(pady=5, padx=10, anchor="n")
        
        entry_width = 450 # Fixed width for entry widgets
 
        ctk.CTkLabel(form_frame, text="Tweak name:").grid(row=0, column=0, padx=10, pady=5, sticky="w")
        self.input_name = ctk.CTkEntry(form_frame, placeholder_text="Enter tweak name", width=entry_width)
        self.input_name.grid(row=0, column=1, padx=10, pady=5, sticky="we")
 
        ctk.CTkLabel(form_frame, text="PowerShell (ON):").grid(row=1, column=0, padx=10, pady=5, sticky="w")
        self.input_command = ctk.CTkEntry(form_frame, placeholder_text="Enter the full command to execute", width=entry_width)
        self.input_command.grid(row=1, column=1, padx=10, pady=5, sticky="we")
 
        ctk.CTkLabel(form_frame, text="PowerShell (OFF):").grid(row=2, column=0, padx=10, pady=5, sticky="w")
        self.input_turn_off_command = ctk.CTkEntry(form_frame, placeholder_text="Enter the command to undo the tweak", width=entry_width)
        self.input_turn_off_command.grid(row=2, column=1, padx=10, pady=5, sticky="we")
 
        # Description textbox placeholder logic
        ctk.CTkLabel(form_frame, text="Description:").grid(row=3, column=0, padx=10, pady=5, sticky="nw")
        
        self.placeholder_text = "Enter a brief description... (Optional)"
        # Get theme-aware colors
        try:
            self.placeholder_color = ctk.ThemeManager.theme["CTkLabel"]["text_color"]
            self.text_color = ctk.ThemeManager.theme["CTkEntry"]["text_color"]
        except Exception:
            # Fallback colors
            self.placeholder_color = ("gray65", "gray35")
            self.text_color = ("black", "white")
 
        self.input_description = ctk.CTkTextbox(form_frame, height=80, width=entry_width)
        self.input_description.grid(row=3, column=1, padx=10, pady=5, sticky="we")
        
        # Bind placeholder events
        self.input_description.bind("<FocusIn>", self._clear_placeholder)
        self.input_description.bind("<FocusOut>", self._add_placeholder)
 
        # Set initial placeholder
        self._add_placeholder(None)
 
        self.add_button = ctk.CTkButton(self, text="Add Tweak", fg_color=ACCENT_COLOR, hover_color=hover_col, command=self.add_tweak)
        self.add_button.pack(pady=(5, 10), padx=10)
 
    def set_scroll_info(self, scroll_frame, no_tweaks_label):
        """Receives the parent scroll frame and 'no tweaks' label from SubTabView."""
        self.scroll_frame = scroll_frame
        self.no_tweaks_label = no_tweaks_label
 
    def _clear_placeholder(self, event):
        """Clears placeholder text on focus."""
        if self.input_description.get("0.0", "end-1c") == self.placeholder_text:
            self.input_description.delete("0.0", "end")
            self.input_description.configure(text_color=self.text_color)
 
    def _add_placeholder(self, event):
        """Adds placeholder text if field is empty on focus out."""
        if not self.input_description.get("0.0", "end-1c"):
            self.input_description.configure(text_color=self.placeholder_color)
            self.input_description.insert("0.0", self.placeholder_text)
 
    def add_tweak(self):
        name = self.input_name.get()
        purpose_raw = self.input_description.get("0.0", "end-1c").strip()
        true_cmd = self.input_command.get()
        false_cmd = self.input_turn_off_command.get()
 
        # Handle placeholder
        if purpose_raw == self.placeholder_text:
            purpose = "" # Let the backend set the default
        else:
            purpose = purpose_raw
 
        # Call the new backend function
        new_tweak_item = add_user_tweak(
            self.config_data, name, purpose, true_cmd, false_cmd
        )
   
        if new_tweak_item:
            
            # --- DYNAMIC UI UPDATE ---
            if self.no_tweaks_label:
                self.no_tweaks_label.destroy()
                self.no_tweaks_label = None
            
            if self.scroll_frame:
                new_widget = TweakItemControl(
                    self.scroll_frame,
                    item=new_tweak_item,
                    config_data=self.config_data,
                    is_user_tweak=True, # Make it deletable
                    fg_color=("white", "gray15")
                )
                new_widget.pack(fill="x", pady=5, padx=5)
            # --- END DYNAMIC UI UPDATE ---
 
            # Clear fields
            self.input_name.delete(0, 'end')
            self.input_command.delete(0, 'end')
            self.input_turn_off_command.delete(0, 'end')
            self.input_description.delete("0.0", 'end')
            self._add_placeholder(None) # Reset placeholder
        
        # If it failed, the backend function already showed an error message.
 
 
class SubTabView(ctk.CTkTabview):
    """Creates the sub-tabs (Performance, Security, etc.) for a main feature."""
    def __init__(self, master, categories_data, config_data, feature_name, **kwargs):
        super().__init__(master, **kwargs)
        hover_col = darker(ACCENT_COLOR,0.85)
        
        self.configure(
            text_color=("black","white"),
            segmented_button_selected_color=(ACCENT_COLOR,ACCENT_COLOR),
            segmented_button_selected_hover_color=(hover_col,hover_col),
            segmented_button_unselected_color=("#E5E5E0","#2B2B2B"),
            segmented_button_unselected_hover_color=("#D5D5D5","#3B3B3B")
        )
        
        for cat_entry in categories_data:
            category_name = cat_entry.get('category')
            if not category_name: continue
 
            items = cat_entry.get('items', [])
            tab_frame = self.add(category_name)
            is_user_tweak = (category_name == "User" and feature_name == "Optimizer")
            
            if is_user_tweak:
                scroll_frame = ctk.CTkScrollableFrame(master=tab_frame, fg_color=("gray90", "gray20"))
                scroll_frame.pack(fill="both", expand=True, padx=10, pady=10)
                add_form = AddTweakFrame(scroll_frame, config_data=config_data)
                add_form.pack(fill="x", padx=5, pady=5)
                
                no_tweaks_label = None
                if not items:
                    no_tweaks_label = ctk.CTkLabel(scroll_frame, text="No custom tweaks found.\nAdd one above.")
                    no_tweaks_label.pack(pady=10, padx=10)
                add_form.set_scroll_info(scroll_frame, no_tweaks_label)
            else:
                ctk.CTkLabel(tab_frame, text="Please restart your computer after desired tweaks are set.", text_color=("black","white")).pack(pady=(10,0))
                scroll_frame = ctk.CTkScrollableFrame(master=tab_frame)
                scroll_frame.pack(fill="both", expand=True, padx=10, pady=10)
            
            for item in items:
                # --- UPDATED HEADER LOGIC ---
                if 'header' in item:
                    # create label as a variable so we can update it later
                    lbl = ctk.CTkLabel(
                        scroll_frame,
                        text=item['header'],
                        font=ctk.CTkFont(size=20, weight="bold"),
                        anchor="w",
                        text_color=ACCENT_COLOR
                    )
                    # mark header labels so refresh_accent can find and update them
                    setattr(lbl, '_is_header', True)
                    lbl.pack(fill="x", pady=(25, 5), padx=5)
                    continue
                # ----------------------------

                TweakItemControl(
                    scroll_frame, item=item, config_data=config_data,
                    is_user_tweak=is_user_tweak, fg_color=("white","gray15")
                ).pack(fill="x",pady=5,padx=5)
 
class MainTabView(ctk.CTkTabview):
    """The main TabView that holds 'Optimizer', 'Dashboard', etc."""
    def __init__(self, master, config_data, **kwargs):
        super().__init__(master, **kwargs)
        hover_col = darker(ACCENT_COLOR,0.85)
        # Configure main tab appearance
        self.configure(
            text_color=("black","white"),
            segmented_button_selected_color=(ACCENT_COLOR,ACCENT_COLOR),
            segmented_button_selected_hover_color=(hover_col,hover_col),
            segmented_button_unselected_color=("#E5E5E5","#2B2B2B"),
            segmented_button_unselected_hover_color=("#D5D5D5","#3B3B3B")
        )
        # Create a main tab for each feature
        for main_tab in config_data.get('features',[]):
            tab_name = main_tab.get('feature')
            if not tab_name: continue
            self.add(tab_name)
            categories = main_tab.get('categories',[])
            if tab_name == "Dashboard":
                InfoFrame(self.tab(tab_name), dashboard_data=main_tab).pack(fill="both", expand=True, padx=5, pady=5)
            elif categories:
                # Add subtabs for categories
                SubTabView(self.tab(tab_name),categories,config_data,tab_name).pack(fill="both",expand=True,padx=5,pady=5)
            else:
                # Placeholder for empty tabs
                ctk.CTkLabel(self.tab(tab_name),text=f"'{tab_name}' content coming soon...",
                             text_color=("black","white")).pack(pady=20,padx=20)
 
 
class PowerTimer(ctk.CTkToplevel):
    """A Toplevel window for scheduling power actions."""
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
        self.wm_overrideredirect(True) # Frameless window
        self.attributes("-topmost",True)
        self.configure(fg_color=("white","#333333"), corner_radius=12)
        # Tooltip text styling
        self.label = ctk.CTkLabel(self,text=text,text_color=("black","white"),
                                  fg_color=("white","#333333"))
        self.label.pack(padx=8,pady=4)
        self.withdraw() # Hide initially
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
    """Main application window."""
    def __init__(self, config_data):
        super().__init__()
        global ACCENT_COLOR
        if not config_data or 'features' not in config_data:
            self.destroy(); return
 
        # Load theme data and accent color
        theme_data = config_data.get("theme", {})
        if isinstance(theme_data, dict):
            self.current_theme = theme_data.get("mode", "system")
            ACCENT_COLOR = theme_data.get("accent_color", ACCENT_COLOR)
        else:
            self.current_theme = theme_data if theme_data in ["dark", "light", "system"] else "system"
 
        # Apply theme
        ctk.set_appearance_mode(self.current_theme)
        useLightTheme(ctk.get_appearance_mode() == "Light")
 
        self.root_data = config_data
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
        # b_add_tweaks removed
 
        # Place buttons vertically
        buttons = [b_theme, b_color, b_power, b_github] # b_add_tweaks removed
        for i, btn in enumerate(buttons, start=1):
            btn.grid(row=i, column=0, pady=5, padx=10)
 
        # Add tooltips to sidebar buttons
        add_tooltip(b_theme, "Theme")
        add_tooltip(b_color, "Accent Color")
        add_tooltip(b_power, "Power Scheduler")
        add_tooltip(b_github, "GitHub")
        # Tooltip for b_add_tweaks removed
 
        # Create main tweak tab area
        MainTabView(self, config_data).grid(row=0, column=1, padx=(3, 60), pady=(10, 30), sticky="nsew")
        
        # Fade in animation
        self.fade_in()
 
    def fade_in(self):
        # Fade in effect
        for i in range(0, 11):
            self.attributes('-alpha', i/10)
            self.update()
            self.after(20)
 
    def toggle_theme(self):
        # Fade out
        for i in range(0, 11):
            self.attributes('-alpha', 1.0 - (i/10))
            self.update()
            self.after(40)
        
        # Switch theme mode
        self.current_theme = {"system":"dark","dark":"light","light":"system"}[self.current_theme]
        ctk.set_appearance_mode(self.current_theme)
        useLightTheme(ctk.get_appearance_mode() == "Light")
        useLightTheme(self.current_theme == "light")
        
        # Fade in
        for i in range(0, 11):
            self.attributes('-alpha', i/10)
            self.update()
            self.after(40)
        
        # Save new theme settings
        if "theme" not in self.root_data:
            self.root_data["theme"] = {}
        self.root_data["theme"]["mode"] = self.current_theme
        save_config(self.root_data)
 
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
            save_config(self.root_data)
            self.refresh_accent()
 
    def refresh_accent(self):
        # Recursively update accent color on all widgets
        hover_col = darker(ACCENT_COLOR, 0.85)
        def update(widget):
            if isinstance(widget, ctk.CTkSwitch):
                widget.configure(progress_color=ACCENT_COLOR)
            elif isinstance(widget, ctk.CTkLabel) and getattr(widget, '_is_header', False):
                # update header labels created from data.yaml 'header' entries
                widget.configure(text_color=ACCENT_COLOR)
            elif isinstance(widget, DisplayFrame):
                # Let DisplayFrame handle its own accent refresh (option menus, canvas outline, apply button)
                try:
                    widget.refresh_accent(ACCENT_COLOR)
                except Exception:
                    pass
            elif isinstance(widget, ctk.CTkTabview):
                widget.configure(segmented_button_selected_color=(ACCENT_COLOR, ACCENT_COLOR),
                                 segmented_button_selected_hover_color=(hover_col, hover_col))
            # Also update the "Add Tweak" button
            elif isinstance(widget, ctk.CTkButton) and widget.cget("text") in ["Shutdown","Restart","BIOS", "Add Tweak"]:
                widget.configure(fg_color=ACCENT_COLOR, hover_color=hover_col)
            for w in widget.winfo_children():
                update(w)
        update(self)
 
    def open_github(self):
        # Opens GitHub repo in browser
        webbrowser.open_new_tab("https://github.com/wirekurosastak/Winsane")