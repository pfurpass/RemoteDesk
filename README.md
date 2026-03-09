# RemoteDesk 🖥️

Ein Remote-Desktop-System, das es ermöglicht, einen Windows-PC über einen Browser fernzusteuern — ähnlich wie TeamViewer.

---

## Überblick

Das System besteht aus **3 Komponenten**:

```
[Windows Client App]  ←──SignalR──→  [ASP.NET Core Server]  ←──SignalR──→  [Browser]
  Screen Capture                        JWT Auth + Relay                   Live View +
  Maus/Tastatur                         WebSockets                         Steuerung
```

---

## Voraussetzungen

- [.NET 8 SDK](https://dotnet.microsoft.com/download) oder höher
- Windows (für den Client — wegen Screen Capture & P/Invoke)
- Ein moderner Browser (Chrome, Firefox, Edge)

---

## Projekt starten

### 1. HTTPS-Zertifikat vertrauen (einmalig)

```bash
dotnet dev-certs https --trust
```

### 2. Server starten

```bash
cd Server
dotnet run
```

Server läuft auf:
- `https://localhost:7001`
- `http://localhost:5000`

### 3. Client starten

```bash
cd Client
dotnet run
```

Das Windows Forms Fenster öffnet sich.

### 4. Verbinden

**Im Client-Fenster:**
1. Server URL: `https://localhost:7001`
2. PC Name: wird automatisch ausgefüllt
3. **Connect** klicken → Status wird grün

**Im Browser:**
1. `https://localhost:7001` öffnen
2. Login: `admin` / `admin123`
3. PC in der Sidebar anklicken → Livestream startet
4. Maus und Tastatur können direkt im Browser verwendet werden

---

## Projektstruktur

```
RemoteDesk/
├── Client/                          # Windows Forms Anwendung
│   ├── Form1.cs                     # Hauptlogik (Capture, Input, SignalR)
│   ├── Form1.Designer.cs            # UI Layout
│   ├── Program.cs                   # Einstiegspunkt
│   ├── Properties/
│   │   └── Settings.cs              # JSON-basierte Einstellungen
│   └── RemoteDesk.Client.csproj
│
└── Server/                          # ASP.NET Core Web Server
    ├── Controllers/
    │   └── AuthController.cs        # POST /api/auth/login → JWT Token
    ├── Hubs/
    │   └── RemoteHub.cs             # SignalR Hub (Herzstück der Kommunikation)
    ├── Models/
    │   └── Models.cs                # LoginRequest, PcInfo
    ├── Services/
    │   └── PcRegistryService.cs     # Verwaltet verbundene PCs im Speicher
    ├── wwwroot/
    │   ├── index.html               # Web-UI (Single Page App)
    │   ├── css/style.css            # Dark-Theme Design
    │   └── js/app.js                # SignalR Client + Rendering + Input
    ├── Program.cs                   # Server Konfiguration
    ├── appsettings.json             # JWT, Ports, Logging
    └── RemoteDesk.Server.csproj
```

---

## Funktionen

| Funktion | Beschreibung |
|---|---|
| 🖥️ Screen Streaming | JPEG-Frames werden per SignalR WebSocket übertragen |
| 🖱️ Maussteuerung | Bewegen, Klicken, Rechtsklick, Doppelklick, Scrollen |
| ⌨️ Tastatursteuerung | Vollständige Tastatureingabe inkl. F-Tasten, Sondertasten |
| 🔐 JWT Authentifizierung | Login mit Token, 8 Stunden gültig |
| 🔄 Auto-Reconnect | Client und Browser verbinden sich automatisch wieder |
| 📊 FPS Anzeige | Live Frames-per-Second Counter im Browser |
| 🔔 System Tray | Client läuft im Hintergrund, minimiert in die Taskleiste |
| ⛶ Vollbild | Browser Vollbildmodus für komfortablere Steuerung |
| 💻 Mehrere PCs | Mehrere PCs können gleichzeitig verbunden sein |

---

## Konfiguration

### Benutzer verwalten

In `Server/Controllers/AuthController.cs`:

```csharp
private static readonly Dictionary<string, string> _users = new()
{
    ["admin"]  = "admin123",
    ["benutzer1"] = "meinPasswort"
};
```

### JWT Secret ändern

In `Server/appsettings.json` — **unbedingt ändern vor Produktiveinsatz!**

```json
"Jwt": {
  "Key": "HIER_EIN_LANGES_ZUFAELLIGES_SECRET_MIN_32_ZEICHEN",
  "Issuer": "RemoteDesktopServer",
  "Audience": "RemoteDesktopClients"
}
```

### Performance einstellen

Im Client-Fenster:

| Einstellung | Empfehlung | Beschreibung |
|---|---|---|
| JPEG Quality | 50–70 | Niedriger = schneller, schlechtere Qualität |
| Interval (ms) | 66 ms | ≈ 15 FPS (gute Balance) |
| Interval (ms) | 100 ms | ≈ 10 FPS (weniger Last) |

---

## SignalR Methoden

### PC → Server
| Methode | Beschreibung |
|---|---|
| `RegisterPc(pcId)` | PC meldet sich beim Server an |
| `SendFrame(pcId, base64)` | Überträgt einen JPEG-Frame |

### Browser → Server
| Methode | Beschreibung |
|---|---|
| `WatchPc(pcId)` | Browser beginnt einen PC zu beobachten |
| `StopWatching(pcId)` | Browser beendet die Verbindung zum PC |
| `SendMouseEvent(pcId, x, y, type)` | Mausevent weiterleiten |
| `SendKeyboardEvent(pcId, key, isDown)` | Tastaturevent weiterleiten |
| `GetOnlinePcs()` | Liste aller verbundenen PCs abrufen |

---

## Sicherheitshinweise

> ⚠️ Dieses System ist als **Entwicklungsprojekt** gedacht.

Für den Produktiveinsatz empfohlen:
- JWT Secret durch ein starkes, zufälliges Secret ersetzen
- Benutzer in einer Datenbank verwalten (z.B. ASP.NET Core Identity)
- CORS auf bekannte Domains einschränken
- HTTPS mit einem echten Zertifikat (z.B. Let's Encrypt)
- Zwei-Faktor-Authentifizierung hinzufügen

---

## Technologien

| Bereich | Technologie |
|---|---|
| Windows Client | C# .NET 8, Windows Forms, SignalR Client |
| Web Server | ASP.NET Core 8, SignalR, JWT Bearer Auth |
| Web Frontend | HTML, CSS, Vanilla JavaScript, SignalR JS |
| Kommunikation | WebSockets (SignalR) |
| Screen Capture | `System.Drawing`, GDI+ |
| Input Simulation | `user32.dll` P/Invoke |