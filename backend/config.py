import os
import subprocess
import yaml
import requests
from collections import defaultdict
from tkinter import messagebox

# --- Constants ---
WINSANE_FOLDER = r"C:\Winsane"
TWEAKS_FILE = os.path.join(WINSANE_FOLDER, "data.yaml")
GITHUB_RAW_URL = "https://raw.githubusercontent.com/wirekurosastak/Winsane/main/data.yaml"
ACCENT_COLOR = "#3B8ED0"


def darker(hex_color, factor=0.8):
    c = hex_color.lstrip("#")
    r, g, b = [int(c[i:i+2],16) for i in (0,2,4)]
    return "#%02x%02x%02x" % (int(r*factor), int(g*factor), int(b*factor))


def run_powershell_as_admin(command):
    if not command.strip():
        return
    try:
        subprocess.run([
            "powershell","-Command",
            f"{command}"
        ], check=True)
    except subprocess.CalledProcessError as e:
        messagebox.showerror("Error", f"Command failed:\n{e}")


def ensure_winsane_folder():
    os.makedirs(WINSANE_FOLDER, exist_ok=True)
    legacy_file = os.path.join(os.path.dirname(os.path.abspath(__file__)), "data.yaml")
    if os.path.exists(legacy_file):
        try:
            os.remove(legacy_file)
        except Exception:
            pass


def save_tweaks(data):
    try:
        with open(TWEAKS_FILE,"w",encoding="utf-8") as f:
            yaml.safe_dump(data,f,allow_unicode=True,indent=2,sort_keys=False)
    except Exception as e:
        messagebox.showerror("Save Error", f"Error saving configuration:\n{e}")


def load_local_config(path):
    if os.path.exists(path):
        try:
            with open(path,"r",encoding="utf-8") as f:
                return yaml.safe_load(f)
        except Exception as e:
            messagebox.showerror("File Error", f"Local load failed:\n{e}")
    return None


def fetch_remote_config(url,timeout=5):
    try:
        resp = requests.get(url, timeout=timeout)
        resp.raise_for_status()
        return yaml.safe_load(resp.text)
    except Exception:
        messagebox.showinfo("Network Error", "Failed to fetch config from GitHub.\nLocal configuration will be used if available.")
        return None


def merge_configs(remote, local):
    if not remote:
        return local
    theme_backup = local.get("theme",{}).copy() if local else {}
    enabled_map = {(feat["feature"], cat["category"], item["name"]): item.get("enabled", False)
                   for feat in (local or {}).get("tweaks", [])
                   for cat in feat.get("categories", [])
                   for item in cat.get("items", [])}
    for feat in remote.get("tweaks", []):
        for cat in feat.get("categories", []):
            for item in cat.get("items", []):
                key = (feat["feature"], cat["category"], item["name"])
                item["enabled"] = enabled_map.get(key, item.get("enabled", False))
    if theme_backup:
        remote["theme"] = theme_backup
    return remote


# Initialization helper
def init_config():
    ensure_winsane_folder()
    local_data = load_local_config(TWEAKS_FILE)
    remote_data = fetch_remote_config(GITHUB_RAW_URL)
    if local_data:
        global_tweak_data = merge_configs(remote_data, local_data) if remote_data else local_data
    else:
        global_tweak_data = remote_data
    if global_tweak_data:
        save_tweaks(global_tweak_data)
    return global_tweak_data
