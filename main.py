import customtkinter as ctk
from tkinter import messagebox
import yaml
import subprocess
import os
import requests

# --- Configuration & Persistence ---
WINSANE_FOLDER    = r"C:\Winsane"
TWEAKS_FILE       = os.path.join(WINSANE_FOLDER, "data.yaml")
global_tweak_data = None

def save_tweaks(data):
    try:
        with open(TWEAKS_FILE, "w", encoding="utf-8") as f:
            yaml.safe_dump(data, f, allow_unicode=True, indent=2, sort_keys=False)
    except Exception as e:
        messagebox.showerror("Save Error", f"An error occurred while saving the configuration file:\n{e}")

# --- Ensure Winsane folder exists ---  
def ensure_winsane_folder():
    folder_path = r"C:\Winsane"
    if not os.path.exists(folder_path):
        print(f"[DEBUG] Folder does not exist, creating: {folder_path}")
        try:
            os.makedirs(folder_path)
        except Exception as e:
            print(f"[ERROR] Failed to create folder: {e}")
            messagebox.showerror("Folder Error", f"Could not create folder at {folder_path}:\n{e}")

        # Remove old data.yaml from the script directory.
    script_dir  = os.path.dirname(os.path.abspath(__file__))
    legacy_file = os.path.join(script_dir, "data.yaml")
    if os.path.exists(legacy_file):
        try:
            os.remove(legacy_file)
        except Exception:
            pass

ensure_winsane_folder()

# --- Load & Merge Configuration ---
def fetch_remote_config(url, timeout=5):
    try:
        resp = requests.get(url, timeout=timeout)
        resp.raise_for_status()
        return yaml.safe_load(resp.text)
    except Exception as e:
        messagebox.showinfo("Network Error", f"GitHub fetch failed.\nCheck your internet connection.\nWinsane will start with the local configuration if available.")
        return None

def load_local_config(path):
    if os.path.exists(path):
        try:
            with open(path, "r", encoding="utf-8") as f:
                return yaml.safe_load(f)
        except Exception as e:
            messagebox.showerror("File Error", f"Local load failed:\n{e}")
    return None

def merge_configs(remote, local):
    if not remote:
        return local
# 1) Collect local ‘enabled’ values into a name‐keyed map   
    enabled_map = {}
    for feat in (local or {}).get("tweaks", []):
        for cat in feat.get("categories", []):
            for item in cat.get("items", []):
                key = (feat["feature"], cat["category"], item["name"])
                enabled_map[key] = item.get("enabled", False)
# 2) Apply these to the remote structure
    for feat in remote.get("tweaks", []):
        for cat in feat.get("categories", []):
            for item in cat.get("items", []):
                key = (feat["feature"], cat["category"], item["name"])
                item["enabled"] = enabled_map.get(key, item.get("enabled", False))

    return remote

# 1) Fetch from GitHub
GITHUB_RAW_URL = "https://raw.githubusercontent.com/wirekurosastak/Winsane/main/data.yaml"
# 2) Load the local file if it exists
local_data = load_local_config(TWEAKS_FILE)
# 3) Try fetching from GitHub, but if it fails, just continue
try:
    remote_data = fetch_remote_config(GITHUB_RAW_URL)
except Exception:
    remote_data = None
# 4) 3) Decision: if local data is available, use it
if local_data:
    if remote_data:
        global_tweak_data = merge_configs(remote_data, local_data)
    else:
        global_tweak_data = local_data
else:
     # if there's no local file, it has to come from remote (or None, in which case the GUI won’t start)
     global_tweak_data = remote_data
if global_tweak_data is not None:
    try:
        with open(TWEAKS_FILE, "w", encoding="utf-8") as f:
            yaml.safe_dump(
                global_tweak_data,
                f,
                allow_unicode=True,
                indent=2,
                sort_keys=False
            )
    except Exception as e:
        messagebox.showerror(
            "Save Error",
            f"Could not cache config locally:\n{e}"
        )

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