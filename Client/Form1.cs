using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.AspNetCore.SignalR.Client;
using RemoteDesktopClient.Properties;

namespace RemoteDesktopClient
{
    public partial class Form1 : Form
    {
        // ── P/Invoke for mouse & keyboard simulation ──────────────────────
        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, IntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        static extern short VkKeyScan(char ch);

        const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        const uint MOUSEEVENTF_LEFTUP = 0x0004;
        const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        const uint MOUSEEVENTF_WHEEL = 0x0800;
        const uint KEYEVENTF_KEYUP = 0x0002;

        // ── State ─────────────────────────────────────────────────────────
        private HubConnection? _hubConnection;
        private System.Windows.Forms.Timer _captureTimer = new();
        private CancellationTokenSource _cts = new();
        private bool _isConnected = false;
        private bool _isStreaming = false;

        // ── Settings ──────────────────────────────────────────────────────
        private string _serverUrl = "https://localhost:7001";
        private string _pcId = Environment.MachineName;
        private int _captureInterval = 100;   // ms between frames
        private int _jpegQuality = 60;

        public Form1()
        {
            InitializeComponent();
            LoadSettings();
            SetupUI();
        }

        // ─────────────────────────────────────────────────────────────────
        // UI Setup
        // ─────────────────────────────────────────────────────────────────
        private void SetupUI()
        {
            txtServerUrl.Text = _serverUrl;
            txtPcId.Text = _pcId;
            nudQuality.Value = _jpegQuality;
            nudInterval.Value = _captureInterval;
            UpdateStatusLabel("Disconnected", Color.OrangeRed);
        }

        private void UpdateStatusLabel(string text, Color color)
        {
            if (InvokeRequired) { Invoke(() => UpdateStatusLabel(text, color)); return; }
            lblStatus.Text = "● " + text;
            lblStatus.ForeColor = color;
        }

        private void Log(string msg)
        {
            if (InvokeRequired) { Invoke(() => Log(msg)); return; }
            lstLog.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
            if (lstLog.Items.Count > 200) lstLog.Items.RemoveAt(lstLog.Items.Count - 1);
        }

        // ─────────────────────────────────────────────────────────────────
        // Connect / Disconnect
        // ─────────────────────────────────────────────────────────────────
        private async void btnConnect_Click(object sender, EventArgs e)
        {
            if (_isConnected) { await DisconnectAsync(); return; }
            await ConnectAsync();
        }

        private async Task ConnectAsync()
        {
            _serverUrl = txtServerUrl.Text.TrimEnd('/');
            _pcId = txtPcId.Text.Trim();
            _jpegQuality = (int)nudQuality.Value;
            _captureInterval = (int)nudInterval.Value;
            SaveSettings();

            try
            {
                UpdateStatusLabel("Connecting…", Color.Gold);
                Log($"Connecting to {_serverUrl}…");

                _hubConnection = new HubConnectionBuilder()
                    .WithUrl($"{_serverUrl}/remotehub", options =>
                    {
                        // Accept self-signed certs in dev
                        options.HttpMessageHandlerFactory = _ =>
                            new System.Net.Http.HttpClientHandler
                            {
                                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                            };
                    })
                    .WithAutomaticReconnect()
                    .Build();

                // ── Incoming command handlers ──────────────────────────
                _hubConnection.On<int, int, string>("MouseEvent", HandleMouseEvent);
                _hubConnection.On<string, bool>("KeyboardEvent", HandleKeyboardEvent);
                _hubConnection.On<string>("ChatMessage", msg => Log($"[Chat] {msg}"));
                _hubConnection.On("StartStream", StartCapture);
                _hubConnection.On("StopStream", StopCapture);

                _hubConnection.Reconnecting += _ => { UpdateStatusLabel("Reconnecting…", Color.Gold); return Task.CompletedTask; };
                _hubConnection.Reconnected += _ => { UpdateStatusLabel("Connected", Color.LimeGreen); return Task.CompletedTask; };
                _hubConnection.Closed += _ => { UpdateStatusLabel("Disconnected", Color.OrangeRed); _isConnected = false; UpdateConnectButton(); return Task.CompletedTask; };

                await _hubConnection.StartAsync();
                await _hubConnection.InvokeAsync("RegisterPc", _pcId);

                _isConnected = true;
                UpdateStatusLabel("Connected", Color.LimeGreen);
                Log("✓ Connected and registered as: " + _pcId);
                UpdateConnectButton();
            }
            catch (Exception ex)
            {
                UpdateStatusLabel("Connection failed", Color.OrangeRed);
                Log("✗ " + ex.Message);
                MessageBox.Show("Connection failed:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task DisconnectAsync()
        {
            StopCapture();
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
            }
            _isConnected = false;
            UpdateStatusLabel("Disconnected", Color.OrangeRed);
            Log("Disconnected.");
            UpdateConnectButton();
        }

        private void UpdateConnectButton()
        {
            if (InvokeRequired) { Invoke(UpdateConnectButton); return; }
            btnConnect.Text = _isConnected ? "Disconnect" : "Connect";
            btnConnect.BackColor = _isConnected ? Color.FromArgb(220, 60, 60) : Color.FromArgb(34, 139, 34);
        }

        // ─────────────────────────────────────────────────────────────────
        // Screen Capture & Streaming
        // ─────────────────────────────────────────────────────────────────
        private void StartCapture()
        {
            if (_isStreaming) return;
            _isStreaming = true;
            _cts = new CancellationTokenSource();
            Log("Screen streaming started.");
            UpdateStatusLabel("Streaming", Color.DodgerBlue);

            Task.Run(async () =>
            {
                var token = _cts.Token;
                while (!token.IsCancellationRequested && _isConnected)
                {
                    try
                    {
                        var base64 = CaptureScreenBase64();
                        if (_hubConnection?.State == HubConnectionState.Connected)
                            await _hubConnection.InvokeAsync("SendFrame", _pcId, base64, token);
                        await Task.Delay(_captureInterval, token);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex) { Log("Capture error: " + ex.Message); await Task.Delay(500); }
                }
            }, _cts.Token);
        }

        private void StopCapture()
        {
            _isStreaming = false;
            _cts.Cancel();
            Log("Screen streaming stopped.");
            if (_isConnected) UpdateStatusLabel("Connected", Color.LimeGreen);
        }

        private string CaptureScreenBase64()
        {
            var bounds = Screen.PrimaryScreen!.Bounds;
            using var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

            // Draw cursor
            DrawCursor(g);

            using var ms = new MemoryStream();
            var encoder = GetJpegEncoder();
            var parms = new EncoderParameters(1);
            parms.Param[0] = new EncoderParameter(Encoder.Quality, (long)_jpegQuality);
            bmp.Save(ms, encoder, parms);
            return Convert.ToBase64String(ms.ToArray());
        }

        private void DrawCursor(Graphics g)
        {
            var cursorPos = Cursor.Position;
            Cursors.Default.Draw(g, new Rectangle(cursorPos.X - 8, cursorPos.Y - 8, 32, 32));
        }

        private static ImageCodecInfo GetJpegEncoder()
        {
            foreach (var codec in ImageCodecInfo.GetImageEncoders())
                if (codec.MimeType == "image/jpeg") return codec;
            throw new Exception("JPEG encoder not found");
        }

        // ─────────────────────────────────────────────────────────────────
        // Input Handlers (called by SignalR from browser)
        // ─────────────────────────────────────────────────────────────────
        private void HandleMouseEvent(int x, int y, string eventType)
        {
            SetCursorPos(x, y);
            switch (eventType)
            {
                case "mousemove": break;
                case "mousedown": mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero); break;
                case "mouseup": mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero); break;
                case "contextmenu":
                    mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, IntPtr.Zero);
                    mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, IntPtr.Zero); break;
                case "dblclick":
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero); break;
            }
        }

        private void HandleKeyboardEvent(string key, bool isKeyDown)
        {
            byte vk = MapKeyToVirtualKey(key);
            if (vk == 0) return;
            if (isKeyDown) keybd_event(vk, 0, 0, IntPtr.Zero);
            else keybd_event(vk, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
        }

        private static byte MapKeyToVirtualKey(string key)
        {
            return key switch
            {
                "Enter" => 0x0D,
                "Backspace" => 0x08,
                "Tab" => 0x09,
                "Escape" => 0x1B,
                "Delete" => 0x2E,
                "Insert" => 0x2D,
                "Home" => 0x24,
                "End" => 0x23,
                "PageUp" => 0x21,
                "PageDown" => 0x22,
                "ArrowLeft" => 0x25,
                "ArrowUp" => 0x26,
                "ArrowRight" => 0x27,
                "ArrowDown" => 0x28,
                "CapsLock" => 0x14,
                "Shift" => 0x10,
                "Control" => 0x11,
                "Alt" => 0x12,
                "F1" => 0x70,
                "F2" => 0x71,
                "F3" => 0x72,
                "F4" => 0x73,
                "F5" => 0x74,
                "F6" => 0x75,
                "F7" => 0x76,
                "F8" => 0x77,
                "F9" => 0x78,
                "F10" => 0x79,
                "F11" => 0x7A,
                "F12" => 0x7B,
                _ => key.Length == 1 ? (byte)(VkKeyScan(key[0]) & 0xFF) : (byte)0
            };
        }

        // ─────────────────────────────────────────────────────────────────
        // System Tray
        // ─────────────────────────────────────────────────────────────────
        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                notifyIcon.Visible = true;
                notifyIcon.ShowBalloonTip(2000, "Remote Desktop", "Running in background", ToolTipIcon.Info);
            }
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            notifyIcon.Visible = false;
        }

        private void miShow_Click(object sender, EventArgs e) => notifyIcon_DoubleClick(sender, e);
        private async void miExit_Click(object sender, EventArgs e)
        {
            await DisconnectAsync();
            Application.Exit();
        }

        // ─────────────────────────────────────────────────────────────────
        // Settings persistence
        // ─────────────────────────────────────────────────────────────────
        private void LoadSettings()
        {
            var s = Settings.Default;
            _serverUrl = s.ServerUrl.Length > 0 ? s.ServerUrl : _serverUrl;
            _pcId = s.PcId.Length > 0 ? s.PcId : _pcId;
            _jpegQuality = s.JpegQuality > 0 ? s.JpegQuality : _jpegQuality;
            _captureInterval = s.CaptureInterval > 0 ? s.CaptureInterval : _captureInterval;
        }

        private void SaveSettings()
        {
            var s = Settings.Default;
            s.ServerUrl = _serverUrl;
            s.PcId = _pcId;
            s.JpegQuality = _jpegQuality;
            s.CaptureInterval = _captureInterval;
            s.Save();
        }

        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            await DisconnectAsync();
            base.OnFormClosing(e);
        }
    }
}