using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

internal class VideoSource
{
    public string Name;
    public string Input;
    public int X, Y, Width, Height;
    public VideoSource(string name, string input) { Name = name; Input = input; }
    public override string ToString() { return Name; }
}

internal static class Theme
{
    public static readonly Color Canvas = Color.FromArgb(14, 19, 26);
    public static readonly Color Header = Color.FromArgb(18, 25, 34);
    public static readonly Color Surface = Color.FromArgb(24, 32, 42);
    public static readonly Color SurfaceBorder = Color.FromArgb(43, 55, 68);
    public static readonly Color Input = Color.FromArgb(17, 24, 32);
    public static readonly Color Text = Color.FromArgb(235, 241, 247);
    public static readonly Color Muted = Color.FromArgb(151, 166, 181);
    public static readonly Color Accent = Color.FromArgb(32, 199, 168);
    public static readonly Color AccentHover = Color.FromArgb(42, 218, 185);
    public static readonly Color Blue = Color.FromArgb(90, 167, 255);
    public static readonly Color Warning = Color.FromArgb(245, 185, 76);
    public static readonly Color Danger = Color.FromArgb(255, 107, 107);
    public static readonly Color Disabled = Color.FromArgb(85, 98, 112);

    public static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class SurfacePanel : Panel
{
    public SurfacePanel()
    {
        DoubleBuffered = true;
        // Keep the card opaque. Transparent child labels can otherwise ask
        // WinForms to repaint sibling controls into their own bounds.
        BackColor = Theme.Surface;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        // Fill the whole client area first. Partial rounded fills leave
        // stale pixels in child HWND buffers during a composited repaint.
        e.Graphics.Clear(Theme.Surface);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = Theme.RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 8))
        using (var pen = new Pen(Theme.SurfaceBorder))
            e.Graphics.DrawPath(pen, path);
    }
}

internal sealed class ModernButton : Control
{
    bool hovered;
    bool pressed;
    public Color ButtonColor = Theme.SurfaceBorder;
    public Color HoverColor = Color.FromArgb(56, 72, 88);
    public Color ButtonTextColor = Theme.Text;

    public ModernButton()
    {
        AutoSize = false;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI Semibold", 9F);
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
    }

    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Capture = true; Focus(); Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            pressed = false; Capture = false; Invalidate();
        }
        // Control raises the Click event from its standard mouse-up path. Do
        // not invoke OnClick manually, otherwise one press becomes start/stop.
        base.OnMouseUp(e);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
        base.OnKeyDown(e);
    }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Color back = Parent == null || Parent.BackColor == Color.Transparent ? Theme.Canvas : Parent.BackColor;
        using (var brush = new SolidBrush(back)) e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color fill = Enabled ? (pressed ? ButtonColor : (hovered ? HoverColor : ButtonColor)) : Color.FromArgb(43, 51, 60);
        Color text = Enabled ? ButtonTextColor : Theme.Disabled;
        using (var path = Theme.RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 6))
        using (var brush = new SolidBrush(fill))
            e.Graphics.FillPath(brush, path);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (Focused)
        {
            var focus = ClientRectangle; focus.Inflate(-4, -4);
            ControlPaint.DrawFocusRectangle(e.Graphics, focus, text, fill);
        }
    }
}

internal sealed class ModernToggle : Control
{
    bool checkedValue;
    public event EventHandler CheckedChanged;
    public bool Checked
    {
        get { return checkedValue; }
        set
        {
            if (checkedValue == value) return;
            checkedValue = value; Invalidate();
            var handler = CheckedChanged; if (handler != null) handler(this, EventArgs.Empty);
        }
    }

    public ModernToggle()
    {
        AutoSize = false;
        Height = 28;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9F);
        TabStop = true;
        AccessibleRole = AccessibleRole.CheckButton;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        // Transparent custom controls can retain pixels from a previously
        // painted sibling (notably the red start button). Paint an opaque
        // surface first so every redraw starts from a clean background.
        BackColor = Theme.Surface;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location) && Enabled) { Checked = !Checked; Focus(); }
        base.OnMouseUp(e);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { Checked = !Checked; e.Handled = true; }
        base.OnKeyDown(e);
    }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using (var brush = new SolidBrush(Theme.Surface))
            e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color track = !Enabled ? Color.FromArgb(54, 63, 73) : (Checked ? Theme.Accent : Color.FromArgb(65, 78, 91));
        using (var path = Theme.RoundedRectangle(new Rectangle(0, 4, 38, 20), 10))
        using (var brush = new SolidBrush(track))
            e.Graphics.FillPath(brush, path);
        int knobX = Checked ? 20 : 2;
        using (var brush = new SolidBrush(Enabled ? Color.White : Theme.Disabled))
            e.Graphics.FillEllipse(brush, knobX, 6, 16, 16);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(48, 0, Math.Max(0, Width - 48), Height), Enabled ? ForeColor : Theme.Disabled, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class ModernRadioButton : Control
{
    bool hovered;
    bool checkedValue;
    public event EventHandler CheckedChanged;
    public bool Checked
    {
        get { return checkedValue; }
        set
        {
            if (checkedValue == value) return;
            checkedValue = value; Invalidate();
            var handler = CheckedChanged; if (handler != null) handler(this, EventArgs.Empty);
        }
    }

    public ModernRadioButton()
    {
        AutoSize = false;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI Semibold", 9F);
        TabStop = true;
        AccessibleRole = AccessibleRole.RadioButton;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        BackColor = Theme.Surface;
    }

    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location) && Enabled)
        {
            if (Parent != null)
                foreach (Control sibling in Parent.Controls)
                    if (sibling is ModernRadioButton && sibling != this) ((ModernRadioButton)sibling).Checked = false;
            Checked = true; Focus();
        }
        base.OnMouseUp(e);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { Checked = true; e.Handled = true; }
        base.OnKeyDown(e);
    }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using (var brush = new SolidBrush(Theme.Surface))
            e.Graphics.FillRectangle(brush, ClientRectangle);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color fill = Checked ? Theme.Accent : (hovered ? Color.FromArgb(48, 61, 74) : Theme.Input);
        Color text = Checked ? Color.FromArgb(9, 30, 27) : Theme.Text;
        using (var path = Theme.RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 6))
        using (var brush = new SolidBrush(fill))
            e.Graphics.FillPath(brush, path);
        if (!Checked)
            using (var path = Theme.RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 6))
            using (var pen = new Pen(Theme.SurfaceBorder)) e.Graphics.DrawPath(pen, path);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

internal sealed class StatusDot : Control
{
    public Color IndicatorColor = Theme.Disabled;
    public StatusDot() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent; }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var glow = new SolidBrush(Color.FromArgb(45, IndicatorColor))) e.Graphics.FillEllipse(glow, 0, 0, Width - 1, Height - 1);
        using (var dot = new SolidBrush(IndicatorColor)) e.Graphics.FillEllipse(dot, 4, 4, Width - 9, Height - 9);
    }
}

internal sealed class AppLogo : Control
{
    public AppLogo()
    {
        DoubleBuffered = true;
        // Control does not support a transparent BackColor unless this style
        // is enabled before assigning the color. Without it WinForms throws
        // an ArgumentException during MainForm construction and the app exits.
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }
    protected override void OnPaint(PaintEventArgs e) { AppIcon.Draw(e.Graphics, ClientRectangle); }
}

internal static class AppIcon
{
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);

    public static void Draw(Graphics g, Rectangle bounds)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float sx = bounds.Width / 64f, sy = bounds.Height / 64f;
        GraphicsState state = g.Save();
        g.TranslateTransform(bounds.Left, bounds.Top); g.ScaleTransform(sx, sy);
        using (var background = Theme.RoundedRectangle(new Rectangle(1, 1, 62, 62), 14))
        using (var brush = new SolidBrush(Theme.Header)) g.FillPath(brush, background);
        using (var pen = new Pen(Theme.Accent, 5f)) { pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; g.DrawRoundedRectangle(pen, new Rectangle(10, 11, 44, 31), 7); }
        using (var pen = new Pen(Theme.Text, 4f)) { pen.StartCap = LineCap.Round; g.DrawLine(pen, 32, 43, 32, 50); g.DrawLine(pen, 22, 51, 42, 51); }
        using (var brush = new SolidBrush(Theme.Blue)) g.FillEllipse(brush, 27, 22, 10, 10);
        g.Restore(state);
    }

    public static Icon Create()
    {
        using (var bitmap = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent); Draw(graphics, new Rectangle(0, 0, 64, 64));
            IntPtr handle = bitmap.GetHicon();
            try { return (Icon)Icon.FromHandle(handle).Clone(); }
            finally { DestroyIcon(handle); }
        }
    }
}

internal static class GraphicsExtensions
{
    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius)
    {
        using (var path = Theme.RoundedRectangle(bounds, radius)) graphics.DrawPath(pen, path);
    }
}

internal class MainForm : Form
{
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // Paint child HWNDs bottom-to-top in one composited surface.
            cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
            return cp;
        }
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);

    readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
    readonly string Ffmpeg;
    readonly string Mediamtx;
    readonly string AudioLoopback;
    ComboBox sourceBox = new ComboBox();
    ComboBox resolutionBox = new ComboBox();
    ComboBox subResolutionBox = new ComboBox();
    ComboBox audioBox = new ComboBox();
    ComboBox audioModeBox = new ComboBox();
    TextBox portBox = new TextBox();
    TextBox pathBox = new TextBox();
    TextBox subPathBox = new TextBox();
    TextBox urlBox = new TextBox();
    TextBox subUrlBox = new TextBox();
    ModernButton startButton = new ModernButton();
    ModernButton helpButton = new ModernButton();
    // Use the native push-button for the language switch so Windows UI
    // Automation and mouse input both invoke its Click event reliably.
    Button languageButton = new Button();
    ModernButton copyButton = new ModernButton();
    ModernButton subCopyButton = new ModernButton();
    Label statusLabel = new Label();
    StatusDot statusDot = new StatusDot();
    ModernToggle subEnabledBox = new ModernToggle();
    ModernToggle audioEnabledBox = new ModernToggle();
    ToolTip toolTip = new ToolTip();
    NotifyIcon trayIcon;
    ContextMenuStrip trayMenu;
    ToolStripMenuItem trayOpenItem;
    ToolStripMenuItem trayExitItem;
    Icon appIcon;
    List<VideoSource> videoSources = new List<VideoSource>();
    Process ffmpegProcess;
    Process mediamtxProcess;
    Process loopbackProcess;
    NamedPipeServerStream loopbackPipe;
    System.Windows.Forms.Timer restartTimer = new System.Windows.Forms.Timer();
    readonly object logLock = new object();
    bool desiredRunning;
    bool applyingSettings;
    int streamGeneration;
    bool exiting;
    bool english;
    readonly List<Control> localizedControls = new List<Control>();
    readonly Dictionary<Control, string> localizedTooltips = new Dictionary<Control, string>();

    static readonly Dictionary<string, string> English = new Dictionary<string, string>
    {
        { "Desktop Streamer", "Desktop Streamer" },
        { "RTSP-трансляция", "RTSP streaming" },
        { "Справка", "Help" }, { "Язык", "Language" },
        { "Состояние трансляции", "Stream status" },
        { "Видео", "Video" }, { "Источник", "Source" }, { "Монитор", "Monitor" },
        { "Основной поток", "Main stream" }, { "Субпоток", "Sub-stream" },
        { "Исходное", "Original" }, { "720p (1280x720)", "720p (1280x720)" },
        { "CIF (352x288)", "CIF (352x288)" }, { "VGA (640x480)", "VGA (640x480)" },
        { "Аудио · G.711 μ-law", "Audio · G.711 μ-law" }, { "Включить звук", "Enable audio" },
        { "Режим", "Mode" }, { "Только микрофон", "Microphone only" },
        { "Только звук ПК (по умолчанию)", "PC audio only (default)" },
        { "Микрофон + звук ПК", "Microphone + PC audio" }, { "Микрофон", "Microphone" },
        { "Сеть", "Network" }, { "RTSP-порт", "RTSP port" },
        { "Основной путь", "Main path" }, { "Путь субпотока", "Sub-stream path" },
        { "Адреса потоков", "Stream addresses" }, { "Основной", "Main" },
        { "Копировать", "Copy" }, { "Остановить поток", "Stop stream" },
        { "Запустить поток", "Start stream" }, { "Остановлено", "Stopped" },
        { "Поток запущен", "Stream started" }, { "Основной и субпоток запущены", "Main and sub-streams started" },
        { "Поток завершён — проверьте журнал", "Stream stopped — check the log" },
        { "Применение настроек...", "Applying settings..." },
        { "Адрес скопирован", "Address copied" }, { "Обновить список", "Refresh list" },
        { "Микрофоны не найдены", "No microphones found" },
        { "Ошибка запуска", "Startup error" },
        { "Рядом с программой должны находиться ffmpeg.exe и mediamtx.exe.", "ffmpeg.exe and mediamtx.exe must be next to the application." },
        { "Укажите корректный RTSP-порт.", "Enter a valid RTSP port." },
        { "Проверьте путь основного потока.", "Check the main stream path." },
        { "Проверьте путь субпотока.", "Check the sub-stream path." },
        { "Выберите источник видео.", "Select a video source." },
        { "MediaMTX не запустился. Проверьте журнал.", "MediaMTX did not start. Check the log." },
        { "Выбран режим с микрофоном, но микрофон не найден.", "A microphone mode is selected, but no microphone was found." },
        { "FFmpeg не запустил поток. Проверьте журнал.", "FFmpeg did not start the stream. Check the log." },
        { "Выходное аудиоустройство Windows недоступно.", "The Windows output audio device is unavailable." },
        { "Рядом с программой отсутствует AudioLoopback.exe.", "AudioLoopback.exe is missing next to the application." },
        { " уже используется.", " is already in use." },
        { "Не удалось открыть звук ПК:", "Could not open PC audio:" },
        { "Не удалось применить настройки:", "Could not apply settings:" },
        { "Настройки не применены — проверьте параметры", "Settings were not applied — check the parameters" },
        { "Справка Desktop Streamer", "Desktop Streamer Help" },
        { "Открыть", "Open" }, { "Выход", "Exit" },
        { "Desktop Streamer захватывает изображение выбранного монитора и публикует его как RTSP-поток.", "Desktop Streamer captures the selected monitor and publishes it as an RTSP stream." },
        { "Можно включить основной поток и дополнительный субпоток с меньшим разрешением.", "You can enable a main stream and an additional lower-resolution sub-stream." },
        { "Аудио поддерживает микрофон, системный звук ПК или их сочетание в G.711 μ-law.", "Audio supports the microphone, PC system audio, or both mixed as G.711 μ-law." },
        { "Адрес RTSP отображается после запуска. Используйте VLC, ffplay или камеру видеонаблюдения для подключения.", "The RTSP address appears after start. Use VLC, ffplay, or a surveillance client to connect." },
        { "Связь: prostyle1992@gmail.com", "Contact: prostyle1992@gmail.com" },
        { "Журнал работы сохраняется в desktopstreamer.log рядом с программой; журналы FFmpeg и MediaMTX — в streamer_ffmpeg.log и streamer_mediamtx.log.", "The application log is saved as desktopstreamer.log next to the program; FFmpeg and MediaMTX logs are streamer_ffmpeg.log and streamer_mediamtx.log." },
        { "ОК", "OK" }
    };

    string T(string russian) { string value; return english && English.TryGetValue(russian, out value) ? value : russian; }
    string LocalizeRuntime(string text)
    {
        if (!english || text == null) return text;
        string value;
        if (English.TryGetValue(text, out value)) return value;
        if (text.StartsWith("RTSP-порт ", StringComparison.Ordinal) && text.EndsWith(" уже используется.", StringComparison.Ordinal))
            return T("RTSP-порт") + text.Substring("RTSP-порт ".Length, text.Length - "RTSP-порт ".Length - " уже используется.".Length) + T(" уже используется.");
        if (text.StartsWith("Не удалось открыть звук ПК: ", StringComparison.Ordinal)) return T("Не удалось открыть звук ПК:") + text.Substring("Не удалось открыть звук ПК: ".Length);
        if (text.StartsWith("Не удалось применить настройки: ", StringComparison.Ordinal)) return T("Не удалось применить настройки:") + text.Substring("Не удалось применить настройки: ".Length);
        if (text.StartsWith("Не удалось открыть выходное аудиоустройство Windows. ", StringComparison.Ordinal)) return T("Выходное аудиоустройство Windows недоступно.") + text.Substring("Не удалось открыть выходное аудиоустройство Windows. ".Length);
        return text;
    }
    string NoMicrophonesText() { return T("Микрофоны не найдены"); }
    void RegisterLocalized(Control control, string russian) { control.Tag = russian; if (!localizedControls.Contains(control)) localizedControls.Add(control); }
    void SetLocalizedTooltip(Control control, string russian) { localizedTooltips[control] = russian; toolTip.SetToolTip(control, T(russian)); }

    void Log(string message)
    {
        string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " | " + message + Environment.NewLine;
        try
        {
            lock (logLock) File.AppendAllText(Path.Combine(Root, "desktopstreamer.log"), line, Encoding.UTF8);
        }
        catch
        {
            try { lock (logLock) File.AppendAllText(Path.Combine(Path.GetTempPath(), "DesktopStreamer.log"), line, Encoding.UTF8); }
            catch { }
        }
    }

    void InitializeTray()
    {
        trayOpenItem = new ToolStripMenuItem(T("Открыть"));
        trayExitItem = new ToolStripMenuItem(T("Выход"));
        trayOpenItem.Click += delegate { RestoreFromTray(); };
        trayExitItem.Click += delegate { Log("Tray exit requested"); exiting = true; Close(); };
        trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add(trayOpenItem);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(trayExitItem);
        trayIcon = new NotifyIcon { Icon = appIcon, Text = "Desktop Streamer", ContextMenuStrip = trayMenu, Visible = true };
        trayIcon.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) RestoreFromTray(); };
        trayIcon.DoubleClick += delegate { RestoreFromTray(); };
        Resize += delegate
        {
            if (WindowState == FormWindowState.Minimized && !exiting)
            {
                Log("Window minimized to system tray");
                ShowInTaskbar = false;
                Hide();
            }
        };
    }

    void RestoreFromTray()
    {
        if (exiting) return;
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
        Log("Window restored from system tray");
    }

    public MainForm()
    {
        Ffmpeg = Path.Combine(Root, "ffmpeg.exe");
        Mediamtx = Path.Combine(Root, "mediamtx.exe");
        AudioLoopback = Path.Combine(Root, "AudioLoopback.exe");
        Text = "Desktop Streamer";
        ClientSize = new Size(920, 842);
        BackColor = Theme.Canvas;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9F);
        // Compose the complete hierarchy in one off-screen buffer. This
        // prevents transparent/owner-drawn children from leaving stale text
        // behind when neighboring controls are repainted.
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        appIcon = AppIcon.Create();
        Icon = appIcon;
        BuildUi();
        InitializeTray();
        RefreshVideoSources();
        RefreshAudio();
        Log("Application started");
        restartTimer.Interval = 700;
        restartTimer.Tick += delegate { restartTimer.Stop(); if (desiredRunning) RestartPipeline(); };
        FormClosing += delegate
        {
            exiting = true;
            Log("Application closing");
            desiredRunning = false; restartTimer.Stop(); StopProcesses();
            if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); trayIcon = null; }
        };
        FormClosed += delegate { if (appIcon != null) appIcon.Dispose(); };
        HandleCreated += delegate
        {
            try { int dark = 1; if (DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)) != 0) DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int)); }
            catch { }
        };
    }

    Label AddLabel(Control parent, string text, int x, int y, int width, int height, Color color, Font font)
    {
        var label = new Label { Text = T(text), Left = x, Top = y, Width = width, Height = height, ForeColor = color, BackColor = parent.BackColor == Color.Transparent ? Theme.Canvas : parent.BackColor, Font = font, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        RegisterLocalized(label, text);
        parent.Controls.Add(label); return label;
    }

    void AddSectionTitle(Control parent, string text)
    {
        AddLabel(parent, text, 20, 12, 260, 28, Theme.Text, new Font("Segoe UI Semibold", 12F));
    }

    void StyleComboBox(ComboBox box)
    {
        box.DropDownStyle = ComboBoxStyle.DropDownList;
        box.FlatStyle = FlatStyle.Flat;
        box.BackColor = Theme.Input;
        box.ForeColor = Theme.Text;
        box.Font = new Font("Segoe UI", 9.5F);
        box.DrawMode = DrawMode.OwnerDrawFixed;
        box.ItemHeight = 25;
        box.DrawItem += delegate(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (var brush = new SolidBrush(selected ? Color.FromArgb(41, 67, 70) : Theme.Input)) e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, box.Items[e.Index].ToString(), box.Font, e.Bounds, box.Enabled ? Theme.Text : Theme.Disabled, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if ((e.State & DrawItemState.Focus) == DrawItemState.Focus) e.DrawFocusRectangle();
        };
    }

    void StyleTextBox(TextBox box, bool address)
    {
        box.BorderStyle = BorderStyle.FixedSingle;
        box.BackColor = Theme.Input;
        box.ForeColor = address ? Theme.Blue : Theme.Text;
        box.Font = new Font(address ? "Consolas" : "Segoe UI", address ? 9F : 9.5F);
    }

    ModernButton CreateRefreshButton(Control parent, int x, int y, EventHandler click)
    {
        var button = new ModernButton { Text = "↻", Left = x, Top = y, Width = 36, Height = 34, Font = new Font("Segoe UI Symbol", 14F), ButtonColor = Theme.SurfaceBorder, HoverColor = Color.FromArgb(58, 75, 91) };
        button.Click += click; parent.Controls.Add(button); SetLocalizedTooltip(button, "Обновить список"); return button;
    }

    void ApplyLanguage()
    {
        foreach (Control c in localizedControls)
        {
            string key = c.Tag as string;
            if (key != null) c.Text = T(key);
        }
        foreach (var pair in localizedTooltips) toolTip.SetToolTip(pair.Key, T(pair.Value));
        languageButton.Text = english ? "RU" : "EN";
        languageButton.AccessibleName = english ? "Русский" : "English";
        if (trayOpenItem != null) trayOpenItem.Text = T("Открыть");
        if (trayExitItem != null) trayExitItem.Text = T("Выход");
        int resolution = resolutionBox.SelectedIndex; resolutionBox.Items.Clear(); resolutionBox.Items.Add(T("Исходное")); resolutionBox.Items.Add(T("720p (1280x720)")); resolutionBox.SelectedIndex = resolution < 0 ? 0 : Math.Min(resolution, resolutionBox.Items.Count - 1);
        int audioMode = audioModeBox.SelectedIndex; audioModeBox.Items.Clear(); audioModeBox.Items.Add(T("Только микрофон")); audioModeBox.Items.Add(T("Только звук ПК (по умолчанию)")); audioModeBox.Items.Add(T("Микрофон + звук ПК")); audioModeBox.SelectedIndex = audioMode < 0 ? 0 : Math.Min(audioMode, audioModeBox.Items.Count - 1);
        int subMode = subResolutionBox.SelectedIndex; subResolutionBox.Items.Clear(); subResolutionBox.Items.Add(T("CIF (352x288)")); subResolutionBox.Items.Add(T("VGA (640x480)")); subResolutionBox.SelectedIndex = subMode < 0 ? 0 : Math.Min(subMode, subResolutionBox.Items.Count - 1);
        RefreshVideoSources();
        if (audioBox.Items.Count == 1 && (audioBox.Items[0].ToString() == "Микрофоны не найдены" || audioBox.Items[0].ToString() == "No microphones found")) { audioBox.Items.Clear(); audioBox.Items.Add(NoMicrophonesText()); audioBox.SelectedIndex = 0; }
        if (!desiredRunning) SetStatus("Остановлено", Theme.Disabled);
        else { statusLabel.Text = LocalizeRuntime(statusLabel.Text); toolTip.SetToolTip(statusLabel, statusLabel.Text); SetStartButton(true); }
    }

    void ToggleLanguage()
    {
        english = !english;
        Log("Language changed to " + (english ? "English" : "Russian"));
        applyingSettings = true;
        try { ApplyLanguage(); }
        finally { applyingSettings = false; }
    }

    void ShowHelp()
    {
        Log("Help opened");
        string message = T("Desktop Streamer захватывает изображение выбранного монитора и публикует его как RTSP-поток.") + "\n\n" +
            T("Можно включить основной поток и дополнительный субпоток с меньшим разрешением.") + "\n\n" +
            T("Аудио поддерживает микрофон, системный звук ПК или их сочетание в G.711 μ-law.") + "\n\n" +
            T("Адрес RTSP отображается после запуска. Используйте VLC, ffplay или камеру видеонаблюдения для подключения.") + "\n\n" +
            T("Журнал работы сохраняется в desktopstreamer.log рядом с программой; журналы FFmpeg и MediaMTX — в streamer_ffmpeg.log и streamer_mediamtx.log.") + "\n\n" +
            T("Связь: prostyle1992@gmail.com");
        MessageBox.Show(this, message, T("Справка Desktop Streamer"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    void BuildUi()
    {
        var header = new Panel { Left = 0, Top = 0, Width = 920, Height = 88, BackColor = Theme.Header };
        Controls.Add(header);
        header.Controls.Add(new AppLogo { Left = 24, Top = 16, Width = 56, Height = 56 });
        AddLabel(header, "Desktop Streamer", 94, 14, 390, 34, Theme.Text, new Font("Segoe UI Semibold", 18F));
        AddLabel(header, "RTSP-трансляция", 96, 48, 250, 22, Theme.Muted, new Font("Segoe UI", 9F));
        helpButton.Text = T("Справка"); helpButton.SetBounds(652, 16, 96, 30); helpButton.ButtonColor = Theme.SurfaceBorder; helpButton.HoverColor = Color.FromArgb(58, 75, 91); helpButton.Click += delegate { ShowHelp(); }; header.Controls.Add(helpButton); RegisterLocalized(helpButton, "Справка");
        languageButton.Text = "EN"; languageButton.SetBounds(756, 16, 72, 30); languageButton.FlatStyle = FlatStyle.Flat; languageButton.FlatAppearance.BorderSize = 0; languageButton.BackColor = Theme.SurfaceBorder; languageButton.ForeColor = Theme.Text; languageButton.Font = new Font("Segoe UI Semibold", 9F); languageButton.Cursor = Cursors.Hand; languageButton.TabStop = true; languageButton.Click += delegate { ToggleLanguage(); }; header.Controls.Add(languageButton);
        statusDot.SetBounds(578, 53, 18, 18); header.Controls.Add(statusDot);
        statusLabel.SetBounds(602, 46, 286, 30); statusLabel.ForeColor = Theme.Muted; statusLabel.BackColor = Theme.Header; statusLabel.Font = new Font("Segoe UI Semibold", 9F); statusLabel.TextAlign = ContentAlignment.MiddleRight; statusLabel.AutoEllipsis = true; header.Controls.Add(statusLabel);
        SetLocalizedTooltip(statusLabel, "Состояние трансляции");

        var videoCard = new SurfacePanel { Left = 24, Top = 108, Width = 872, Height = 184 };
        Controls.Add(videoCard); AddSectionTitle(videoCard, "Видео");
        AddLabel(videoCard, "Источник", 20, 52, 70, 34, Theme.Muted, Font);
        AddLabel(videoCard, "Монитор", 92, 52, 92, 34, Theme.Text, Font);
        StyleComboBox(sourceBox); sourceBox.SetBounds(190, 52, 614, 34); videoCard.Controls.Add(sourceBox);
        sourceBox.SelectedIndexChanged += delegate { ScheduleRestart(); };
        CreateRefreshButton(videoCard, 816, 52, delegate { RefreshVideoSources(); });
        AddLabel(videoCard, "Основной поток", 20, 112, 110, 34, Theme.Muted, Font);
        StyleComboBox(resolutionBox); resolutionBox.SetBounds(138, 112, 230, 34); resolutionBox.Items.Add(T("Исходное")); resolutionBox.Items.Add(T("720p (1280x720)")); resolutionBox.SelectedIndex = 0; videoCard.Controls.Add(resolutionBox);
        resolutionBox.SelectedIndexChanged += delegate { ScheduleRestart(); };
        subEnabledBox.Text = T("Субпоток"); subEnabledBox.SetBounds(430, 115, 154, 28); subEnabledBox.ForeColor = Theme.Text; subEnabledBox.CheckedChanged += delegate { SetSubControls(); ScheduleRestart(); }; videoCard.Controls.Add(subEnabledBox); RegisterLocalized(subEnabledBox, "Субпоток");
        StyleComboBox(subResolutionBox); subResolutionBox.SetBounds(650, 112, 202, 34); subResolutionBox.Items.Add(T("CIF (352x288)")); subResolutionBox.Items.Add(T("VGA (640x480)")); subResolutionBox.SelectedIndex = 0; videoCard.Controls.Add(subResolutionBox);
        subResolutionBox.SelectedIndexChanged += delegate { if (subEnabledBox.Checked) ScheduleRestart(); };

        var audioCard = new SurfacePanel { Left = 24, Top = 308, Width = 872, Height = 140 };
        Controls.Add(audioCard); AddSectionTitle(audioCard, "Аудио · G.711 μ-law");
        audioEnabledBox.Text = T("Включить звук"); audioEnabledBox.SetBounds(690, 14, 162, 28); audioEnabledBox.ForeColor = Theme.Text; audioEnabledBox.CheckedChanged += delegate { SetAudioControls(); ScheduleRestart(); }; audioCard.Controls.Add(audioEnabledBox); RegisterLocalized(audioEnabledBox, "Включить звук");
        AddLabel(audioCard, "Режим", 20, 67, 58, 34, Theme.Muted, Font);
        StyleComboBox(audioModeBox); audioModeBox.SetBounds(82, 67, 270, 34); audioModeBox.Items.Add(T("Только микрофон")); audioModeBox.Items.Add(T("Только звук ПК (по умолчанию)")); audioModeBox.Items.Add(T("Микрофон + звук ПК")); audioModeBox.SelectedIndex = 0; audioModeBox.SelectedIndexChanged += delegate { SetAudioControls(); if (audioEnabledBox.Checked) ScheduleRestart(); }; audioCard.Controls.Add(audioModeBox);
        AddLabel(audioCard, "Микрофон", 372, 67, 74, 34, Theme.Muted, Font);
        StyleComboBox(audioBox); audioBox.SetBounds(450, 67, 354, 34); audioCard.Controls.Add(audioBox);
        audioBox.SelectedIndexChanged += delegate { if (audioEnabledBox.Checked) ScheduleRestart(); };
        CreateRefreshButton(audioCard, 816, 67, delegate { RefreshAudio(); });

        var networkCard = new SurfacePanel { Left = 24, Top = 464, Width = 872, Height = 126 };
        Controls.Add(networkCard); AddSectionTitle(networkCard, "Сеть");
        AddLabel(networkCard, "RTSP-порт", 20, 48, 120, 22, Theme.Muted, Font);
        StyleTextBox(portBox, false); portBox.SetBounds(20, 74, 120, 28); portBox.Text = "8554"; networkCard.Controls.Add(portBox);
        AddLabel(networkCard, "Основной путь", 165, 48, 310, 22, Theme.Muted, Font);
        StyleTextBox(pathBox, false); pathBox.SetBounds(165, 74, 310, 28); pathBox.Text = "desktop"; networkCard.Controls.Add(pathBox);
        AddLabel(networkCard, "Путь субпотока", 500, 48, 352, 22, Theme.Muted, Font);
        StyleTextBox(subPathBox, false); subPathBox.SetBounds(500, 74, 352, 28); subPathBox.Text = "desktop_sub"; networkCard.Controls.Add(subPathBox);
        portBox.TextChanged += delegate { ScheduleRestart(); }; pathBox.TextChanged += delegate { ScheduleRestart(); }; subPathBox.TextChanged += delegate { if (subEnabledBox.Checked) ScheduleRestart(); };

        startButton.SetBounds(24, 610, 236, 50); startButton.Click += delegate { ToggleStream(); }; Controls.Add(startButton);
        AddLabel(this, "H.264   •   G.711 μ-law   •   RTSP/TCP", 286, 619, 360, 32, Theme.Muted, new Font("Segoe UI", 9F));

        var addressCard = new SurfacePanel { Left = 24, Top = 680, Width = 872, Height = 140 };
        Controls.Add(addressCard); AddSectionTitle(addressCard, "Адреса потоков");
        AddLabel(addressCard, "Основной", 20, 48, 120, 30, Theme.Muted, Font);
        StyleTextBox(urlBox, true); urlBox.SetBounds(142, 48, 606, 28); urlBox.ReadOnly = true; addressCard.Controls.Add(urlBox);
        copyButton.Text = T("Копировать"); copyButton.SetBounds(760, 47, 92, 30); copyButton.Enabled = false; copyButton.Click += delegate { CopyAddress(urlBox.Text); }; addressCard.Controls.Add(copyButton); RegisterLocalized(copyButton, "Копировать");
        AddLabel(addressCard, "Субпоток", 20, 91, 120, 30, Theme.Muted, Font);
        StyleTextBox(subUrlBox, true); subUrlBox.SetBounds(142, 91, 606, 28); subUrlBox.ReadOnly = true; addressCard.Controls.Add(subUrlBox);
        subCopyButton.Text = T("Копировать"); subCopyButton.SetBounds(760, 90, 92, 30); subCopyButton.Enabled = false; subCopyButton.Click += delegate { CopyAddress(subUrlBox.Text); }; addressCard.Controls.Add(subCopyButton); RegisterLocalized(subCopyButton, "Копировать");

        SetStatus("Остановлено", Theme.Disabled);
        SetStartButton(false);
        SetSubControls();
        SetAudioControls();
    }

    void SetStatus(string text, Color color)
    {
        string shown = LocalizeRuntime(text);
        statusLabel.Text = shown;
        statusLabel.ForeColor = color == Theme.Disabled ? Theme.Muted : color;
        statusDot.IndicatorColor = color;
        statusDot.Invalidate();
        toolTip.SetToolTip(statusLabel, shown);
        Log("Status: " + shown);
    }

    void SetStartButton(bool running)
    {
        startButton.Text = T(running ? "Остановить поток" : "Запустить поток");
        RegisterLocalized(startButton, running ? "Остановить поток" : "Запустить поток");
        startButton.ButtonColor = running ? Theme.Danger : Theme.Accent;
        startButton.HoverColor = running ? Color.FromArgb(255, 127, 127) : Theme.AccentHover;
        startButton.ButtonTextColor = running ? Color.White : Color.FromArgb(8, 34, 29);
        startButton.Invalidate();
    }

    void SetSubControls()
    {
        subResolutionBox.Enabled = subEnabledBox.Checked; subPathBox.Enabled = subEnabledBox.Checked;
    }

    void SetAudioControls()
    {
        bool enabled = audioEnabledBox.Enabled && audioEnabledBox.Checked;
        audioModeBox.Enabled = enabled;
        audioBox.Enabled = enabled && audioModeBox.SelectedIndex != 1;
    }

    void CopyAddress(string value)
    {
        if (value.Length > 0) { Clipboard.SetText(value); Log("RTSP address copied"); SetStatus("Адрес скопирован", Theme.Blue); }
    }

    void ScheduleRestart()
    {
        if (!desiredRunning || applyingSettings) return;
        SetStatus("Применение настроек...", Theme.Warning);
        restartTimer.Stop(); restartTimer.Start();
    }

    void ToggleStream()
    {
        Log(desiredRunning ? "Stop requested" : "Start requested");
        if (desiredRunning)
        {
            desiredRunning = false; restartTimer.Stop(); StopStream();
        }
        else
        {
            desiredRunning = true;
            if (!StartPipeline(true)) { desiredRunning = false; StopStream(); }
        }
    }

    void RestartPipeline()
    {
        if (!desiredRunning) return;
        Log("Restarting pipeline after settings change");
        applyingSettings = true;
        SetStatus("Применение настроек...", Theme.Warning);
        StopProcesses();
        bool started = StartPipeline(false);
        applyingSettings = false;
        if (!started && desiredRunning) SetStatus("Настройки не применены — проверьте параметры", Theme.Danger);
    }

    void RefreshVideoSources()
    {
        string selected = sourceBox.SelectedItem == null ? "" : sourceBox.SelectedItem.ToString();
        int selectedIndexBefore = sourceBox.SelectedIndex;
        sourceBox.Items.Clear(); videoSources.Clear();
        foreach (var s in Screen.AllScreens)
        {
            var v = new VideoSource(T("Монитор") + " " + (videoSources.Count + 1) + " (" + s.Bounds.Width + "x" + s.Bounds.Height + ")", "desktop"); v.X = s.Bounds.Left; v.Y = s.Bounds.Top; v.Width = s.Bounds.Width; v.Height = s.Bounds.Height; videoSources.Add(v);
        }
        foreach (var v in videoSources) sourceBox.Items.Add(v);
        Log("Video monitors refreshed: " + videoSources.Count);
        int selectedIndex = -1;
        for (int i = 0; i < sourceBox.Items.Count; i++) if (sourceBox.Items[i].ToString() == selected) { selectedIndex = i; break; }
        if (sourceBox.Items.Count > 0) sourceBox.SelectedIndex = selectedIndex >= 0 ? selectedIndex : (selectedIndexBefore >= 0 && selectedIndexBefore < sourceBox.Items.Count ? selectedIndexBefore : 0);
    }

    void RefreshAudio()
    {
        string selected = audioBox.SelectedItem == null ? "" : audioBox.SelectedItem.ToString();
        audioBox.Items.Clear();
        if (File.Exists(Ffmpeg))
        {
            try
            {
                var psi = new ProcessStartInfo(Ffmpeg, "-hide_banner -list_devices true -f dshow -i dummy") { UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true, StandardErrorEncoding = Encoding.UTF8 };
                var p = Process.Start(psi); string output = p.StandardError.ReadToEnd(); p.WaitForExit();
                foreach (Match m in Regex.Matches(output, "\\\"([^\\\"]+)\\\"\\s+\\(audio\\)")) if (!audioBox.Items.Contains(m.Groups[1].Value)) audioBox.Items.Add(m.Groups[1].Value);
            }
            catch { }
        }
        if (audioBox.Items.Count == 0) audioBox.Items.Add(NoMicrophonesText());
        audioEnabledBox.Enabled = true;
        audioBox.SelectedIndex = audioBox.Items.Contains(selected) ? audioBox.Items.IndexOf(selected) : 0;
        SetAudioControls();
        Log("Audio devices refreshed: " + audioBox.Items.Count);
    }

    static string Q(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
    static string LocalIp()
    {
        try { using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)) { s.Connect("8.8.8.8", 80); return ((IPEndPoint)s.LocalEndPoint).Address.ToString(); } } catch { return "127.0.0.1"; }
    }
    bool IsPortFree(int port)
    {
        try { var l = new TcpListener(IPAddress.Loopback, port); l.Start(); l.Stop(); return true; } catch { return false; }
    }

    string ScaleOption(ComboBox box)
    {
        if (box.SelectedIndex == 1 && box == resolutionBox) return "-vf scale=1280:720 ";
        if (box.SelectedIndex == 1 && box == subResolutionBox) return "-vf scale=640:480 ";
        if (box.SelectedIndex == 0 && box == subResolutionBox) return "-vf scale=352:288 ";
        return "";
    }

    void AppendOutput(StringBuilder args, ComboBox resolution, string audioMap, int port, string path, int bitrate)
    {
        args.Append("-map 0:v:0 "); if (audioMap != null) args.Append("-map ").Append(Q(audioMap)).Append(" ");
        args.Append(ScaleOption(resolution));
        args.Append("-c:v libx264 -preset veryfast -tune zerolatency -pix_fmt yuv420p -b:v ").Append(bitrate).Append("k -g 30 ");
        if (audioMap != null) args.Append("-c:a pcm_mulaw -ar 8000 -ac 1 ");
        args.Append("-f rtsp -rtsp_transport tcp ").Append(Q("rtsp://127.0.0.1:" + port + "/" + path)).Append(" ");
    }

    bool TryGetLoopbackFormat(out string format, out int sampleRate, out int channels, out string error)
    {
        format = null; sampleRate = 0; channels = 0; error = null;
        if (!File.Exists(AudioLoopback)) { error = T("Рядом с программой отсутствует AudioLoopback.exe."); return false; }
        try
        {
            var process = Process.Start(new ProcessStartInfo(AudioLoopback, "--info") { WorkingDirectory = Root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
            string output = process.StandardOutput.ReadToEnd().Trim(); string stderr = process.StandardError.ReadToEnd(); process.WaitForExit(5000);
            string[] parts = output.Split('|');
            if (process.ExitCode != 0 || parts.Length != 3 || !int.TryParse(parts[1], out sampleRate) || !int.TryParse(parts[2], out channels)) { error = T("Выходное аудиоустройство Windows недоступно.") + " " + stderr; return false; }
            format = parts[0]; return true;
        }
        catch (Exception ex) { error = T("Не удалось открыть звук ПК:") + " " + ex.Message; return false; }
    }

    void CaptureLog(StreamReader reader, string file)
    {
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                string text = reader.ReadToEnd();
                lock (logLock) File.AppendAllText(file, text, Encoding.UTF8);
            }
            catch { }
        });
    }

    bool FailStart(string message, bool showMessage)
    {
        string shown = LocalizeRuntime(message);
        Log("Error: " + shown);
        SetStatus(shown, Theme.Danger);
        if (showMessage) MessageBox.Show(shown, T("Ошибка запуска"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        return false;
    }

    bool StartPipeline(bool showMessages)
    {
        Log("Pipeline start validation");
        if (!File.Exists(Ffmpeg) || !File.Exists(Mediamtx)) return FailStart(T("Рядом с программой должны находиться ffmpeg.exe и mediamtx.exe."), showMessages);
        int port; if (!int.TryParse(portBox.Text, out port) || port < 1 || port > 65535) return FailStart(T("Укажите корректный RTSP-порт."), showMessages);
        string path = pathBox.Text.Trim(); if (!Regex.IsMatch(path, "^[A-Za-z0-9_-]+$")) return FailStart(T("Проверьте путь основного потока."), showMessages);
        string subPath = subPathBox.Text.Trim();
        if (subEnabledBox.Checked && (!Regex.IsMatch(subPath, "^[A-Za-z0-9_-]+$") || subPath == path)) return FailStart(T("Проверьте путь субпотока."), showMessages);
        if (!IsPortFree(port)) return FailStart(T("RTSP-порт") + " " + port + T(" уже используется."), showMessages);
        var src = sourceBox.SelectedItem as VideoSource; if (src == null) return FailStart(T("Выберите источник видео."), showMessages);
        string paths = "  " + path + ":\n    source: publisher\n";
        if (subEnabledBox.Checked) paths += "  " + subPath + ":\n    source: publisher\n";
        try
        {
            string mtxLogPath = Path.Combine(Root, "streamer_mediamtx.log");
            string ffLogPath = Path.Combine(Root, "streamer_ffmpeg.log");
            File.WriteAllText(mtxLogPath, "", Encoding.UTF8); File.WriteAllText(ffLogPath, "", Encoding.UTF8);
            string cfg = Path.Combine(Root, "streamer_mediamtx.yml"); File.WriteAllText(cfg, "rtspAddress: :" + port + "\nprotocols: [tcp]\nrtmp: no\nhls: no\nwebrtc: no\nsrt: no\npaths:\n" + paths, Encoding.ASCII);
            var startedMtx = Process.Start(new ProcessStartInfo(Mediamtx, Q(cfg)) { WorkingDirectory = Root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
            mediamtxProcess = startedMtx; CaptureLog(startedMtx.StandardOutput, mtxLogPath); CaptureLog(startedMtx.StandardError, mtxLogPath);
            Log("MediaMTX process started on port " + port);
            Thread.Sleep(600); if (startedMtx.HasExited) { StopProcesses(); return FailStart(T("MediaMTX не запустился. Проверьте журнал."), showMessages); }
            var args = new StringBuilder("-hide_banner -loglevel warning -f gdigrab -framerate 15 ");
            args.Append("-offset_x ").Append(src.X).Append(" -offset_y ").Append(src.Y).Append(" -video_size ").Append(src.Width).Append("x").Append(src.Height).Append(" ");
            args.Append("-i ").Append(Q(src.Input)).Append(" ");
            bool sound = audioEnabledBox.Checked && audioEnabledBox.Enabled;
            bool micSound = sound && audioModeBox.SelectedIndex != 1;
            bool pcSound = sound && audioModeBox.SelectedIndex != 0;
            string micName = audioBox.SelectedItem == null ? "" : audioBox.SelectedItem.ToString();
            if (micSound && (micName.Length == 0 || micName == "Микрофоны не найдены" || micName == "No microphones found")) { StopProcesses(); return FailStart(T("Выбран режим с микрофоном, но микрофон не найден."), showMessages); }
            string mainAudioMap = null;
            string subAudioMap = null;
            if (micSound)
            {
                args.Append("-thread_queue_size 512 -f dshow -i ").Append(Q("audio=" + micName)).Append(" ");
                mainAudioMap = "1:a:0"; subAudioMap = "1:a:0";
            }
            string loopbackFormat = null; int loopbackRate = 0, loopbackChannels = 0; string loopbackError = null;
            if (pcSound)
            {
                if (!TryGetLoopbackFormat(out loopbackFormat, out loopbackRate, out loopbackChannels, out loopbackError)) { StopProcesses(); return FailStart(loopbackError, showMessages); }
                string pipeName = "DesktopStreamerAudio_" + Guid.NewGuid().ToString("N");
                loopbackPipe = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                loopbackProcess = Process.Start(new ProcessStartInfo(AudioLoopback, "") { WorkingDirectory = Root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
                var pipe = loopbackPipe; var loop = loopbackProcess;
                ThreadPool.QueueUserWorkItem(delegate { try { pipe.WaitForConnection(); loop.StandardOutput.BaseStream.CopyTo(pipe); } catch { } finally { try { pipe.Dispose(); } catch { } } });
                args.Append("-thread_queue_size 512 -f ").Append(loopbackFormat).Append(" -ar ").Append(loopbackRate).Append(" -ac ").Append(loopbackChannels).Append(" -i ").Append(Q("\\\\.\\pipe\\" + pipeName)).Append(" ");
                if (micSound)
                {
                    if (subEnabledBox.Checked)
                    {
                        args.Append("-filter_complex ").Append(Q("[1:a][2:a]amix=inputs=2:duration=longest:dropout_transition=0,asplit=2[aout_main][aout_sub]")).Append(" ");
                        mainAudioMap = "[aout_main]"; subAudioMap = "[aout_sub]";
                    }
                    else
                    {
                        args.Append("-filter_complex ").Append(Q("[1:a][2:a]amix=inputs=2:duration=longest:dropout_transition=0[aout]")).Append(" ");
                        mainAudioMap = "[aout]";
                    }
                }
                else { mainAudioMap = "1:a:0"; subAudioMap = "1:a:0"; }
            }
            AppendOutput(args, resolutionBox, mainAudioMap, port, path, 2500);
            if (subEnabledBox.Checked) AppendOutput(args, subResolutionBox, subAudioMap, port, subPath, 700);
            var startedFfmpeg = Process.Start(new ProcessStartInfo(Ffmpeg, args.ToString()) { WorkingDirectory = Root, UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true });
            ffmpegProcess = startedFfmpeg; CaptureLog(startedFfmpeg.StandardError, ffLogPath);
            Log("FFmpeg process started for monitor " + src.Name);
            Thread.Sleep(500); if (startedFfmpeg.HasExited) { StopProcesses(); return FailStart(T("FFmpeg не запустил поток. Проверьте журнал."), showMessages); }
            int startedGeneration = streamGeneration;
            startedFfmpeg.EnableRaisingEvents = true;
            startedFfmpeg.Exited += delegate
            {
                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (startedGeneration != streamGeneration || ffmpegProcess != startedFfmpeg || !desiredRunning) return;
                        StopProcesses(); SetStatus("Поток завершён — проверьте журнал", Theme.Danger); SetStartButton(false); desiredRunning = false;
                    });
                }
                catch { }
            };
            string ip = LocalIp(); SetStartButton(true); SetStatus(subEnabledBox.Checked ? T("Основной и субпоток запущены") : T("Поток запущен"), Theme.Accent); urlBox.Text = "rtsp://" + ip + ":" + port + "/" + path; copyButton.Enabled = true;
            subUrlBox.Text = subEnabledBox.Checked ? "rtsp://" + ip + ":" + port + "/" + subPath : ""; subCopyButton.Enabled = subEnabledBox.Checked;
            Log("Pipeline started successfully");
            return true;
        }
        catch (Exception ex)
        {
            Log("Pipeline start exception: " + ex);
            StopProcesses(); return FailStart("Не удалось применить настройки: " + ex.Message, showMessages);
        }
    }

    void StopStream()
    {
        Log("Stopping pipeline");
        StopProcesses();
        SetStartButton(false); SetStatus(T("Остановлено"), Theme.Disabled); urlBox.Text = ""; subUrlBox.Text = ""; copyButton.Enabled = false; subCopyButton.Enabled = false;
    }

    void StopProcesses()
    {
        streamGeneration++;
        Process oldFfmpeg = ffmpegProcess; Process oldMtx = mediamtxProcess; ffmpegProcess = null; mediamtxProcess = null;
        Process oldLoop = loopbackProcess; loopbackProcess = null;
        NamedPipeServerStream oldPipe = loopbackPipe; loopbackPipe = null;
        if (oldPipe != null) { try { oldPipe.Dispose(); } catch { } }
        if (oldFfmpeg != null) { try { if (!oldFfmpeg.HasExited) { oldFfmpeg.Kill(); oldFfmpeg.WaitForExit(2000); } oldFfmpeg.Dispose(); } catch { } }
        if (oldMtx != null) { try { if (!oldMtx.HasExited) { oldMtx.Kill(); oldMtx.WaitForExit(2000); } oldMtx.Dispose(); } catch { } }
        if (oldLoop != null) { try { if (!oldLoop.HasExited) { oldLoop.Kill(); oldLoop.WaitForExit(1000); } oldLoop.Dispose(); } catch { } }
    }
}

internal static class Program
{
    [STAThread] static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); }
}
