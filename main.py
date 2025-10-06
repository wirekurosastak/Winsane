from backend.config import init_config
from frontend.ui import Winsane


def main():
    tweak_data = init_config()
    if tweak_data:
        app = Winsane(tweak_data)
        app.mainloop()


if __name__ == "__main__":
    main()