from backend.config import init_config
from frontend.ui import Winsane


def main():
    config_data = init_config()
    if config_data:
        app = Winsane(config_data)
        app.mainloop()


if __name__ == "__main__":
    main()