# CrossDeviceTracker.Desktop

Windows desktop client for CrossDeviceTracker that tracks foreground application usage and syncs screen time data with the backend API.

---

## Overview

CrossDeviceTracker.Desktop is the Windows data-collection component of the CrossDeviceTracker system. It monitors the currently active foreground window, records application usage as time blocks, and synchronizes those logs with the backend API.

---

## System Architecture

```
Windows Desktop Client  →  Backend API  →  Dashboard (Future)
 (this project)             (storage)       (visualization)
```

---

## Features

### System Tray Integration
- **Minimize to Tray** — Click the close button to minimize the app to the system tray instead of closing it
- **Background Tracking** — App continues tracking even when minimized
- **Quick Access** — Double-click the tray icon to show/hide the window
- **Context Menu** — Right-click tray icon to Show or Exit

### GUI Features
- **Real-time Status Display** — Shows current tracking status
- **Current App Display** — View which app is currently active
- **Activity Log** — See recent app usage with start times and durations
- **Clear Logs** — Clean up the activity log

---

## How It Works

1. **Foreground Detection** — Polls the active window using Win32 APIs (`GetForegroundWindow`, `GetWindowThreadProcessId`) at a fixed interval (default: 2 seconds).
2. **Time Block Tracking** — When the foreground application changes, the previous usage block is finalized with a start time, end time, and duration.
3. **Local Storage** — Usage logs are persisted locally for offline resilience.
4. **API Sync** — Logs are sent to the backend API, authenticated with a device JWT in the `Authorization: Bearer <token>` header.

---

## Tech Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 10 |
| Language | C# |
| Project Type | Windows Forms GUI Application |
| Target Platform | Windows |

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- Windows OS

---

## Getting Started

```bash
# Clone the repository
git clone https://github.com/<your-username>/CrossDeviceTracker.Desktop.git
cd CrossDeviceTracker.Desktop

# Build the project
dotnet build

# Run the application
dotnet run
```

---

## Project Structure

```
CrossDeviceTracker.Desktop/
├── CrossDeviceTracker.Desktop.csproj   # Project file
├── Program.cs                          # Application entry point
├── DESIGN.md                           # Design document
├── DOCUMENT.md                         # Development documentation
└── README.md                           # This file
```

---

## Development Roadmap

| Phase | Goal | Status |
|-------|------|--------|
| Phase 1 | Detect active foreground window | Not started |
| Phase 2 | Track per-application time blocks | Not started |
| Phase 3 | Store usage logs locally | Not started |
| Phase 4 | Send usage logs to backend API | Not started |
| Phase 5 | Background execution & system tray | ✅ Complete |

---

## Configuration

The desktop client authenticates with the backend using a **device JWT** obtained during the device linking process. All API requests include this token in the request header:

```
Authorization: Bearer <device_jwt>
```

---

## Related Projects

- **CrossDeviceTracker Backend** — API server that stores and processes usage logs.
- **CrossDeviceTracker Dashboard** (future) — Web dashboard for visualizing screen time analytics.

---

## License

This project is part of the CrossDeviceTracker system.
