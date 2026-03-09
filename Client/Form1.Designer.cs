using System.Windows.Forms;
using System.Drawing;

namespace RemoteDesktopClient
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            var mainPanel = new TableLayoutPanel();
            var headerPanel = new Panel();
            var lblTitle = new Label();
            lblStatus = new Label();
            var settingsGroup = new GroupBox();
            var settingsLayout = new TableLayoutPanel();
            var lblUrl = new Label();
            txtServerUrl = new TextBox();
            var lblPcId = new Label();
            txtPcId = new TextBox();
            var lblQuality = new Label();
            nudQuality = new NumericUpDown();
            var lblInterval = new Label();
            nudInterval = new NumericUpDown();
            btnConnect = new Button();
            var logGroup = new GroupBox();
            lstLog = new ListBox();
            notifyIcon = new NotifyIcon(components);
            var trayMenu = new ContextMenuStrip(components);
            miShow = new ToolStripMenuItem();
            miExit = new ToolStripMenuItem();

            SuspendLayout();

            Text = "RemoteDesk – Client";
            Size = new Size(480, 560);
            MinimumSize = new Size(480, 560);
            BackColor = Color.FromArgb(18, 18, 24);
            ForeColor = Color.FromArgb(220, 220, 230);
            Font = new Font("Segoe UI", 9.5f);
            Resize += Form1_Resize;

            headerPanel.Dock = DockStyle.Top;
            headerPanel.Height = 64;
            headerPanel.BackColor = Color.FromArgb(26, 26, 36);
            lblTitle.Text = "⬡  RemoteDesk Client"; lblTitle.Font = new Font("Segoe UI Semibold", 13f);
            lblTitle.ForeColor = Color.FromArgb(100, 180, 255); lblTitle.AutoSize = true; lblTitle.Location = new Point(16, 14);
            lblStatus.Text = "● Disconnected"; lblStatus.Font = new Font("Segoe UI", 9f);
            lblStatus.ForeColor = Color.OrangeRed; lblStatus.AutoSize = true; lblStatus.Location = new Point(16, 40);
            headerPanel.Controls.AddRange(new Control[] { lblTitle, lblStatus });

            settingsGroup.Text = "Connection Settings"; settingsGroup.ForeColor = Color.FromArgb(140, 160, 200);
            settingsGroup.Dock = DockStyle.Fill; settingsGroup.Padding = new Padding(12);

            settingsLayout.ColumnCount = 2; settingsLayout.Dock = DockStyle.Fill;
            settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            settingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            settingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            settingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            settingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            settingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            settingsLayout.Padding = new Padding(8);

            StyleLabel(lblUrl, "Server URL"); StyleTextBox(txtServerUrl);
            StyleLabel(lblPcId, "PC Name / ID"); StyleTextBox(txtPcId);
            StyleLabel(lblQuality, "JPEG Quality");
            StyleLabel(lblInterval, "Interval (ms)");

            ((System.ComponentModel.ISupportInitialize)nudQuality).BeginInit();
            nudQuality.Minimum = 10; nudQuality.Maximum = 100; nudQuality.Value = 60; nudQuality.Increment = 5;
            StyleNud(nudQuality);
            ((System.ComponentModel.ISupportInitialize)nudQuality).EndInit();

            ((System.ComponentModel.ISupportInitialize)nudInterval).BeginInit();
            nudInterval.Minimum = 0; nudInterval.Maximum = 2000; nudInterval.Value = 50; nudInterval.Increment = 10;
            StyleNud(nudInterval);
            ((System.ComponentModel.ISupportInitialize)nudInterval).EndInit();

            btnConnect.Text = "Connect"; btnConnect.Dock = DockStyle.Fill;
            btnConnect.FlatStyle = FlatStyle.Flat; btnConnect.BackColor = Color.FromArgb(34, 139, 34);
            btnConnect.ForeColor = Color.White; btnConnect.Font = new Font("Segoe UI Semibold", 10.5f);
            btnConnect.FlatAppearance.BorderSize = 0; btnConnect.Click += btnConnect_Click;

            settingsLayout.Controls.Add(lblUrl, 0, 0); settingsLayout.Controls.Add(txtServerUrl, 1, 0);
            settingsLayout.Controls.Add(lblPcId, 0, 1); settingsLayout.Controls.Add(txtPcId, 1, 1);
            settingsLayout.Controls.Add(lblQuality, 0, 2); settingsLayout.Controls.Add(nudQuality, 1, 2);
            settingsLayout.Controls.Add(lblInterval, 0, 3); settingsLayout.Controls.Add(nudInterval, 1, 3);
            settingsLayout.SetColumnSpan(btnConnect, 2); settingsLayout.Controls.Add(btnConnect, 0, 4);
            settingsGroup.Controls.Add(settingsLayout);

            logGroup.Text = "Activity Log"; logGroup.ForeColor = Color.FromArgb(140, 160, 200); logGroup.Dock = DockStyle.Fill;
            lstLog.Dock = DockStyle.Fill; lstLog.BackColor = Color.FromArgb(12, 12, 18);
            lstLog.ForeColor = Color.FromArgb(160, 200, 160); lstLog.Font = new Font("Consolas", 8.5f); lstLog.BorderStyle = BorderStyle.None;
            logGroup.Controls.Add(lstLog);

            mainPanel.Dock = DockStyle.Fill; mainPanel.RowCount = 2; mainPanel.ColumnCount = 1;
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 240));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainPanel.Padding = new Padding(12); mainPanel.BackColor = Color.FromArgb(18, 18, 24);
            mainPanel.Controls.Add(settingsGroup, 0, 0); mainPanel.Controls.Add(logGroup, 0, 1);

            miShow.Text = "Show"; miExit.Text = "Exit";
            miShow.Click += miShow_Click; miExit.Click += miExit_Click;
            trayMenu.Items.AddRange(new ToolStripItem[] { miShow, new ToolStripSeparator(), miExit });
            notifyIcon.Text = "RemoteDesk Client"; notifyIcon.ContextMenuStrip = trayMenu;
            notifyIcon.DoubleClick += notifyIcon_DoubleClick;

            Controls.AddRange(new Control[] { mainPanel, headerPanel });
            headerPanel.BringToFront();
            ResumeLayout(false);
        }

        static void StyleLabel(Label l, string t) { l.Text = t; l.Dock = DockStyle.Fill; l.TextAlign = ContentAlignment.MiddleLeft; l.ForeColor = Color.FromArgb(180, 190, 210); }
        static void StyleTextBox(TextBox t) { t.Dock = DockStyle.Fill; t.BackColor = Color.FromArgb(30, 30, 42); t.ForeColor = Color.FromArgb(210, 215, 230); t.BorderStyle = BorderStyle.FixedSingle; }
        static void StyleNud(NumericUpDown n) { n.Dock = DockStyle.Fill; n.BackColor = Color.FromArgb(30, 30, 42); n.ForeColor = Color.FromArgb(210, 215, 230); }

        private Label lblStatus;
        private TextBox txtServerUrl;
        private TextBox txtPcId;
        private NumericUpDown nudQuality;
        private NumericUpDown nudInterval;
        private Button btnConnect;
        private ListBox lstLog;
        private NotifyIcon notifyIcon;
        private ToolStripMenuItem miShow;
        private ToolStripMenuItem miExit;
    }
}