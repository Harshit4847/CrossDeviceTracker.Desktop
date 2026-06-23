# CrossDeviceTracker.Desktop — Documentation

---

## Development Update — Device Linking Integration

### Status: Completed

The desktop application is now successfully able to link with the backend using the desktop linking flow.

### Completed Flow

1. User generates a desktop linking token from the backend.
2. User enters the linking token into the desktop application.
3. Desktop application sends:

   * Link Token
   * Device Name
   * Platform
4. Backend validates the token.
5. Backend creates a device record.
6. Backend issues a Device JWT.
7. Desktop application receives and stores the Device JWT.
8. Linked device appears in the backend database.

### Verification

Confirmed:

* Desktop linking token is accepted by the backend.
* Device record is created successfully.
* Device JWT is returned successfully.
* Desktop application can complete the linking process without errors.

### Current Project Status

#### Completed

* Foreground Window Detection
* App Change Detection
* Session Tracking
* Duration Calculation
* SQLite Persistence
* Lock / Unlock Handling
* Windows Forms UI
* Desktop Device Linking
* Device JWT Acquisition

#### In Progress

* Device JWT Persistence
* Backend Time Log Synchronization
* Unauthorized Device Handling
* Background Sync Service

### Current Issue Under Investigation

Time logs are successfully created and stored locally in SQLite with SyncStatus = Pending.

However, logs are not yet appearing in the backend database.

Current sync pipeline status:

Tracker Engine → SQLite Storage → Pending Logs → Backend Sync ❌

The next debugging task is to determine whether:

* Sync requests are not being sent.
* Device JWT is not attached correctly.
* Backend is rejecting requests.
* Time log endpoint validation is failing.

### Next Milestone

Implement and verify successful upload of pending time logs from SQLite to the backend API and update SyncStatus from Pending to Sent after successful synchronization.

---

## 1. Project Initialization

Created a new GitHub repository for the Windows desktop client of the CrossDeviceTracker system.

**Repository Name:** `CrossDeviceTracker.Desktop`

The desktop application will be responsible for tracking foreground application usage on Windows and sending usage logs to the backend API.

---

## 2. Project Structure

The project is a .NET Windows Forms application with a layered architecture.

Current repository structure:

```
CrossDeviceTracker.Desktop
│
├── Core/
│   └── AppTracker.cs              # Foreground app tracking engine
├── Data/
│   ├── ILogRepository.cs          # Persistence interface
│   └── SqliteLogRepository.cs     # SQLite implementation
├── Models/
│   └── Log.cs                     # Session log model
├── Services/
│   ├── ApiClient.cs               # Backend API client
│   ├── DeviceAuthService.cs       # Device JWT auth (planned)
│   └── SyncService.cs             # Log sync service (planned)
├── MainForm.cs                    # Windows Forms UI
├── DeviceLinkingDialog.cs         # Device linking UI
├── Program.cs                     # Application entry point
├── appsettings.json               # Configuration
├── DESIGN.md
├── DOCUMENT.md
├── IMPLEMENTATION.md
├── README.md
└── .gitignore
```

---

## 3. Role in the System

The CrossDeviceTracker.Desktop application will act as the Windows client responsible for:

- Detecting the currently active foreground application
- Tracking usage duration of each application
- Storing usage logs locally
- Synchronizing usage logs with the CrossDeviceTracker backend API

---

## 4. Screen Time Definition

The tracker measures active foreground application usage.

Time spent on Windows lock screen (LockApp) is excluded from tracking and analytics.

---

## 5. Development Plan

The desktop client is being developed incrementally in the following phases:

| Phase | Goal | Status |
|-------|------|--------|
| **Phase 1** | Detect the active foreground window on Windows | ✅ Complete |
| **Phase 2** | Track time spent on each application and generate time blocks | ✅ Complete |
| **Phase 3** | Store usage logs locally (SQLite) | ✅ Complete |
| **Phase 4** | Lock/unlock session handling | ✅ Complete |
| **Phase 5** | Device JWT authentication | ⏳ Planned |
| **Phase 6** | Backend synchronization (send usage logs to API) | ⏳ Planned |
| **Phase 7** | Background execution and system tray integration | ⏳ Planned |

---

## 6. First Development Goal

**Implement foreground window detection.** ✅ Done

The application polls the active window every 2 seconds via `AppTracker`, detects app changes, and persists completed sessions to SQLite. The Windows Forms UI displays current app and recent activity.

---

## 7. Backend Integration

The desktop client will communicate with the CrossDeviceTracker backend API.

Device authentication will use the device JWT generated during the desktop linking process.

---

## 8. Tracking Engine Design (Finalized)

### Goal

Build a Windows desktop app that:

- Tracks foreground app usage
- Calculates time spent per app
- Stores logs locally
- Syncs later to backend

### Architecture

- `Core` -> Logic (tracking engine)
- `Data` -> Storage (SQLite)
- `Core` = brain
- `Data` = memory
- `Sync` (future) = network

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
- `startTime`
- `isRunning`
- `dataService`

#### Loop Flow

```text
while (isRunning)
	currentApp = getCurrentApp()
	currentTime = now

	if currentApp != previousApp
		if previousApp exists
			FinalizeSession(currentTime)

		startTime = currentTime
		previousApp = currentApp

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

Interface:

- `SaveLogAsync(Log log)`

Rules:

- Async (non-blocking)
- Called by Core
- Handles SQLite

`SyncStatus` lifecycle:

- `Pending` -> not sent
- `Sent` -> success
- `Failed` -> error (retry later)

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
previousApp = getCurrentApp()
startTime = currentTime
isRunning = true
```

Start tracking from now.

### Stop Logic

```text
Stop()
	FinalizeSession(currentTime)
	isRunning = false
```

Do NOT:

- Wait for backend
- Do network calls

### System Behavior

A log is created when app changes (session ends).

Not:

- every 2 seconds
- when app starts

### Edge Cases

| Case | Status |
|------|--------|
| App exit / shutdown | ✅ Finalizes last session on close |
| App change | ✅ Finalizes session and saves to SQLite |
| System lock / unlock | ✅ LockApp excluded; session finalized on lock, resumed on unlock |

Always finalize the last session on shutdown.

### Two Engines

1. **Tracker Engine** ✅
   - Detect usage
   - Create logs
   - Save locally
2. **Sync Engine** ⏳
   - Read pending logs
   - Send to backend
   - Update status

### Current Stage

Core tracking, session management, lock/unlock handling, Windows Forms UI, and SQLite persistence are complete. Next priorities:

1. **Phase 5** — Device JWT authentication: link device and attach token to API requests.
2. **Phase 6** — Backend synchronization: send pending logs via `SyncService`.
3. **Phase 7** — Background execution and system tray: keep tracking when minimized; tray icon with show/exit.

### Future Consideration

Crash recovery for unfinalized sessions is not currently implemented.

Sessions are persisted when finalized (app change or application shutdown).