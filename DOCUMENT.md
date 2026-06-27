# CrossDeviceTracker.Desktop — Documentation

---

## Current Project Status

All core features have been implemented and are functional.

### Completed

* Foreground Window Detection (Win32 APIs)
* App Change Detection
* Session Tracking and Duration Calculation
* SQLite Persistence (offline-first)
* Lock / Unlock Handling (LockApp exclusion)
* Windows Forms UI (status display, current app, pending logs)
* System Tray Integration (minimize to tray, background tracking, context menu)
* Desktop Device Linking (token-based pairing with backend)
* Device JWT Acquisition and Persistence (`device.json`)
* Backend Time Log Synchronization (`SyncService`, 30s interval)
* Unauthorized Device Handling (auto-relink on 401)
* Background Sync Service (`SyncService`)
* Diagnostic and Debug Utilities (`SyncDebugHelper`)
* Clear All Logs

### Sync Pipeline

```
Tracker Engine → SQLite Storage → Pending Logs → SyncService → ApiClient → Backend API
                                                                    ↓
                                                          Update SyncStatus (Sent / Failed)
```

---

## 1. Project Initialization

Created a new GitHub repository for the Windows desktop client of the CrossDeviceTracker system.

**Repository Name:** `CrossDeviceTracker.Desktop`

The desktop application tracks foreground application usage on Windows and sends usage logs to the backend API.

---

## 2. Project Structure

The project is a .NET Windows Forms application with a layered architecture.

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
├── Program.cs                         # Application entry point and DI setup
├── appsettings.json                   # API base URL and configuration
├── CrossDeviceTracker.Desktop.csproj  # Project file
├── DESIGN.md                          # Architecture and design document
├── DOCUMENT.md                        # This file
├── IMPLEMENTATION.md                  # Implementation details
└── README.md                          # Project overview
```

---

## 3. Role in the System

The CrossDeviceTracker.Desktop application acts as the Windows client responsible for:

- Detecting the currently active foreground application
- Tracking usage duration of each application
- Storing usage logs locally in SQLite
- Synchronizing usage logs with the CrossDeviceTracker backend API
- Managing device authentication and linking

---

## 4. Screen Time Definition

The tracker measures active foreground application usage.

Time spent on Windows lock screen (LockApp) is excluded from tracking and analytics.

---

## 5. Development Plan

The desktop client was developed incrementally in the following phases:

| Phase | Goal | Status |
|-------|------|--------|
| **Phase 1** | Detect the active foreground window on Windows | Complete |
| **Phase 2** | Track time spent on each application and generate time blocks | Complete |
| **Phase 3** | Store usage logs locally (SQLite) | Complete |
| **Phase 4** | Lock/unlock session handling | Complete |
| **Phase 5** | Device JWT authentication and linking | Complete |
| **Phase 6** | Backend synchronization (send usage logs to API) | Complete |
| **Phase 7** | Background execution and system tray integration | Complete |

---

## 6. Foreground Window Detection

The application polls the active window every 2 seconds via `AppTracker`, detects app changes, and persists completed sessions to SQLite. The Windows Forms UI displays the current app and pending/failed activity logs.

---

## 7. Backend Integration

The desktop client communicates with the CrossDeviceTracker backend API hosted on Azure.

**Base URL:** Configured in `appsettings.json`

### Endpoints Used

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/devices/link` | POST | Link device using a one-time token |
| `/api/timelogs` | POST | Submit individual time log entries |

### Authentication

Device authentication uses the device JWT generated during the desktop linking process. All API requests include the JWT in the `Authorization: Bearer <token>` header.

---

## 8. Tracking Engine Design

### Goal

Build a Windows desktop app that:

- Tracks foreground app usage
- Calculates time spent per app
- Stores logs locally
- Syncs to backend

### Architecture

| Layer | Role |
|-------|------|
| `Core` | Logic (tracking engine) |
| `Data` | Storage (SQLite) |
| `Services` | Network (sync, auth, API) |
| `UI` | Display (WinForms, tray) |

### Core Logic (Tracking Engine)

#### Polling Strategy

Runs every 2 seconds using:

- `GetForegroundWindow`
- `GetWindowThreadProcessId`
- `Process.GetProcessById`

#### Detection Pipeline

`HWND` -> `ProcessId` -> `Process` -> `ProcessName`

#### State (Class Fields)

- `previousApp`
- `currentApp`
- `sessionStartTime`
- `isRunning`

#### Loop Flow

```text
while (isRunning)
	rawApp = getCurrentApp()
	trackableApp = ToTrackableApp(rawApp)   // null if LockApp
	currentTime = now

	if trackableApp != previousApp
		if previousApp exists
			FinalizeSession(currentTime)

		startTime = currentTime
		previousApp = trackableApp

	wait 2 seconds
```

#### FinalizeSession()

```text
endTime = currentTime
duration = endTime - startTime

create Log object

SaveLogAsync(log)
```

### Log Model (Core)

- `Id` (GUID)
- `AppName`
- `StartTime`
- `EndTime`
- `Duration`
- `SyncStatus` (`Pending` / `Sent` / `Failed`)
- `CreatedAt`

### Key Concepts

- `Log` = object in memory
- DB row = stored version of `Log`
- Client duration = hint
- Server duration = truth

### Data Layer

Interface (`ILogRepository`):

- `SaveLogAsync(Log log)`
- `GetPendingLogsAsync()` — returns logs with Pending or Failed status
- `UpdateSyncStatusAsync(Guid logId, SyncStatus status)`
- `InitializeAsync()` — create database and tables
- `DeleteAllLogsAsync()` — clear all logs

Rules:

- Async (non-blocking)
- Called by Core
- Handles SQLite

`SyncStatus` lifecycle:

- `Pending` -> created locally, not yet synced
- `Sent` -> successfully synced to backend
- `Failed` -> sync attempt failed (retry on next cycle)

### Important Design Rules

Core should NOT:

- Talk directly to DB
- Send to backend

Core should:

- Detect
- Calculate
- Create log
- Pass to Data layer

### Startup Logic

```text
Load device.json → restore DeviceJwt
Check IsLinkedAsync()
    Not linked → DeviceLinkingDialog
    Linked → MainForm

MainForm:
    Initialize repository
    Start AppTracker (background)
    Start SyncService (background)
    Start UI timer (2s)
```

Start tracking from now.

### Stop Logic

```text
ExitApplication()
    AppTracker.StopAsync()
        FinalizeSession(currentTime)
        isRunning = false
    SyncService.StopAsync()
        Final sync attempt
    Dispose tray icon
    Application.Exit()
```

### System Behavior

A log is created when app changes (session ends).

Not:

- every 2 seconds
- when app starts

### Edge Cases

| Case | Status |
|------|--------|
| App exit / shutdown | Finalizes last session on close |
| App change | Finalizes session and saves to SQLite |
| System lock / unlock | LockApp excluded; session finalized on lock, resumed on unlock |
| Network unavailable | Logs stored locally, synced when connectivity returns |
| 401 Unauthorized | DeviceUnauthorized event fires, user prompted to relink |

Always finalize the last session on shutdown.

### Two Engines

1. **Tracker Engine**
   - Detect usage
   - Create logs
   - Save locally
2. **Sync Engine**
   - Read pending/failed logs
   - Send to backend via ApiClient
   - Update SyncStatus (Sent / Failed)
   - Handle 401 → trigger relink

### Device Linking Flow

1. User generates a link token from the backend
2. User enters token in `DeviceLinkingDialog`
3. `DeviceAuthService.LinkDeviceAsync()` calls `ApiClient.LinkDeviceAsync()` → POST `/api/devices/link`
4. Backend validates token, creates device, returns JWT
5. JWT persisted in `device.json`
6. `ApiClient.DeviceJwt` set for authenticated requests
7. On startup, JWT is loaded from `device.json` automatically

### Future Consideration

Crash recovery for unfinalized sessions is not currently implemented.

Sessions are persisted when finalized (app change or application shutdown).
