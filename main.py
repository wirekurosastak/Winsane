import customtkinter as ctk
from tkinter import messagebox
import yaml
import subprocess

# --- Configuration & Persistence ---
TWEAKS_FILE = "data.yaml"
global_tweak_data = None

def save_tweaks(data):
    try:
        with open(TWEAKS_FILE, "w", encoding="utf-8") as f:
            yaml.safe_dump(data, f, allow_unicode=True, indent=2, sort_keys=False)
    except Exception as e:
        messagebox.showerror("Save Error", f"An error occurred while saving the configuration file:\n{e}")

# --- Load YAML ---
try:
    with open(TWEAKS_FILE, "r", encoding="utf-8") as f:
        global_tweak_data = yaml.safe_load(f)
except FileNotFoundError:
    messagebox.showerror("Error", f"Configuration file not found!")
except yaml.YAMLError as e:
    messagebox.showerror("Error", f"Configuration reading error: {e}")

# --- GUI Components ---
class TweakItemControl(ctk.CTkFrame):
    def __init__(self, master, item, all_data, **kwargs):
        super().__init__(master, **kwargs)
        self.item = item
        self.all_data = all_data

        self.grid_columnconfigure(0, weight=1)
        self.grid_columnconfigure(1, weight=0)

        self.name_label = ctk.CTkLabel(
            self, text=item['name'], font=ctk.CTkFont(weight="bold", size=14)
        )
        self.name_label.grid(row=0, column=0, padx=15, pady=(5, 0), sticky="w")

        self.purpose_label = ctk.CTkLabel(
            self,
            text=item.get('purpose', 'No description.'),
            wraplength=450,
            justify="left",
            fg_color="transparent",
            text_color=("gray30", "gray70")  # <-- Based on system theme
        )
        self.purpose_label.grid(row=1, column=0, padx=15, pady=(0, 5), sticky="w")

        initial_state = item.get('enabled', False)
        self.tweak_var = ctk.BooleanVar(value=initial_state)
        self.tweak_switch = ctk.CTkSwitch(
            self,
            text="",
            command=self.toggle_tweak,
            variable=self.tweak_var,
            onvalue=True,
            offvalue=False
        )
        self.tweak_switch.grid(row=0, column=1, rowspan=2, padx=20, pady=10, sticky="e")

    def execute_command(self, command, action_name):
        if not command:
            messagebox.showinfo("Information", f"No '{action_name}' command defined for this tweak.")
            return
        try:
            subprocess.run(["powershell", "-Command", command], check=True)
        except subprocess.CalledProcessError as e:
            messagebox.showerror("Error", f"Command execution failed:\n{e}")

    def toggle_tweak(self):
        is_on = self.tweak_var.get()
        if is_on:
            command = self.item.get(True, '')   # Boolean True
            action_name = "Enable"
        else:
            command = self.item.get(False, '')  # Boolean False
            action_name = "Disable"


        self.execute_command(command, action_name)
        self.item['enabled'] = is_on
        save_tweaks(self.all_data)

class SubTabView(ctk.CTkTabview):
    def __init__(self, master, categories_data, root_data, **kwargs):
        super().__init__(master, **kwargs)
        self.categories_data = categories_data
        self.root_data = root_data

        # Group items by category
        from collections import defaultdict
        category_map = defaultdict(list)
        for cat_entry in categories_data:
            category_map[cat_entry['category']].extend(cat_entry.get('items', []))

        self.configure(text_color=("black", "white"))

        for category_name, items in category_map.items():
            self.add(category_name)
            scroll_frame = ctk.CTkScrollableFrame(
                master=self.tab(category_name),
                label_text=f"{category_name} Settings",
                label_text_color=("black", "white")
            )
            scroll_frame.pack(fill="both", expand=True, padx=10, pady=10)

            for item in items:
                TweakItemControl(
                    scroll_frame,
                    item=item,
                    all_data=self.root_data,
                    fg_color=("white", "gray15")  # <-- Bright/Dark
                ).pack(fill="x", pady=5, padx=5)

class MainTabView(ctk.CTkTabview):
    def __init__(self, master, all_data, **kwargs):
        super().__init__(master, **kwargs)
        self.root_data = all_data
        tweaks_data = all_data.get('tweaks', [])

        for main_tab in tweaks_data:
            tab_name = main_tab.get('feature')
            if not tab_name:
                continue
            self.add(tab_name)
            tab_frame = self.tab(tab_name)

            categories = main_tab.get('categories', [])
            if categories:
                self.subtab = SubTabView(tab_frame, categories_data=categories, root_data=self.root_data)
                self.subtab.pack(fill="both", expand=True, padx=5, pady=5)
            else:
                ctk.CTkLabel(tab_frame, text=f"The content of the '{tab_name}' tab will be available soon...").pack(pady=20, padx=20)

class Winsane(ctk.CTk):
    def __init__(self, tweak_data):
        super().__init__()

        if not tweak_data or 'tweaks' not in tweak_data:
            self.destroy()
            return

        ctk.set_appearance_mode("system")
        ctk.set_default_color_theme("blue")
        self.title("Winsane")
        self.geometry("1000x800")
        self.minsize(800, 600)
        self.grid_columnconfigure(0, weight=1)
        self.grid_rowconfigure(0, weight=1)

        self.tabs = MainTabView(self, tweak_data)
        self.tabs.grid(row=0, column=0, padx=20, pady=10, sticky="nsew")

# --- Start Application ---
if global_tweak_data is not None:
    app = Winsane(global_tweak_data)
    app.mainloop()
