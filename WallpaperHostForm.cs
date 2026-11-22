using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

namespace SlaysiWallpaper;

public class WallpaperHostForm : Form
{
    private readonly WebView2 _webView;
    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _mouseTimer;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _startupItem;
    private bool _isPaused = false;
    private Point _lastCursor = Point.Empty;

    private const string AppName = "Slaysi3DWallpaper";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    public WallpaperHostForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        Bounds = bounds;
        BackColor = Color.FromArgb(3, 8, 18);

        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Color.FromArgb(3, 8, 18)
        };
        Controls.Add(_webView);

        // System Tray Menu
        var menu = new ContextMenuStrip();
        _pauseItem = new ToolStripMenuItem("Pause Wallpaper", null, TogglePause);
        var settingsItem = new ToolStripMenuItem("Open Wallpaper Settings (S)", null, OpenSettings);
        _startupItem = new ToolStripMenuItem("Run at Windows Startup", null, ToggleStartup)
        {
            Checked = IsStartupEnabled()
        };
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => Application.Exit());

        menu.Items.Add(_pauseItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Slaysi.si 3D Live Wallpaper",
            ContextMenuStrip = menu,
            Visible = true
        };

        // Forward global desktop cursor movement to WebView2 for live 3D parallax
        _mouseTimer = new System.Windows.Forms.Timer { Interval = 30 };
        _mouseTimer.Tick += MouseTimer_Tick;

        Load += WallpaperHostForm_Load;
        FormClosed += (_, _) =>
        {
            _mouseTimer.Stop();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        };
    }

    private async void WallpaperHostForm_Load(object? sender, EventArgs e)
    {
        AttachToDesktopWorkerW();

        string userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Slaysi3DWallpaper", "WebView2Cache");
        Directory.CreateDirectory(userDataFolder);

        var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await _webView.EnsureCoreWebView2Async(env);

        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

        string html = LoadEmbeddedWallpaperHtml();
        string tempHtmlPath = Path.Combine(userDataFolder, "wallpaper.html");
        File.WriteAllText(tempHtmlPath, html, Encoding.UTF8);

        _webView.CoreWebView2.Navigate(new Uri(tempHtmlPath).AbsoluteUri);
        _mouseTimer.Start();
    }

    private void AttachToDesktopWorkerW()
    {
        IntPtr progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero) return;

        SendMessageTimeout(progman, 0x052C, new IntPtr(0), IntPtr.Zero, 0x0000, 1000, out _);

        IntPtr workerW = IntPtr.Zero;
        EnumWindows((topHandle, _) =>
        {
            IntPtr shellView = FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView != IntPtr.Zero)
            {
                workerW = FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
                return false;
            }
            return true;
        }, IntPtr.Zero);

        if (workerW != IntPtr.Zero)
        {
            SetParent(Handle, workerW);
        }
        else
        {
            SetParent(Handle, progman);
        }

        var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        SetBounds(0, 0, bounds.Width, bounds.Height);
    }

    private static string LoadEmbeddedWallpaperHtml()
    {
        var asm = Assembly.GetExecutingAssembly();
        string? resName = Array.Find(asm.GetManifestResourceNames(), n => n.EndsWith("slaysi_3d_live_wallpaper_HD.html"));
        if (resName == null) return "<html><body style='background:#030812;color:#0ff'>Wallpaper resource missing.</body></html>";
        using var stream = asm.GetManifestResourceStream(resName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private void MouseTimer_Tick(object? sender, EventArgs e)
    {
        if (_isPaused || _webView.CoreWebView2 == null) return;
        if (!GetCursorPos(out Point pt)) return;
        if (pt == _lastCursor) return;
        _lastCursor = pt;

        var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        int relX = pt.X - bounds.Left;
        int relY = pt.Y - bounds.Top;
        string js = $"window.dispatchEvent(new PointerEvent('pointermove', {{ clientX: {relX}, clientY: {relY} }}));";
        _webView.CoreWebView2.ExecuteScriptAsync(js);
    }

    private void TogglePause(object? sender, EventArgs e)
    {
        _isPaused = !_isPaused;
        _pauseItem.Text = _isPaused ? "Resume Wallpaper" : "Pause Wallpaper";
        if (_webView.CoreWebView2 != null)
        {
            _webView.CoreWebView2.IsMuted = _isPaused;
            _webView.Visible = !_isPaused;
        }
    }

    private void OpenSettings(object? sender, EventArgs e)
    {
        if (_webView.CoreWebView2 == null) return;
        _webView.CoreWebView2.ExecuteScriptAsync(
            "document.body.classList.remove('clean-mode'); window.dispatchEvent(new KeyboardEvent('keydown', { key: 's' }));"
        );
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(AppName) != null;
    }

    private void ToggleStartup(object? sender, EventArgs e)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        if (key == null) return;

        if (_startupItem.Checked)
        {
            key.DeleteValue(AppName, false);
            _startupItem.Checked = false;
        }
        else
        {
            string exePath = Environment.ProcessPath ?? Application.ExecutablePath;
            key.SetValue(AppName, $""{exePath}"");
            _startupItem.Checked = true;
        }
    }
}
