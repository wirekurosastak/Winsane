import os
import subprocess
import yaml
import requests
from collections import defaultdict
from tkinter import messagebox
 
# --- Constants ---
WINSANE_FOLDER = r"C:\Winsane"
DATA_FILE = os.path.join(WINSANE_FOLDER, "data.yaml")
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
 
 
def save_config(data):
    try:
        with open(DATA_FILE,"w",encoding="utf-8") as f:
            yaml.safe_dump(data,f,allow_unicode=True,indent=2,sort_keys=False)
    except Exception as e:
        messagebox.showerror("Save Error", f"Error saving configuration:\n{e}")
 
 
def add_user_tweak(config_data, name, purpose, true_cmd, false_cmd):
    """
    Adds a new tweak to the 'User' category and saves the config.
    Returns the new tweak item dictionary on success, None on failure.
    """
    if not name or not true_cmd or not false_cmd:
        messagebox.showerror("Error", "Tweak Name and PowerShell (ON/OFF) commands are required.")
        return None
 
    new_tweak = {
        "name": name,
        "purpose": purpose if purpose else "No Description.",
        True: true_cmd,
        False: false_cmd,
        "enabled": False
    }
 
    # Find the Optimizer feature and User category
    user_category = None
    for feature in config_data.get('features', []):
        if feature.get('feature') == "Optimizer":
            for category in feature.get('categories', []):
                if category.get('category') == "User":
                    user_category = category
                    break
            if user_category is not None:
                break
    
    if user_category is None:
        messagebox.showerror("Config Error", "Could not find 'Optimizer' -> 'User' category in config.")
        return None
 
    if 'items' not in user_category:
        user_category['items'] = []
    
    user_category['items'].append(new_tweak)
 
    try:
        save_config(config_data)
        return new_tweak # Return the newly created item
    except Exception as e:
        messagebox.showerror("Save Error", f"Failed to save config: {e}")
        return None
 
 
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
    """
    Merges the remote and local configs.
    The remote is the "master" (default), but the local config overrides:
    1. 'theme' settings.
    2. All 'enabled' statuses.
    3. Custom tweaks found in the "User" category.
    """
    if not remote:
        return local
    if not local:
        return remote

    # 1. Save the local theme settings
    theme_backup = local.get("theme", {}).copy()

    # 2. Collect all 'enabled' statuses AND custom tweaks from 'local'
    enabled_map = {}
    local_user_tweaks = [] # Stores the custom tweaks

    for feat in local.get("features", []):
        for cat in feat.get("categories", []):
            
            # Find the 'User' category under 'Optimizer'
            if feat.get('feature') == 'Optimizer' and cat.get('category') == 'User':
                local_user_tweaks = cat.get('items', []) # Save the custom tweaks
            
            # Build the 'enabled' map
            for item in cat.get("items", []):
                
                # If this is a header, we leave it out because it doesn't have 'name' and 'enabled' keys
                if 'header' in item:
                    continue
                
                key = (feat["feature"], cat["category"], item["name"])
                enabled_map[key] = item.get("enabled", False)

    # 3. Apply saved 'enabled' statuses to the 'remote' config
    #    AND find the 'User' category in the 'remote' config
    remote_user_category_items_list = None # Will hold the 'items' list from the remote 'User' category

    for feat in remote.get("features", []):
        for cat in feat.get("categories", []):

            # Find the 'User' category in the remote config
            if feat.get('feature') == 'Optimizer' and cat.get('category') == 'User':
                if 'items' not in cat:
                        cat['items'] = [] # Create 'items' list if it doesn't exist
                remote_user_category_items_list = cat['items']
            
            # Apply the 'enabled' statuses
            for item in cat.get("items", []):
                # If this is a header, we leave it out because it doesn't have a 'name' key
                if 'header' in item:
                    continue
                
                key = (feat["feature"], cat["category"], item["name"])
                if key in enabled_map:
                    item["enabled"] = enabled_map[key]
    
    # 4. Add the local custom tweaks (from local_user_tweaks) to the remote config
    if remote_user_category_items_list is not None:
        # Get a set of names already in the remote list to avoid duplicates
        # Ensure that headers are not considered in name collision checking
        remote_tweak_names = {item.get('name') for item in remote_user_category_items_list if 'header' not in item}
        
        for local_tweak in local_user_tweaks:
            
           # If the saved "User" tweak is a header, don't try to treat it as a tweak
            if 'header' in local_tweak:
                continue
           
            # Only add the tweak if its name is not already in the remote list
            if local_tweak.get('name') not in remote_tweak_names:
                # Before adding, set its 'enabled' status based on the map
                key = ('Optimizer', 'User', local_tweak.get('name'))
                local_tweak['enabled'] = enabled_map.get(key, False)
                
                remote_user_category_items_list.append(local_tweak)

    # 5. Restore the theme settings
    if theme_backup:
        remote["theme"] = theme_backup
        
    return remote
 
# Initialization helper
def init_config():
    ensure_winsane_folder()
    local_data = load_local_config(DATA_FILE)
    remote_data = fetch_remote_config(GITHUB_RAW_URL)
    
    if local_data:
        global_config_data = merge_configs(remote_data, local_data) if remote_data else local_data
    else:
        global_config_data = remote_data
        
    if global_config_data:
        save_config(global_config_data)
        
    return global_config_data

def delete_user_tweak(config_data, tweak_name):
    """
    Finds and deletes the tweak with the given name from the config_data dict.
    Note: Calling 'save_config' afterwards is the responsibility of the UI (frontend).
    """
    user_tweaks_list = None
    
    # 1. Find the 'User' tweaks list (the 'items' list)
    # Navigate the structure: features -> Optimizer -> categories -> User -> items
    try:
        for feature in config_data.get('features', []):
            if feature.get('feature') == 'Optimizer':
                for category in feature.get('categories', []):
                    if category.get('category') == 'User':
                        user_tweaks_list = category.get('items', [])
                        break
                if user_tweaks_list is not None:
                    break
    except Exception as e:
        print(f"Error reading config structure: {e}")
        raise Exception("Invalid config file structure.")

    if user_tweaks_list is None:
        # This isn't necessarily an error; the 'User' category might not exist yet.
        print("Note: 'User' category or 'items' list not found.")
        return # Nothing to delete

    # 2. Find the item to remove in the list
    tweak_to_remove = None
    for item in user_tweaks_list:
        if item.get('name') == tweak_name:
            tweak_to_remove = item
            break
    
    # 3. Remove the item if it was found
    if tweak_to_remove:
        user_tweaks_list.remove(tweak_to_remove)
        print(f"Tweak '{tweak_name}' removed from config data.")
    else:
        # Raise an error if not found (this will be caught by the UI)
        raise KeyError(f"Tweak '{tweak_name}' not found in 'User' list.")