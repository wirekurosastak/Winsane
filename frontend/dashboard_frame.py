import customtkinter as ctk
from backend.dashboard_manager import SystemInfoManager

class InfoFrame(ctk.CTkFrame):
    def __init__(self, master, dashboard_data, **kwargs):
        super().__init__(master, **kwargs)
        self.configure(fg_color="transparent")

        try:
            self.manager = SystemInfoManager()
            self.data_labels = {} 
            static_data = self.manager.get_static_data()
            self.static_drives = static_data.get('drives', [])

            # Layout Setup (2 Columns)
            self.grid_columnconfigure(0, weight=1, uniform="group1")
            self.grid_columnconfigure(1, weight=1, uniform="group1")
            self.grid_rowconfigure(0, weight=1) 

            left_frame = ctk.CTkFrame(self, fg_color=("gray90", "gray20"))
            left_frame.grid(row=0, column=0, padx=10, pady=10, sticky="nsew")
            left_frame.grid_columnconfigure(1, weight=1)
            
            right_frame = ctk.CTkFrame(self, fg_color=("gray90", "gray20"))
            right_frame.grid(row=0, column=1, padx=(0, 10), pady=10, sticky="nsew")
            right_frame.grid_columnconfigure(1, weight=1)

            # Data Pre-formatting
            static_data["cpu_cores_text"] = f"{static_data.get('cpu_cores', 'N/A')} Cores / {static_data.get('cpu_threads', 'N/A')} Threads"
            static_data["mb_text"] = f"{static_data.get('mb_manufacturer', 'N/A')} {static_data.get('mb_product', 'N/A')}"

            # UI Builder from YAML
            layout_config = dashboard_data.get('layout', {})
            frames = {"left": left_frame, "right": right_frame}
            column_layouts = {
                "left": layout_config.get('left', []),
                "right": layout_config.get('right', [])
            }
            row_counters = {"left": 0, "right": 0}

            for frame_key, layout in column_layouts.items():
                parent_frame = frames[frame_key]
                row_idx = 0
                for item in layout:
                    item_type = item.get('type')
                    if item_type == "header":
                        self._create_section_header(parent_frame, item.get('title', '...'), row_idx)
                    
                    elif item_type == "row":
                        key = item.get('key')
                        if not key:
                            row_idx += 1
                            continue
                        
                        label = item.get('label', '')
                        static_key = item.get('source_key')
                        default = item.get('default', 'N/A')
                        wrap = item.get('wrap', 350)

                        initial_text = static_data.get(static_key, default) if static_key else default
                        
                        self.data_labels[key] = self._create_info_row(
                            parent_frame, label, initial_text, row_idx, data_wraplength=wrap
                        )
                    row_idx += 1
                row_counters[frame_key] = row_idx # Save last row index

            # Dynamic Drive Rows
            row_idx = row_counters["left"] 
            if not self.static_drives:
                self._create_info_row(left_frame, "Partitions:", "No drives found.", row_idx); row_idx+=1
            else:
                for drive in self.static_drives:
                    mountpoint = drive['mountpoint']
                    total_str = drive['total_str']
                    label_key = f"disk_{mountpoint}"
                    initial_text = f"0.0% (0.0 GB / {total_str})"
                    
                    self.data_labels[label_key] = self._create_info_row(left_frame, f"{mountpoint} Usage:", initial_text, row_idx)
                    row_idx += 1

            # Start Update Loop
            self.update_info()

        except Exception as e:
            # Fallback error message
            label = ctk.CTkLabel(self, text=f"Error reading system information:\n{e}",
                                 font=ctk.CTkFont(size=16), text_color="red",
                                 justify="left", wraplength=600)
            label.place(relx=0.5, rely=0.5, anchor="center")

    # Widget Creation Helpers

    def _create_section_header(self, parent, title, row):
        label = ctk.CTkLabel(parent, text=title, font=ctk.CTkFont(size=16, weight="bold"), anchor="w")
        label.grid(row=row, column=0, columnspan=2, padx=10, pady=(10, 2), sticky="ew")

    def _create_info_row(self, parent, label_text, data_text, row, data_wraplength=350):
        label = ctk.CTkLabel(parent, text=label_text, font=ctk.CTkFont(weight="bold"), anchor="e")
        label.grid(row=row, column=0, padx=(10, 5), pady=2, sticky="e")
        
        data_label = ctk.CTkLabel(parent, text=data_text, anchor="w", wraplength=data_wraplength)
        data_label.grid(row=row, column=1, padx=5, pady=2, sticky="w")
        
        return data_label # Return the data widget for updating

    # Data Update Function

    def update_info(self):
        # Fetches new data and updates the corresponding labels
        try:
            if not hasattr(self, 'manager'): return
            data = self.manager.get_dynamic_data()
            
            # Update CPU/RAM
            self.data_labels["cpu_load"].configure(text=data["cpu_load"])
            ram_text = f"{data['ram_percent']}% ({data['ram_used']} / {data['ram_total']})"
            self.data_labels["ram_usage"].configure(text=ram_text)

            # Update GPU
            self.data_labels["gpu_name"].configure(text=data["gpu_name"])
            self.data_labels["gpu_load"].configure(text=data["gpu_load"])
            gpu_mem_text = f"{data['gpu_mem_used']} / {data['gpu_mem_total']}"
            self.data_labels["gpu_mem"].configure(text=gpu_mem_text)

            # Update Disks
            disk_usages = data.get('disk_usages', [])
            for usage_data in disk_usages:
                mountpoint = usage_data['mountpoint']
                label_key = f"disk_{mountpoint}"
                
                drive_static_data = next((d for d in self.static_drives if d['mountpoint'] == mountpoint), None)
                if drive_static_data and label_key in self.data_labels:
                    total_str = drive_static_data['total_str']
                    disk_text = f"{usage_data['percent']}% ({usage_data['used_str']} / {total_str})"
                    self.data_labels[label_key].configure(text=disk_text)

        except Exception as e:
            print(f"Update info error: {e}")
            return # Stop loop on error

        # Schedule the next update
        if self.winfo_exists():
            self.after(5000, self.update_info)