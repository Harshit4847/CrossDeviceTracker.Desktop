# CrossDeviceTracker.Desktop

Windows desktop client for CrossDeviceTracker that tracks foreground application usage and syncs screen time data with the backend API.

---

## Overview

CrossDeviceTracker.Desktop is the Windows data-collection component of the CrossDeviceTracker system. It monitors the currently active foreground window, records application usage as time blocks, persists them locally in SQLite, and synchronizes those logs with the backend API using a device JWT for authentication.

---

## System Architecture

```
Windows Desktop Client  →  Backend API  →  Dashboard (Future)
 (this project)             (storage)       (visualization)

  ┌─────────────────────────────────────────────────┐
  │ Core       AppTracker (Win32 polling engine)     │
  │              ↓                                   │
  │ Data       SqliteLogRepository (offline-first)   │
  │              ↓                                   │
  │ Services   SyncService → ApiClient → Backend API │
  │            DeviceAuthService (JWT management)     │
  │              ↓                                   │
  │ UI         MainForm (WinForms + system tray)     │
  │            DeviceLinkingDialog                   │
  └─────────────────────────────────────────────────┘
```

---

## Features

### Foreground Tracking
- **Win32 Polling** — Polls the active window every 2 seconds via `GetForegroundWindow` and `GetWindowThreadProcessId`
- **Time Block Tracking** — Records application usage sessions with start time, end time, and duration
- **Lock Screen Exclusion** — Windows lock screen (`LockApp`) is excluded from tracking

### Device Linking & Authentication
- **Token-Based Linking** — Pair the desktop client to your account using a one-time link token
- **Device JWT Persistence** — JWT stored locally in `device.json` and reloaded on startup
- **Auto-Relink** — On 401 (Unauthorized), prompts re-linking automatically

### Backend Synchronization
- **Background Sync** — `SyncService` pushes pending logs to the backend API every 30 seconds
- **Offline-First** — Logs are stored locally in SQLite before network transmission
- **Retry Logic** — Failed logs are retried on subsequent sync cycles

### System Tray Integration
- **Minimize to Tray** — Click the close button to minimize the app to the system tray instead of closing it
- **Background Tracking** — App continues tracking even when minimized
- **Quick Access** — Double-click the tray icon to show/hide the window
- **Context Menu** — Right-click tray icon for Show, Relink Device, or Exit

### GUI Features
- **Real-time Status Display** — Shows current tracking and device link status
- **Current App Display** — View which app is currently active
- **Pending / Failed Logs** — See logs awaiting sync with start times, durations, and sync status
- **Clear Logs** — Clean up all local logs

---

## How It Works

1. **Foreground Detection** — Polls the active window using Win32 APIs (`GetForegroundWindow`, `GetWindowThreadProcessId`) every 2 seconds.
2. **Time Block Tracking** — When the foreground application changes, the previous usage block is finalized with a start time, end time, and duration.
3. **Local Storage** — Usage logs are persisted locally in SQLite (`logs.db`) for offline resilience.
4. **Device Linking** — On first launch, the user enters a device token to link the client to their account. The backend issues a device JWT.
5. **API Sync** — `SyncService` runs in the background, pushing pending logs to the backend API every 30 seconds, authenticated with the device JWT in the `Authorization: Bearer <token>` header.

---

## Tech Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 10.0 |
| Language | C# |
| Project Type | Windows Forms GUI Application |
| Target Platform | Windows |
| Local Database | SQLite (`System.Data.SQLite.Core` 1.0.119) |
| HTTP | `Microsoft.Extensions.Http` 10.0.0 |

---

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- Windows OS

---

## Getting Started

```bash
# Clone the repository
git clone https://github.com/Harshit4847/CrossDeviceTracker.Desktop.git
cd CrossDeviceTracker.Desktop

# Build the project
dotnet build

# Run the application
dotnet run
```

On first launch, the application will prompt you to enter a device linking token generated from the backend.

---

## Project Structure

```
CrossDeviceTracker.Desktop/
├── Core/
│   └── AppTracker.cs                  # Foreground window polling engine (Win32)
├── Data/
│   ├── ILogRepository.cs              # Repository interface
│   └── SqliteLogRepository.cs         # SQLite persistence implementation
├── Models/
│   ├── Log.cs                         # Log model and SyncStatus enum
│   └── LinkDesktopResponse.cs         # Device linking API response DTO
├── Services/
│   ├── ApiClient.cs                   # Backend API client (sync, linking, auth)
│   ├── DeviceAuthService.cs           # Device JWT management and persistence
│   └── SyncService.cs                 # Background sync service (30s interval)
├── MainForm.cs                        # Windows Forms UI and system tray lifecycle
├── DeviceLinkingDialog.cs             # Device linking dialog UI
├── SyncDebugHelper.cs                 # Diagnostic and debugging utilities
├── Program.cs                         # Application entry point
├── appsettings.json                   # API base URL and configuration
├── CrossDeviceTracker.Desktop.csproj  # Project file
├── DESIGN.md                          # Architecture and design document
├── DOCUMENT.md                        # Development documentation
├── IMPLEMENTATION.md                  # Implementation details
└── README.md                          # This file
```

---

## Development Roadmap

| Phase | Goal | Status |
|-------|------|--------|
| Phase 1 | Detect active foreground window | Complete |
| Phase 2 | Track per-application time blocks | Complete |
| Phase 3 | Store usage logs locally (SQLite) | Complete |
| Phase 4 | Lock/unlock session handling | Complete |
| Phase 5 | Device JWT authentication and linking | Complete |
| Phase 6 | Backend synchronization (SyncService) | Complete |
| Phase 7 | Background execution and system tray | Complete |

---

## Configuration

### API Settings

API configuration is defined in `appsettings.json`:

```json
{
  "Api": {
    "BaseUrl": "https://crossdevicetracker-api-hy-erhyaffahwaufsba.southeastasia-01.azurewebsites.net",
    "TimeoutSeconds": 30,
    "SyncIntervalSeconds": 30
  },
  "Device": {
    "Id": null,
    "Jwt": null
  }
}
```

### Device Authentication

The desktop client authenticates with the backend using a **device JWT** obtained during the device linking process. The JWT is persisted locally in `device.json` and loaded on startup. All API requests include this token in the request header:

```
Authorization: Bearer <device_jwt>
```

---

## Related Projects

- **[CrossDeviceTracker.Api](https://github.com/Harshit4847/CrossDeviceTracker.Api)** — Backend API server that stores and processes usage logs.
- **CrossDeviceTracker Dashboard** (future) — Web dashboard for visualizing screen time analytics.

---

## License

This project is part of the CrossDeviceTracker system.
