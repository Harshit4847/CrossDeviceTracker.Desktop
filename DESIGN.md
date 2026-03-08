# CrossDeviceTracker.Desktop — Design Document

---

## 1. Desktop Client Purpose

The CrossDeviceTracker.Desktop application is responsible for monitoring foreground application activity on Windows devices and reporting usage logs to the backend API.

The desktop client operates as the primary data collection component in the CrossDeviceTracker system.

---

## 2. System Architecture

The CrossDeviceTracker system consists of three major components:

```
User Device (Windows Desktop Client)
        ↓
Collect foreground application usage data

Backend API
        ↓
Store and process time logs

Dashboard (Future)
        ↓
Visualize screen time analytics
```

---

## 3. Foreground Window Detection

The desktop client will detect the currently active window using Windows Win32 APIs.

The detection process follows these steps:

1. Call `GetForegroundWindow()` to obtain the handle of the active window.
2. Use `GetWindowThreadProcessId()` to retrieve the process ID.
3. Use the process ID to identify the application name.

This process will run periodically to monitor application usage.

---

## 4. Application Usage Tracking Strategy

The system will track usage in **time blocks** instead of logging every detection event.

**Example:**

| Field | Value |
|-------|-------|
| Application | Chrome |
| StartTime | 10:00 |
| EndTime | 10:10 |
| Duration | 600 seconds |

When the foreground application changes, the previous block is finalized and a new block begins.

---

## 5. Tracking Interval

The desktop client will periodically check the active window.

Initial implementation will use a **fixed polling interval** (e.g., 2 seconds).

This interval balances accuracy and CPU usage.

---

## 6. Data Flow

```
Foreground Window Detection
        ↓
Track application start time
        ↓
Detect application change
        ↓
Finalize usage block
        ↓
Store usage log locally
        ↓
Send logs to backend API
```

---

## 7. Device Authentication

The desktop client will authenticate with the backend using a **device JWT** obtained during the device linking process.

All API requests from the desktop client will include this device JWT in the `Authorization` header.

**Example header:**

```
Authorization: Bearer <device_jwt>
```

---

## 8. Future Components

The desktop client will later include additional modules:

- Local storage for offline log persistence
- Background service execution
- System tray application
- Log batching and synchronization
