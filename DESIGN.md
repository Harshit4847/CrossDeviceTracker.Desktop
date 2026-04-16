# CrossDeviceTracker.Desktop - Design Document

---

## 1. Overview

The `CrossDeviceTracker.Desktop` application is responsible for:

- Tracking foreground application usage on Windows
- Measuring active engagement time
- Storing usage data locally (offline-first)
- Preparing data for synchronization with backend API

The system follows a separation of concerns design:

- Core (tracking logic)
- Data (local persistence)
- Sync (future implementation)

## 2. Architecture

```text
Core (Tracking Engine)
        ↓
Data Layer (SQLite Storage)
        ↓
Sync Layer (Future: Backend API)
```

Responsibilities:

| Layer | Responsibility |
|-------|----------------|
| Core | Detect app usage, calculate time, create logs |
| Data | Persist logs locally |
| Sync | Send logs to backend (future) |

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
- `startTime` -> when the session started
- `isRunning` -> loop control flag

### 4.3 Main Loop

```text
while (isRunning):

    currentApp = getCurrentApp()
    currentTime = now

    if currentApp != previousApp:

        if previousApp exists:
            FinalizeSession(currentTime)

        startTime = currentTime
        previousApp = currentApp

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

### 6.1 Interface

`SaveLogAsync(Log log)`

### 6.2 Responsibilities

- Persist logs to SQLite
- Handle storage reliability
- No business logic

## 7. Startup Behavior

```text
previousApp = getCurrentApp()
startTime = currentTime
isRunning = true
```

Tracking starts from current moment.

Prevents fake sessions.

## 8. Shutdown Behavior

```text
Stop():
    FinalizeSession(currentTime)
    isRunning = false
```

Important rules:

- Must finalize last session
- Must save to DB
- Must NOT block on network

## 9. Event Handling

Sessions are finalized on:

- App change
- System lock
- Application shutdown

## 10. Separation of Concerns

Core SHOULD:

- Detect app usage
- Calculate time
- Create logs

Core SHOULD NOT:

- Access database directly
- Send data to backend

## 11. Offline-First Design

- Logs are stored locally first
- Sync happens later

Ensures:

- No data loss
- Works without internet

## 12. Sync Strategy (Future)

```text
Fetch logs where SyncStatus = Pending
        ↓
Send to backend
        ↓
Update status (Sent / Failed)
```

A separate Sync Service will implement this flow.

## 13. Timing Considerations

- Polling interval: 2 seconds
- Possible inaccuracy: +/- 2 seconds
- Backend applies tolerance validation

## 14. Key Design Decisions

| Decision | Reason |
|----------|--------|
| Polling over events | Simplicity |
| GUID for IDs | Offline-safe uniqueness |
| Store EndTime | Query performance and validation |
| SyncStatus enum | Reliable retry logic |
| Separate Core/Data | Clean architecture |

## 15. Summary

The desktop application is designed as a:

- Tracking engine
- Offline-first system
- Cleanly separated architecture

It ensures:

- Accurate session tracking
- Reliable local storage
- Future scalability for backend sync
