# CrossDeviceTracker.Desktop - Design Document

---

## 1. Overview

The `CrossDeviceTracker.Desktop` application is responsible for:

- Tracking foreground application usage on Windows
- Measuring active engagement time
- Storing usage data locally (offline-first)
- Synchronizing data with the backend API
- Managing device authentication via JWT

The system follows a separation of concerns design:

- Core (tracking logic)
- Data (local persistence)
- Services (authentication and synchronization)
- UI (Windows Forms and system tray)

## 2. Architecture

```text
Program.cs (Entry Point / DI Setup)
        ↓
┌───────────────────────────────────────────────────┐
│  UI Layer                                         │
│    MainForm (WinForms + system tray)              │
│    DeviceLinkingDialog (token entry dialog)        │
├───────────────────────────────────────────────────┤
│  Core Layer                                       │
│    AppTracker (Win32 polling engine, 2s interval) │
├───────────────────────────────────────────────────┤
│  Services Layer                                   │
│    SyncService (background sync, 30s interval)    │
│    ApiClient (HTTP client, JWT auth, sync/link)   │
│    DeviceAuthService (JWT persistence, linking)   │
├───────────────────────────────────────────────────┤
│  Data Layer                                       │
│    ILogRepository → SqliteLogRepository (SQLite)  │
└───────────────────────────────────────────────────┘
```

Responsibilities:

| Layer | Responsibility |
|-------|----------------|
| Core | Detect app usage, calculate time, create logs |
| Data | Persist logs to SQLite, query by sync status |
| Services | Device linking, JWT management, backend sync |
| UI | Display tracking status, system tray lifecycle |

## 3. Tracking Strategy

### 3.1 Polling

- The system uses polling every 2 seconds
- Simpler than event-based tracking
- Acceptable trade-off between accuracy and performance

### 3.2 Foreground Detection Pipeline

```text
GetForegroundWindow()
        ↓
GetWindowThreadProcessId()
        ↓
Process.GetProcessById()
        ↓
Process.ProcessName
```

## 4. Tracking Logic

### 4.1 Core Idea

Track one active application at a time.

A session ends when:

- App changes
- System locks
- Application stops

### 4.2 State Variables

The tracker maintains:

- `previousApp` -> currently tracked app
- `currentApp` -> raw current foreground app
- `startTime` -> when the session started
- `isRunning` -> loop control flag

### 4.3 Main Loop

```text
while (isRunning):

    rawApp = getCurrentApp()
    trackableApp = ToTrackableApp(rawApp)   // null if LockApp
    currentTime = now

    if trackableApp != previousApp:

        if previousApp exists:
            FinalizeSession(currentTime)

        startTime = currentTime
        previousApp = trackableApp

    wait 2 seconds
```

### 4.4 Session Finalization

```text
endTime = currentTime
duration = endTime - startTime

create Log object

SaveLogAsync(log)
```

## 5. Log Model

Represents a single app usage session.

- `Id` (GUID)
- `AppName`
- `StartTime`
- `EndTime`
- `Duration`
- `SyncStatus`
- `CreatedAt`

### 5.1 SyncStatus

- `Pending` -> not yet synced
- `Sent` -> successfully synced
- `Failed` -> failed to sync

### 5.2 Design Principles

- `Log` is a Core model, not Data-specific
- Created in memory, then persisted
- Used for local storage
- Used for backend sync
- Used for debugging

## 6. Data Layer

### 6.1 Interface (`ILogRepository`)

- `SaveLogAsync(Log log)` - Persist a log session
- `GetPendingLogsAsync()` - Query logs with Pending or Failed status
- `UpdateSyncStatusAsync(Guid logId, SyncStatus status)` - Update sync state
- `InitializeAsync()` - Create database and tables
- `DeleteAllLogsAsync()` - Clear all logs

### 6.2 Responsibilities

- Persist logs to SQLite (`logs.db`)
- Handle storage reliability
- No business logic

## 7. Startup Behavior

```text
Load device.json → restore DeviceJwt if exists
Check IsLinkedAsync()
    If not linked → show DeviceLinkingDialog
    If linked → proceed to MainForm

MainForm:
    Initialize repository
    Start AppTracker (background task)
    Start SyncService (background task)
    Start UI update timer (2s interval)

AppTracker:
    previousApp = getCurrentApp()
    startTime = currentTime
    isRunning = true
```

Tracking starts from current moment. Prevents fake sessions.

## 8. Shutdown Behavior

```text
ExitApplication():
    Stop AppTracker:
        FinalizeSession(currentTime)
        isRunning = false
    Stop SyncService:
        Final sync attempt
        isRunning = false
    Dispose tray icon
    Application.Exit()
```

Important rules:

- Must finalize last session
- Must save to DB
- Final sync attempt before exit

## 9. Event Handling

Sessions are finalized on:

- App change
- System lock (LockApp detected → previous session finalized, LockApp excluded)
- Application shutdown (ExitApplication via tray menu)

## 10. Separation of Concerns

Core SHOULD:

- Detect app usage
- Calculate time
- Create logs

Core SHOULD NOT:

- Access database directly (uses ILogRepository)
- Send data to backend
- Manage authentication

## 11. Offline-First Design

- Logs are stored locally first in SQLite
- Sync happens asynchronously via SyncService

Ensures:

- No data loss
- Works without internet
- Logs accumulate locally and sync when connectivity is available

## 12. Sync Strategy

```text
SyncService loop (every 30 seconds):
    ↓
ApiClient.SyncPendingLogsAsync():
    ↓
Fetch logs where SyncStatus IN (Pending, Failed)
    ↓
For each log → POST to /api/timelogs
    ↓
On success → UpdateSyncStatusAsync(logId, Sent)
On failure → UpdateSyncStatusAsync(logId, Failed)
On 401     → Fire DeviceUnauthorized event → prompt relink
```

### 12.1 Sync Payload

```json
{
  "appName": "chrome",
  "startTime": "2026-06-10T12:45:00.0000000Z",
  "durationSeconds": 30
}
```

### 12.2 Authentication

All sync requests include the device JWT:

```
Authorization: Bearer <device_jwt>
```

## 13. Device Authentication

### 13.1 Linking Flow

1. User generates a desktop link token from the backend/mobile app
2. User enters the token in `DeviceLinkingDialog`
3. `DeviceAuthService` calls `ApiClient.LinkDeviceAsync(linkToken)` which POSTs to `/api/devices/link` with:
   - `linkToken`
   - `deviceName` (machine name)
   - `platform` ("Windows")
4. Backend validates token, creates device record, returns device JWT
5. JWT is persisted locally in `device.json`
6. `ApiClient.DeviceJwt` is set for authenticated requests

### 13.2 JWT Persistence

- Stored in `device.json` alongside the application binary
- Loaded on startup via `DeviceAuthService.LoadDeviceAsync()`
- Contains: `DeviceJwt`, `DeviceName`, `LinkedAt`

### 13.3 Unauthorized Handling

- On HTTP 401 from sync requests, `ApiClient` fires `DeviceUnauthorized` event
- `MainForm` handles the event: shows balloon tip, unlinks device, opens relink dialog
- User can also manually relink via the tray context menu

## 14. Timing Considerations

- Polling interval: 2 seconds
- Sync interval: 30 seconds
- Possible tracking inaccuracy: +/- 2 seconds
- Backend applies tolerance validation

## 15. Key Design Decisions

| Decision | Reason |
|----------|--------|
| Polling over events | Simplicity |
| GUID for IDs | Offline-safe uniqueness |
| Store EndTime | Query performance and validation |
| SyncStatus enum | Reliable retry logic |
| Separate Core/Data/Services | Clean architecture |
| Offline-first storage | No data loss on network failure |
| Device JWT in local file | Persistent auth across restarts |
| 30-second sync interval | Balance between timeliness and efficiency |
| LockApp exclusion | Only track active engagement |

## 16. Summary

The desktop application is designed as a:

- Tracking engine (Win32 foreground detection)
- Offline-first system (SQLite local storage)
- Authenticated sync client (device JWT + background sync)
- Background Windows application (system tray integration)

It ensures:

- Accurate session tracking
- Reliable local storage
- Secure device authentication
- Automatic backend synchronization
