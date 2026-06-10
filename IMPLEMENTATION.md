# Tracking Engine Implementation

## ✅ Completed

### 1. **Models/Log.cs** - Core Log Model
- Represents a single app usage session
- Properties: Id (GUID), AppName, StartTime, EndTime, Duration, SyncStatus, CreatedAt
- SyncStatus enum: Pending, Sent, Failed
- Pretty toString() for debugging

### 2. **Data/ILogRepository.cs** - Repository Interface
- `SaveLogAsync(Log log)` - Persist a log session
- `GetPendingLogsAsync()` - Query logs not yet synced
- `UpdateSyncStatusAsync(Guid logId, SyncStatus status)` - Update sync state
- `InitializeAsync()` - Set up database

### 3. **Data/SqliteLogRepository.cs** - SQLite Implementation
- Creates `logs.db` with Logs table on first run
- Full async/await support
- Stores all Log properties
- Supports future sync layer queries

### 4. **Core/AppTracker.cs** - Main Tracking Engine
- **Polling**: Every 2 seconds (configurable)
- **Win32 Integration**: Uses GetForegroundWindow() and GetWindowThreadProcessId()
- **Session Lifecycle**:
  - Initializes with current app on startup
  - Detects app changes via polling
  - Finalizes session when app changes
  - Saves logs to SQLite
- **Graceful Shutdown**: Handles Ctrl+C, finalizes last session before exit
- **Error Handling**: Catches exceptions in tracking loop, continues running

### 5. **Program.cs** - Entry Point
- Initializes SqliteLogRepository and AppTracker
- Registers Ctrl+C handler for clean shutdown
- Starts async tracking loop

### 6. **Dependencies**
- Added `System.Data.SQLite` (v1.0.119)

## 📋 Architecture

```
Program.cs (Entry Point)
    ↓
AppTracker (Polling Engine)
    ↓
GetCurrentApp() → Win32 APIs → Process.ProcessName
    ↓
Detect App Changes
    ↓
FinalizeSession() → Create Log
    ↓
ILogRepository.SaveLogAsync()
    ↓
SqliteLogRepository → SQLite Database (logs.db)
```

## 🚀 How It Works

1. **Startup**: Tracker initializes with the currently active app
2. **Polling Loop**: Every 2 seconds, checks if foreground app changed
3. **Session End**: When app changes, finalizes previous session:
   - Calculates duration
   - Creates Log object
   - Saves to SQLite
4. **Shutdown**: Ctrl+C triggers final session save, clean exit

## 📊 Database Schema

```sql
CREATE TABLE Logs (
    Id TEXT PRIMARY KEY,           -- GUID as string
    AppName TEXT NOT NULL,         -- Process name (e.g., "chrome", "code")
    StartTime TEXT NOT NULL,       -- ISO 8601 format
    EndTime TEXT NOT NULL,         -- ISO 8601 format
    DurationSeconds REAL NOT NULL, -- Calculated duration
    SyncStatus TEXT NOT NULL,      -- Pending | Sent | Failed
    CreatedAt TEXT NOT NULL        -- UTC timestamp
)
```

## 🔄 Sync Status Workflow

```
Pending (Initial)
    ↓
[Future: Sync Service sends to backend]
    ↓
Sent or Failed
    ↓
[Future: Query via GetPendingLogsAsync()]
```

## 📝 Example Output

```
🎯 App Tracker started
Initial app: explorer
Polling every 2 seconds...

→ App changed to: code
✓ Logged: code: 2026-06-10 12:45:00 → 2026-06-10 12:45:30 (30s)
→ App changed to: chrome
✓ Logged: chrome: 2026-06-10 12:45:30 → 2026-06-10 12:46:00 (30s)

⏹️  Stopping tracker...
✓ Logged: chrome: 2026-06-10 12:46:00 → 2026-06-10 12:46:10 (10s)
✅ Tracker stopped
```

## 🎯 Alignment with Design

✅ **Polling Strategy**: 2-second interval as specified  
✅ **Separation of Concerns**: Core (tracking) → Data (persistence) → Sync (future)  
✅ **Offline-First**: All logs saved locally before sync  
✅ **Clean Shutdown**: Finalizes last session on Ctrl+C  
✅ **GUID for IDs**: Ensures offline-safe uniqueness  
✅ **SyncStatus Tracking**: Ready for future sync service  

## 📦 Ready for Next Phases

- **Phase 2**: Implement Sync Service (send pending logs to backend)
- **Phase 3**: Add device authentication (DeviceAuthService)
- **Phase 4**: Background execution & system tray
- **Phase 5**: Configuration and settings management
