from backend.config import init_config, manage_uac
from frontend.ui import Winsane


def main():
    # Disable UAC on startup (if not permanently disabled)
    manage_uac(False)
    
    try:
        config_data = init_config()
        if config_data:
            app = Winsane(config_data)
            app.mainloop()
    finally:
        # Re-enable UAC on exit
        manage_uac(True)


if __name__ == "__main__":
    main()