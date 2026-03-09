# RemoteDesk 🖥️

A remote desktop system that lets you view and control a Windows PC through a browser — similar to TeamViewer.

---

## Overview

The system consists of **3 components**:

```
[Windows Client App]  ←──SignalR──→  [ASP.NET Core Server]  ←──SignalR──→  [Browser]
  Screen Capture                        JWT Auth + Relay                   Live View +
  Mouse/Keyboard                        WebSockets                         Control
```

---

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or higher
- Windows (for the client — requires Screen Capture & P/Invoke)
- A modern browser (Chrome, Firefox, Edge)

---

## Getting Started

### 1. Trust the HTTPS certificate (one-time)

```bash
dotnet dev-certs https --trust
```

### 2. Start the Server

```bash
cd Server
dotnet run
```

Server runs on:
- `https://localhost:7001`
- `http://localhost:5000`

### 3. Start the Client

```bash
cd Client
dotnet run
```

The Windows Forms window will open.

### 4. Connect

**In the Client window:**
1. Server URL: `https://localhost:7001`
2. PC Name: auto-filled with your machine name
3. Click **Connect** → status turns green

**In the Browser:**
1. Open `https://localhost:7001`
2. Login: `admin` / `admin123`
3. Click your PC in the sidebar → live stream starts
4. Use your mouse and keyboard directly in the browser

---

## Project Structure

```
RemoteDesk/
├── Client/                          # Windows Forms application
│   ├── Form1.cs                     # Core logic (capture, input, SignalR)
│   ├── Form1.Designer.cs            # UI layout
│   ├── Program.cs                   # Entry point
│   ├── Properties/
│   │   └── Settings.cs              # JSON-based persistent settings
│   └── RemoteDesk.Client.csproj
│
└── Server/                          # ASP.NET Core web server
    ├── Controllers/
    │   └── AuthController.cs        # POST /api/auth/login → JWT token
    ├── Hubs/
    │   └── RemoteHub.cs             # SignalR hub (core relay logic)
    ├── Models/
    │   └── Models.cs                # LoginRequest, PcInfo
    ├── Services/
    │   └── PcRegistryService.cs     # In-memory registry of connected PCs
    ├── wwwroot/
    │   ├── index.html               # Web UI (single page app)
    │   ├── css/style.css            # Dark theme design
    │   └── js/app.js                # SignalR client + rendering + input
    ├── Program.cs                   # Server configuration
    ├── appsettings.json             # JWT, ports, logging
    └── RemoteDesk.Server.csproj
```

---

## Features

| Feature | Description |
|---|---|
| 🖥️ Screen Streaming | JPEG frames transmitted via SignalR WebSocket |
| 🖱️ Mouse Control | Move, click, right-click, double-click, scroll |
| ⌨️ Keyboard Control | Full keyboard pass-through incl. F-keys and special keys |
| 🔐 JWT Authentication | Token-based login, valid for 8 hours |
| 🔄 Auto-Reconnect | Client and browser reconnect automatically on disconnect |
| 📊 FPS Counter | Live frames-per-second display in the toolbar |
| 🔔 System Tray | Client minimizes to the system tray and runs in the background |
| ⛶ Fullscreen | Browser fullscreen mode for immersive control |
| 💻 Multiple PCs | Multiple PCs can be registered and switched between |

---

## Configuration

### Managing Users

In `Server/Controllers/AuthController.cs`:

```csharp
private static readonly Dictionary<string, string> _users = new()
{
    ["admin"] = "admin123",
    ["alice"] = "mypassword"
};
```

### Changing the JWT Secret

In `Server/appsettings.json` — **must be changed before deploying!**

```json
"Jwt": {
  "Key": "REPLACE_WITH_A_LONG_RANDOM_SECRET_MIN_32_CHARS",
  "Issuer": "RemoteDesktopServer",
  "Audience": "RemoteDesktopClients"
}
```

### Performance Tuning

Adjust in the Client window:

| Setting | Recommended | Description |
|---|---|---|
| JPEG Quality | 50–70 | Lower = faster, less quality |
| Interval (ms) | 66 ms | ≈ 15 FPS (smooth) |
| Interval (ms) | 100 ms | ≈ 10 FPS (lighter load) |

---

## SignalR Hub Methods

### PC → Server
| Method | Description |
|---|---|
| `RegisterPc(pcId)` | PC agent registers itself on connect |
| `SendFrame(pcId, base64)` | Streams a JPEG screen frame |

### Browser → Server
| Method | Description |
|---|---|
| `WatchPc(pcId)` | Browser starts watching a PC |
| `StopWatching(pcId)` | Browser stops the session |
| `SendMouseEvent(pcId, x, y, type)` | Forward a mouse event |
| `SendKeyboardEvent(pcId, key, isDown)` | Forward a key press |
| `GetOnlinePcs()` | Get list of all connected PCs |

### Server → Browser
| Event | Description |
|---|---|
| `ReceiveFrame(base64)` | New screen frame arrived |
| `PcOnline(pcId)` | A PC came online |
| `PcOffline(pcId)` | A PC disconnected |

### Server → PC
| Event | Description |
|---|---|
| `StartStream` | Begin sending frames |
| `StopStream` | Pause sending frames |
| `MouseEvent(x, y, type)` | Execute mouse action |
| `KeyboardEvent(key, isDown)` | Execute key press |

---

## Security Notes

> ⚠️ This project is intended as a **development / learning project**.

For production use, consider:
- Replace the JWT secret with a strong, random value
- Store users in a database (e.g. ASP.NET Core Identity)
- Restrict CORS to known domains only
- Use HTTPS with a real certificate (e.g. Let's Encrypt)
- Add two-factor authentication
- Implement IP allowlisting

---

## Tech Stack

| Layer | Technology |
|---|---|
| Windows Client | C# .NET 8, Windows Forms, SignalR Client |
| Web Server | ASP.NET Core 8, SignalR, JWT Bearer Auth |
| Web Frontend | HTML, CSS, Vanilla JavaScript, SignalR JS |
| Communication | WebSockets (SignalR) |
| Screen Capture | `System.Drawing`, GDI+ |
| Input Simulation | `user32.dll` P/Invoke |