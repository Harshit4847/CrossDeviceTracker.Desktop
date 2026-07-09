# CrossDeviceTracker.Desktop — Implementation Details

## Completed Components

### 1. **Models/Log.cs** — Core Log Model
- Represents a single app usage session
- Properties: `Id` (GUID), `AppName`, `StartTime`, `EndTime`, `Duration`, `SyncStatus`, `CreatedAt`
- `SyncStatus` enum: `Pending`, `Sent`, `Failed`
- `ToString()` override for debugging output

### 2. **Models/LinkDesktopResponse.cs** — Linking Response DTO
- Deserializes the backend response from `/api/devices/link`
- Contains `DeviceJwt` property

### 3. **Data/ILogRepository.cs** — Repository Interface
- `SaveLogAsync(Log log)` — Persist a log session
- `GetPendingLogsAsync()` — Query logs with `Pending` or `Failed` status
- `UpdateSyncStatusAsync(Guid logId, SyncStatus status)` — Update sync state
- `InitializeAsync()` — Create database and tables
- `DeleteAllLogsAsync()` — Clear all logs

### 4. **Data/SqliteLogRepository.cs** — SQLite Implementation
- Creates `logs.db` with `Logs` table on first run
- Full async/await support via `System.Data.SQLite`
- Stores all Log properties as text (ISO 8601 for dates, REAL for duration)
- Supports querying by sync status for retry logic

### 5. **Core/AppTracker.cs** — Foreground Tracking Engine
- **Polling**: Every 2 seconds (configurable via `PollingIntervalMs`)
- **Win32 Integration**: Uses `GetForegroundWindow()` and `GetWindowThreadProcessId()`
- **LockApp Exclusion**: `ToTrackableApp()` maps LockApp to `null`, preventing lock screen from being tracked
- **Session Lifecycle**:
  - Initializes with current app on startup
  - Detects app changes via polling
  - Finalizes session when app changes (calculates duration, creates Log, saves to SQLite)
- **Graceful Shutdown**: `StopAsync()` finalizes the last session before exit
- **Error Handling**: Catches exceptions in tracking loop, continues running

### 6. **Services/ApiClient.cs** — Backend API Client
- **IApiClient Interface**: `SyncPendingLogsAsync()`, `SendLogAsync(Log)`, `LinkDeviceAsync(string)`
- **Sync Logic**: Fetches pending/failed logs from repository, POSTs each to `/api/timelogs`, updates sync status
- **Device Linking**: POSTs to `/api/devices/link` with `linkToken`, `deviceName`, `platform`
- **JWT Auth**: Attaches `Authorization: Bearer <jwt>` header to all API requests
- **401 Handling**: Fires `DeviceUnauthorized` event on HTTP 401 response
- **Payload Format**: `{ appName, startTime (ISO 8601), durationSeconds }`
- **Error Handling**: Catches and logs exceptions, returns success/failure per log

### 7. **Services/DeviceAuthService.cs** — Device Authentication
- **IDeviceAuthService Interface**: `IsLinkedAsync()`, `LoadDeviceJwtAsync()`, `LoadDeviceAsync()`, `LinkDeviceAsync(string)`, `SaveDeviceJwtAsync(string)`, `UnlinkAsync()`
- **DeviceAuthState**: Stores `DeviceId`, `DeviceJwt`, `DeviceName`, `LinkedAt`, `Verify`
- **Persistence**: Saves/loads JWT and device metadata to/from `device.json` (JSON, next to application binary)
- **Linking Flow**: Calls `ApiClient.LinkDeviceAsync()`, persists returned JWT with DeviceAuthState, sets `ApiClient.DeviceJwt`
- **Unlinking**: Deletes `device.json`, clears `ApiClient.DeviceJwt`

### 8. **Services/SyncService.cs** — Background Sync Service
- **ISyncService Interface**: `StartAsync()`, `StopAsync()`, `SyncOnceAsync()`
- **Loop**: Calls `ApiClient.SyncPendingLogsAsync()` every 30 seconds
- **Graceful Shutdown**: Performs a final sync attempt before stopping
- **Error Handling**: Catches and logs exceptions, continues running

### 9. **MainForm.cs** — Windows Forms UI
- **Tracking Display**: Shows current app, tracking status, and device link status
- **Pending Logs ListView**: Displays pending/failed logs with app name, start time, duration, and sync status
- **System Tray**: `NotifyIcon` with context menu (Show, Relink Device, Exit)
- **Minimize to Tray**: Closing the window hides to tray; balloon tip confirms background tracking
- **UI Timer**: Refreshes UI every 2 seconds
- **Relink Integration**: Handles `DeviceUnauthorized` event to prompt re-linking
- **Clear Logs**: Button to delete all local logs
- **Graceful Exit**: Stops tracker and sync service, disposes tray icon

### 10. **DeviceLinkingDialog.cs** — Device Linking UI
- Modal dialog for entering a device link token
- Multiline text box for pasting tokens
- Status label for feedback (linking progress, errors)
- Optional custom message parameter for forced relinking scenarios
- Calls `DeviceAuthService.LinkDeviceAsync()` on submit
- Returns `DialogResult.OK` on successful link

### 11. **SyncDebugHelper.cs** — Diagnostic Utilities
- `PrintDeviceStatusAsync()` — Shows device name, auth status, JWT preview
- `PrintAllLogsAsync()` — Tabular display of all logs with sync status
- `PrintSyncStatisticsAsync()` — Counts of Sent, Pending, Failed logs
- `PrintDiagnosticReportAsync()` — Full diagnostic report combining all above

### 12. **Program.cs** — Application Entry Point
- Uses manual instantiation (no DI container)
- Creates `SqliteLogRepository`
- Creates `ApiClient` with repository
- Creates `DeviceAuthService` with ApiClient
- Loads saved device JWT on startup via `DeviceAuthService.LoadDeviceAsync()`
- Sets `ApiClient.DeviceJwt` from loaded device
- If not linked, shows `DeviceLinkingDialog` (exits if cancelled)
- Launches `MainForm` with dependencies

---

## Architecture

```
Program.cs (Entry Point - Manual Instantiation)
    ↓
Create SqliteLogRepository
    ↓
Create ApiClient(repository)
    ↓
Create DeviceAuthService(apiClient)
    ↓
DeviceAuthService.LoadDeviceAsync() → restore JWT from device.json
    ↓
Set ApiClient.DeviceJwt
    ↓
IsLinkedAsync()? → No → DeviceLinkingDialog → LinkDeviceAsync()
    ↓                                              ↓
    Yes                                    ApiClient.LinkDeviceAsync()
    ↓                                              ↓
MainForm                                   Save DeviceAuthState → device.json
    ├── AppTracker.StartAsync()            Set ApiClient.DeviceJwt
    │     ↓
    │   Polling Loop (2s)
    │     ↓
    │   GetCurrentApp() → Win32 APIs → Process.ProcessName
    │     ↓
    │   Detect App Change → FinalizeSession()
    │     ↓
    │   ILogRepository.SaveLogAsync() → SQLite (logs.db)
    │
    ├── SyncService.StartAsync()
    │     ↓
    │   Sync Loop (30s)
    │     ↓
    │   ApiClient.SyncPendingLogsAsync()
    │     ↓
    │   GetPendingLogsAsync() → POST /api/timelogs → UpdateSyncStatus()
    │     ↓
    │   On 401 → DeviceUnauthorized event → Relink prompt
    │
    └── UI Timer (2s)
          ↓
        Update current app, status, pending logs list
```

---

## How It Works

1. **Startup**: Loads saved JWT, checks device link status, shows linking dialog if needed
2. **Tracking**: `AppTracker` polls every 2 seconds, detects foreground app changes
3. **Session End**: When app changes, finalizes previous session (calculates duration, creates Log, saves to SQLite)
4. **Sync**: `SyncService` pushes pending logs to backend every 30 seconds via `ApiClient`
5. **Auth Failure**: On 401, fires `DeviceUnauthorized` → unlinks device → shows relink dialog
6. **Shutdown**: Finalizes last tracking session, performs final sync attempt, exits cleanly

---

## Database Schema

```sql
CREATE TABLE Logs (
    Id TEXT PRIMARY KEY,           -- GUID as string
    AppName TEXT NOT NULL,         -- Process name (e.g., "chrome", "code")
    StartTime TEXT NOT NULL,       -- ISO 8601 format
    EndTime TEXT NOT NULL,         -- ISO 8601 format
    DurationSeconds REAL NOT NULL, -- Calculated duration in seconds
    SyncStatus TEXT NOT NULL,      -- Pending | Sent | Failed
    CreatedAt TEXT NOT NULL        -- UTC timestamp, ISO 8601
)
```

---

## Sync Status Workflow

```
Pending (created locally)
    ↓
SyncService (every 30s) → ApiClient.SyncPendingLogsAsync()
    ↓                              ↓
  Success                        Failure
    ↓                              ↓
  Sent                          Failed
                                   ↓
                          Retried on next sync cycle
```

---

## Device Authentication Flow

```
First Launch:
    device.json not found → DeviceLinkingDialog
        ↓
    User pastes link token → LinkDeviceAsync()
        ↓
    POST /api/devices/link { linkToken, deviceName, platform }
        ↓
    Backend returns { deviceJwt }
        ↓
    Save to device.json, set ApiClient.DeviceJwt

Subsequent Launches:
    device.json found → LoadDeviceAsync() → set ApiClient.DeviceJwt

Token Expiry / Revocation:
    API returns 401 → DeviceUnauthorized event
        ↓
    UnlinkAsync() → delete device.json
        ↓
    Show DeviceLinkingDialog → re-link
```

---

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `System.Data.SQLite.Core` | 1.0.119 | SQLite database access |
| `Microsoft.Extensions.Http` | 10.0.0 | HTTP client infrastructure |

---

## Example Output

```
🎯 App Tracker started
Initial app: explorer
Polling every 2 seconds...

→ App changed to: code
✓ Logged: code: 2024-01-15 12:45:00 → 2024-01-15 12:45:30 (30s)
→ App changed to: chrome
✓ Logged: chrome: 2024-01-15 12:45:30 → 2024-01-15 12:46:00 (30s)

🔄 Sync Service started (interval: 30s)
🔄 Sync Service: Syncing 2 log(s)...
  Sent: code (30s)
  Sent: chrome (30s)
✅ Sync complete: 2 succeeded, 0 failed

⏹️  Stopping tracker...
✓ Logged: chrome: 2024-01-15 12:46:00 → 2024-01-15 12:46:10 (10s)
✅ Tracker stopped
```

---

## Alignment with Design

- **Polling Strategy**: 2-second interval as specified
- **Separation of Concerns**: Core (tracking) → Data (persistence) → Services (sync, auth) → UI (display)
- **Manual Instantiation**: Program.cs uses manual dependency creation instead of DI container
- **Offline-First**: All logs saved locally before sync
- **Clean Shutdown**: Finalizes last session, performs final sync attempt
- **GUID for IDs**: Ensures offline-safe uniqueness
- **SyncStatus Tracking**: Full lifecycle with retry logic for failed logs
- **Device Authentication**: JWT-based auth with DeviceAuthState persistence and auto-relink
- **Background Sync**: 30-second interval with graceful shutdown
- **System Tray**: Minimize-to-tray with context menu (Show, Relink, Exit)
- **Diagnostic Tools**: SyncDebugHelper provides device status, log inspection, and sync statistics
