# CrossDeviceTracker.Desktop — Documentation

---

## 1. Project Initialization

Created a new GitHub repository for the Windows desktop client of the CrossDeviceTracker system.

**Repository Name:** `CrossDeviceTracker.Desktop`

The desktop application will be responsible for tracking foreground application usage on Windows and sending usage logs to the backend API.

---

## 2. Initial Project Structure

The project was initialized as a .NET console application.

Current repository structure:

```
CrossDeviceTracker.Desktop
│
├── CrossDeviceTracker.Desktop.csproj
├── Program.cs
├── README.md
└── .gitignore
```

The console application will be used first to prototype foreground window detection before adding UI or background services.

---

## 3. Role in the System

The CrossDeviceTracker.Desktop application will act as the Windows client responsible for:

- Detecting the currently active foreground application
- Tracking usage duration of each application
- Storing usage logs locally
- Synchronizing usage logs with the CrossDeviceTracker backend API

---

## 4. Development Plan

The desktop client will be developed incrementally in the following phases:

| Phase | Goal |
|-------|------|
| **Phase 1** | Detect the active foreground window on Windows |
| **Phase 2** | Track time spent on each application and generate time blocks |
| **Phase 3** | Store usage logs locally |
| **Phase 4** | Send usage logs to the backend API |
| **Phase 5** | Add background execution and system tray integration |

---

## 5. First Development Goal

**Implement foreground window detection.**

The application will check the active window every few seconds and print the detected application name to the console.

---

## 6. Backend Integration

The desktop client will communicate with the CrossDeviceTracker backend API.

Device authentication will use the device JWT generated during the desktop linking process.