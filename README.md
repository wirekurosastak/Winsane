# Winsane

<div align="center">

**The Ultimate Windows 11 Optimization Utility**

[![.NET](https://img.shields.io/badge/.NET-8.0-512bd4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/UI-Avalonia-aa00ff?style=flat-square)](https://avaloniaui.net/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2011-0078d4?style=flat-square&logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-Non--Commercial-red?style=flat-square)](LICENSE)

</div>

---

**Winsane** is a modern, lightweight, and powerful system utility designed to streamline your Windows 11 experience. built with **.NET 8** and **Avalonia UI**, it combines robust performance optimization tools with a beautiful, Fluent Design-inspired interface.

Unlike generic script collections, Winsane provides a safe, interactive, and extensible environment to manage your system's health, privacy, and performance.

## ✨ Key Features

### 📊 Comprehensive Dashboard
Get a real-time overview of your system's vitals. Monitor **CPU**, **GPU**, **RAM** usage, and view detailed hardware specifications and security status (TPM, Secure Boot) at a glance.

### 🚀 System Optimizer
Boost your PC's performance with a curated list of tweaks.
- **Performance:** Optimize services, startup behavior, and resource allocation.
- **Privacy:** Disable telemetry and unwanted tracking features.
- **Customization:** Tweak UI elements to your liking.
- **Safety:** Every action is reversible, and user-defined tweaks are supported via PowerShell.

### 📦 App Manager (Winget Integration)
Say goodbye to bloatware. Winsane integrates directly with **Windows Package Manager (Winget)**.
- **Debloat:** Remove pre-installed junk and trialware.
- **Install:** Quickly install essential applications (Browsers, Dev Tools, Utilities).

### 🧹 Deep Cleaner
Reclaim storage space by safely removing temporary files, caches, and system logs that clutter your drive.

### 🛠 Admin Tools & Power User Features
- **Quick Access:** Launch essential Windows administrative tools (Registry Editor, Group Policy, Services) instantly.
- **Custom Tweaks:** Add your own PowerShell scripts directly within the UI to extend functionalities.
- **Backup:** One-click generation of System Restore Points ensures you can always roll back changes.

## 🛠 Tech Stack

- **Framework:** .NET 8
- **UI Framework:** Avalonia UI (Cross-platform XAML)
- **Design System:** FluentAvalonia (Bringing WinUI 3 aesthetics to Avalonia)
- **Logic:** C# 12, PowerShell Integration, WMI/CIM

## 📦 Getting Started

### Prerequisites
- Windows 11 (64-bit)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Building from Source

1. **Clone the repository**
   ```bash
   git clone https://github.com/wirekurosastak/Winsane.git
   cd Winsane
   ```

2. **Publish (Standalone Single File)**
   ```bash
   dotnet publish -r win-x64 -c Release --self-contained true -p:PublishSingleFile=true
   ```

3. **Run the application**
   Navigate to `bin/Release/net8.0-windows/win-x64/publish` and run `WinsaneCS.exe`.

## ⚠️ Disclaimer

**Winsane** is a powerful tool that modifies system configurations. While extensive care has been taken to ensure safety (including mandatory Restore Point prompts), modifying Windows Registry and system services always carries some risk.

**Use at your own risk.** The developers are not responsible for any system instability or data loss. Key features like the "Optimizer" are designed for Power Users who understand the implications of system tweaking.

## 📄 License

This project is licensed under a **Custom Non-Commercial License**. See the [LICENSE](LICENSE) file for details.
