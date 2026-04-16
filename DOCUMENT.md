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

---

## 7. Tracking Engine Design (Finalized)

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

Handle:

- App exit
- System lock
- Shutdown

Always finalize the last session.

### Two Engines

1. Tracker Engine (Now)
   - Detect usage
   - Create logs
   - Save locally
2. Sync Engine (Later)
   - Read pending logs
   - Send to backend
   - Update status

### Current Stage (Execution Now)

Design is complete. No more planning needed.

Do next in this exact order:

1. Print active app every 2 seconds.
2. Detect app change.
3. Print session logs.