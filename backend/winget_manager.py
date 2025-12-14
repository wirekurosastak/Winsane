import threading
import subprocess
from tkinter import messagebox

class WingetManager:
    _instance = None
    _lock = threading.Lock()

    def __new__(cls):
        if cls._instance is None:
            with cls._lock:
                if cls._instance is None:
                    cls._instance = super(WingetManager, cls).__new__(cls)
                    cls._instance.install_lock = threading.Lock()
        return cls._instance

    def run_command(self, command):
        """
        Executes a command. If it's a winget command, it acquires a lock
        to ensure sequential execution.
        """
        # Check if it's likely a winget command
        is_winget = "winget" in command.lower()

        if is_winget:
            # Acquire lock for winget commands
            with self.install_lock:
                return self._execute(command)
        else:
            # Run other commands directly
            return self._execute(command)

    def _execute(self, command):
        try:
            # Using the same subprocess call as in config.py
            subprocess.run([
                "powershell", "-Command",
                f"{command}"
            ], check=True, creationflags=0x08000000) # CREATE_NO_WINDOW
        except subprocess.CalledProcessError as e:
            # Re-raising or handling? The original code showed an error box.
            # We'll let the caller handle it or show it here if we want to match config.py exactly.
            # But config.py catches it. Let's raise it so config.py can catch it if we call it from there?
            # Actually, config.py's run_powershell_as_admin catches it.
            # So if we are called BY run_powershell_as_admin, we should probably raise.
            raise e
