using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BilibiliMediaApp
{
    public sealed class MainForm : Form
    {
        private const int HotkeyToggle = 0x1001;
        private const int HotkeyBack = 0x1002;
        private const int HotkeyForward = 0x1003;

        private readonly string[] _args;
        private readonly AppSettings _settings;
        private readonly string _controlScript;

        private WebView2 _webView;
        private TextBox _urlBox;
        private Label _statusLabel;
        private Label _hotkeyLabel;
        private Button _backButton;
        private Button _forwardButton;
        private CheckBox _topMostCheck;
        private Button _settingsButton;
        private TableLayoutPanel _topBar;
        private TableLayoutPanel _bottomBar;

        private bool _webViewReady;
        private bool _commandBusy;
        private bool _offscreen;
        private string _debugPort;
        private string _startUrl;
        private HotkeyBinding _toggleBinding;
        private HotkeyBinding _backBinding;
        private HotkeyBinding _forwardBinding;
        private bool _closing;
        private bool _htmlFullscreen;
        private FormWindowState _preFullscreenState;
        private FormBorderStyle _preFullscreenBorder;
        private bool _preFullscreenTopMost;

        public MainForm(string[] args)
        {
            _args = args;
            ParseArguments();
            _settings = AppSettings.Load();
            if (!string.IsNullOrEmpty(_startUrl))
            {
                _settings.LastUrl = _startUrl;
            }
            _controlScript = LoadTextResource("ControlScript");
            _toggleBinding = HotkeyBinding.FromStorage(
                _settings.HotkeyToggle, HotkeyBinding.DefaultToggle);
            _backBinding = HotkeyBinding.FromStorage(
                _settings.HotkeyBack, HotkeyBinding.DefaultBack);
            _forwardBinding = HotkeyBinding.FromStorage(
                _settings.HotkeyForward, HotkeyBinding.DefaultForward);

            InitializeWindow();
            InitializeControls();
            ApplyWindowBounds();

            this.Load += MainForm_Load;
            this.FormClosing += MainForm_FormClosing;
            this.Resize += delegate
            {
                ApplyPageZoom();
                AdjustResponsiveLayout();
            };
        }

        private void ParseArguments()
        {
            for (int i = 0; i < _args.Length; i++)
            {
                string arg = _args[i];
                if (string.Equals(arg, "--offscreen", StringComparison.OrdinalIgnoreCase))
                {
                    _offscreen = true;
                }
                else if (arg.StartsWith("--url=", StringComparison.OrdinalIgnoreCase))
                {
                    _startUrl = arg.Substring("--url=".Length);
                }
                else if (arg.StartsWith("--debug-port=", StringComparison.OrdinalIgnoreCase))
                {
                    _debugPort = arg.Substring("--debug-port=".Length);
                }
            }
        }

        private static string LoadTextResource(string name)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null) return string.Empty;
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static Icon LoadAppIcon()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AppIcon"))
            {
                if (stream == null) return SystemIcons.Application;
                return new Icon(stream);
            }
        }

        private void InitializeWindow()
        {
            this.Text = "B站跟视频助手";
            this.Icon = LoadAppIcon();
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.BackColor = Color.FromArgb(15, 17, 22);
            this.ForeColor = Color.FromArgb(232, 234, 240);
            this.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            this.Size = new Size(1440, 820);
            this.StartPosition = FormStartPosition.Manual;
            this.DoubleBuffered = true;
        }

        private void InitializeControls()
        {
            this.SuspendLayout();

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.BackColor = Color.FromArgb(15, 17, 22);
            root.Padding = new Padding(0);
            root.Margin = new Padding(0);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));

            _webView = new WebView2();
            _webView.Dock = DockStyle.Fill;
            _webView.BackColor = Color.Black;
            _webView.Margin = new Padding(0);

            _topBar = BuildTopBar();
            _bottomBar = BuildBottomBar();
            root.Controls.Add(_topBar, 0, 0);
            root.Controls.Add(_webView, 0, 1);
            root.Controls.Add(_bottomBar, 0, 2);
            this.Controls.Add(root);
            _webView.SendToBack();
            _topBar.BringToFront();
            _bottomBar.BringToFront();
            SetUrlText(_settings.LastUrl);

            this.ResumeLayout(false);
        }

        private TableLayoutPanel BuildTopBar()
        {
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 3;
            panel.RowCount = 1;
            panel.BackColor = Color.FromArgb(24, 27, 34);
            panel.Padding = new Padding(10, 12, 10, 10);
            panel.Margin = new Padding(0);
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140f));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var nav = new FlowLayoutPanel();
            nav.Dock = DockStyle.Fill;
            nav.WrapContents = false;
            nav.FlowDirection = FlowDirection.LeftToRight;
            nav.BackColor = Color.Transparent;
            nav.Padding = new Padding(0);
            nav.Margin = new Padding(0);

            _backButton = CreateNavButton("←", 38);
            _backButton.Click += delegate { GoBack(); };
            _forwardButton = CreateNavButton("→", 38);
            _forwardButton.Click += delegate { GoForward(); };

            Button reloadButton = CreateNavButton("⟳", 38);
            reloadButton.Click += delegate { ReloadPage(); };

            Button homeButton = CreateTextButton("B站", 62);
            homeButton.Click += delegate { NavigateTo("https://www.bilibili.com/"); };

            nav.Controls.Add(_backButton);
            nav.Controls.Add(_forwardButton);
            nav.Controls.Add(reloadButton);
            nav.Controls.Add(homeButton);

            var right = new FlowLayoutPanel();
            right.Dock = DockStyle.Fill;
            right.WrapContents = false;
            right.FlowDirection = FlowDirection.RightToLeft;
            right.BackColor = Color.Transparent;
            right.Padding = new Padding(0);
            right.Margin = new Padding(0);

            _settingsButton = CreateTextButton("设置", 64);
            _settingsButton.Click += delegate { ShowSettingsDialog(); };

            _topMostCheck = new CheckBox();
            _topMostCheck.Text = "置顶";
            _topMostCheck.Appearance = Appearance.Button;
            _topMostCheck.FlatStyle = FlatStyle.Flat;
            _topMostCheck.TextAlign = ContentAlignment.MiddleCenter;
            _topMostCheck.Width = 62;
            _topMostCheck.Height = 32;
            _topMostCheck.BackColor = Color.FromArgb(34, 38, 48);
            _topMostCheck.ForeColor = Color.FromArgb(220, 224, 235);
            _topMostCheck.FlatAppearance.BorderSize = 0;
            _topMostCheck.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 57, 72);
            _topMostCheck.FlatAppearance.CheckedBackColor = Color.FromArgb(251, 114, 153);
            _topMostCheck.Cursor = Cursors.Hand;
            _topMostCheck.Margin = new Padding(4, 0, 0, 0);
            _topMostCheck.CheckedChanged += delegate
            {
                this.TopMost = _topMostCheck.Checked;
                _settings.TopMost = _topMostCheck.Checked;
            };

            right.Controls.Add(_settingsButton);
            right.Controls.Add(_topMostCheck);

            var urlHost = new Panel();
            urlHost.Dock = DockStyle.Fill;
            urlHost.BackColor = Color.Transparent;
            urlHost.Padding = new Padding(8, 0, 8, 0);
            urlHost.Margin = new Padding(0);

            var urlWrap = new Panel();
            urlWrap.Dock = DockStyle.Fill;
            urlWrap.BackColor = Color.FromArgb(38, 42, 53);
            urlWrap.Padding = new Padding(10, 6, 10, 4);

            _urlBox = new TextBox();
            _urlBox.BorderStyle = BorderStyle.None;
            _urlBox.Dock = DockStyle.Fill;
            _urlBox.BackColor = Color.FromArgb(38, 42, 53);
            _urlBox.ForeColor = Color.FromArgb(232, 234, 240);
            _urlBox.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
            _urlBox.ShortcutsEnabled = true;
            _urlBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    NavigateFromInput();
                }
            };

            urlWrap.Controls.Add(_urlBox);
            urlHost.Controls.Add(urlWrap);

            panel.Controls.Add(nav, 0, 0);
            panel.Controls.Add(urlHost, 1, 0);
            panel.Controls.Add(right, 2, 0);
            right.BringToFront();
            nav.BringToFront();

            return panel;
        }

        private TableLayoutPanel BuildBottomBar()
        {
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 2;
            panel.RowCount = 1;
            panel.BackColor = Color.FromArgb(13, 15, 20);
            panel.Margin = new Padding(0);
            panel.Padding = new Padding(0);
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380f));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _hotkeyLabel = new Label();
            _hotkeyLabel.Dock = DockStyle.Fill;
            _hotkeyLabel.TextAlign = ContentAlignment.MiddleRight;
            _hotkeyLabel.Padding = new Padding(0, 0, 12, 0);
            _hotkeyLabel.ForeColor = Color.FromArgb(120, 128, 145);
            _hotkeyLabel.Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Regular, GraphicsUnit.Point);
            _hotkeyLabel.Text = "Ctrl+Shift+1 播放/暂停   ·   Ctrl+Shift+2 快退   ·   Ctrl+Shift+3 快进";
            _hotkeyLabel.Margin = new Padding(0);

            _statusLabel = new Label();
            _statusLabel.Dock = DockStyle.Fill;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _statusLabel.Padding = new Padding(12, 0, 0, 0);
            _statusLabel.ForeColor = Color.FromArgb(150, 157, 172);
            _statusLabel.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            _statusLabel.Text = "正在启动内置浏览器…";
            _statusLabel.AutoEllipsis = true;
            _statusLabel.Margin = new Padding(0);

            panel.Controls.Add(_statusLabel, 0, 0);
            panel.Controls.Add(_hotkeyLabel, 1, 0);
            _hotkeyLabel.BringToFront();
            _statusLabel.BringToFront();
            return panel;
        }

        private static Button CreateNavButton(string text, int width)
        {
            var button = new Button();
            button.Text = text;
            button.Width = width;
            button.Height = 32;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 57, 72);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(70, 76, 94);
            button.BackColor = Color.FromArgb(34, 38, 48);
            button.ForeColor = Color.FromArgb(220, 224, 235);
            button.Font = new Font("Segoe UI Symbol", 11f, FontStyle.Bold, GraphicsUnit.Point);
            button.Cursor = Cursors.Hand;
            button.TabStop = false;
            button.Margin = new Padding(0, 0, 4, 0);
            return button;
        }

        private static Button CreateTextButton(string text, int width)
        {
            var button = new Button();
            button.Text = text;
            button.Width = width;
            button.Height = 32;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 57, 72);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(70, 76, 94);
            button.BackColor = Color.FromArgb(34, 38, 48);
            button.ForeColor = Color.FromArgb(220, 224, 235);
            button.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            button.Cursor = Cursors.Hand;
            button.TabStop = false;
            button.Margin = new Padding(0, 0, 4, 0);
            return button;
        }

        private void ApplyWindowBounds()
        {
            if (_offscreen)
            {
                this.StartPosition = FormStartPosition.Manual;
                this.Location = new Point(-32000, -32000);
                this.Size = new Size(1100, 760);
                return;
            }

            if (_settings.HasWindowBounds)
            {
                var bounds = new Rectangle(
                    _settings.WindowX,
                    _settings.WindowY,
                    _settings.WindowWidth,
                    _settings.WindowHeight);
                if (IsOnAnyScreen(bounds))
                {
                    this.StartPosition = FormStartPosition.Manual;
                    this.Bounds = bounds;
                    this.TopMost = _settings.TopMost;
                    return;
                }
            }

            Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
            int width = Math.Min(_settings.WindowWidth, Math.Max(300, workArea.Width - 40));
            int height = Math.Min(_settings.WindowHeight, Math.Max(200, workArea.Height - 60));
            this.Size = new Size(width, height);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = _settings.TopMost;
        }

        private static bool IsOnAnyScreen(Rectangle bounds)
        {
            Screen[] screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                if (screens[i].WorkingArea.IntersectsWith(bounds))
                {
                    return true;
                }
            }
            return false;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyDarkTitleBar();
        }

        private void ApplyDarkTitleBar()
        {
            try
            {
                int enabled = 1;
                NativeMethods.DwmSetWindowAttribute(this.Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
                int caption = ColorTranslator.ToWin32(Color.FromArgb(24, 27, 34));
                NativeMethods.DwmSetWindowAttribute(this.Handle, NativeMethods.DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
                int text = ColorTranslator.ToWin32(Color.FromArgb(240, 242, 247));
                NativeMethods.DwmSetWindowAttribute(this.Handle, NativeMethods.DWMWA_TEXT_COLOR, ref text, sizeof(int));
            }
            catch
            {
            }
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            if (_topMostCheck != null)
            {
                _topMostCheck.Checked = _settings.TopMost;
            }
            RegisterGlobalHotkeys();
            UpdateHotkeyLabel();
            AdjustResponsiveLayout();
            await InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                string runtimeVersion = null;
                try
                {
                    runtimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
                }
                catch
                {
                    runtimeVersion = null;
                }

                if (string.IsNullOrEmpty(runtimeVersion))
                {
                    ShowWebViewRuntimeMissing();
                    return;
                }

                string dataDirectory = ResolveWebViewDataDirectory();
                Directory.CreateDirectory(dataDirectory);

                string browserArguments = "--autoplay-policy=no-user-gesture-required";
                if (!string.IsNullOrEmpty(_debugPort))
                {
                    browserArguments += " --remote-debugging-port=" + _debugPort;
                }

                var options = new CoreWebView2EnvironmentOptions(browserArguments);
                CoreWebView2Environment environment =
                    await CoreWebView2Environment.CreateAsync(null, dataDirectory, options);
                await _webView.EnsureCoreWebView2Async(environment);

                CoreWebView2 core = _webView.CoreWebView2;
                core.Settings.AreDefaultContextMenusEnabled = true;
                core.Settings.AreDevToolsEnabled = !string.IsNullOrEmpty(_debugPort);
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = true;

                core.NewWindowRequested += Core_NewWindowRequested;
                core.NavigationStarting += Core_NavigationStarting;
                core.NavigationCompleted += Core_NavigationCompleted;
                core.DocumentTitleChanged += Core_DocumentTitleChanged;
                core.SourceChanged += Core_SourceChanged;
                core.HistoryChanged += Core_HistoryChanged;
                core.ProcessFailed += Core_ProcessFailed;
                core.ContainsFullScreenElementChanged += Core_ContainsFullScreenElementChanged;

                _webViewReady = true;
                ApplyPageZoom();
                SetStatus("正在打开 B 站…", false);

                string startUrl = string.IsNullOrEmpty(_settings.LastUrl)
                    ? "https://www.bilibili.com/"
                    : _settings.LastUrl;
                core.Navigate(startUrl);
            }
            catch (Exception ex)
            {
                SetStatus("内置浏览器启动失败：" + ex.Message, true);
                MessageBox.Show(
                    "内置浏览器启动失败。\n\n" + ex.Message +
                    "\n\n如果系统缺少 WebView2 运行时，请安装后重试。",
                    "B站跟视频助手",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private string ResolveWebViewDataDirectory()
        {
            string primary = Path.Combine(AppSettings.DataDirectory, "WebView2");
            try
            {
                Directory.CreateDirectory(primary);
                return primary;
            }
            catch
            {
                string fallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BilibiliMediaApp", "WebView2");
                Directory.CreateDirectory(fallback);
                return fallback;
            }
        }

        private void ShowWebViewRuntimeMissing()
        {
            SetStatus("缺少 Microsoft Edge WebView2 运行时", true);
            DialogResult result = MessageBox.Show(
                "此应用需要 Microsoft Edge WebView2 运行时。\n\n" +
                "Windows 11 通常已经自带；如果缺失，可以点击“是”打开微软下载页面。",
                "B站跟视频助手",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
            {
                OpenExternal("https://go.microsoft.com/fwlink/p/?LinkId=2124703");
            }
        }

        private void Core_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            if (IsBilibiliUri(e.Uri))
            {
                if (_webViewReady)
                {
                    _webView.CoreWebView2.Navigate(e.Uri);
                }
            }
            else
            {
                OpenExternal(e.Uri);
            }
        }

        private void Core_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (!IsBilibiliUri(e.Uri) && !string.Equals(e.Uri, "about:blank", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                OpenExternal(e.Uri);
                return;
            }
            SetStatus("正在加载…", false);
        }

        private void Core_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            UpdateNavigationButtons();
            if (!e.IsSuccess)
            {
                SetStatus("页面加载失败：" + e.WebErrorStatus, true);
                return;
            }
            SetStatus("就绪 · 浏览器在后台也能控制视频", false);
            ApplyPageZoom();
            if (_webViewReady)
            {
                SetUrlText(_webView.CoreWebView2.Source);
            }
        }

        private void Core_DocumentTitleChanged(object sender, object e)
        {
            if (!_webViewReady) return;
            string title = _webView.CoreWebView2.DocumentTitle;
            this.Text = string.IsNullOrEmpty(title)
                ? "B站跟视频助手"
                : "B站跟视频助手 - " + title;
        }

        private void Core_SourceChanged(object sender, CoreWebView2SourceChangedEventArgs e)
        {
            if (_webViewReady)
            {
                SetUrlText(_webView.CoreWebView2.Source);
            }
        }

        private void SetUrlText(string url)
        {
            if (_urlBox == null || _urlBox.Focused || string.IsNullOrEmpty(url))
            {
                return;
            }
            if (!string.Equals(_urlBox.Text, url, StringComparison.Ordinal))
            {
                _urlBox.Text = url;
                _urlBox.SelectionStart = 0;
                _urlBox.SelectionLength = 0;
            }
        }

        private void Core_HistoryChanged(object sender, object e)
        {
            UpdateNavigationButtons();
        }

        private void Core_ProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
        {
            SetStatus("内置浏览器进程异常：" + e.ProcessFailedKind + "，请重启应用", true);
        }

        private void Core_ContainsFullScreenElementChanged(object sender, object e)
        {
            if (!_webViewReady) return;

            bool fullscreen = _webView.CoreWebView2.ContainsFullScreenElement;
            if (fullscreen == _htmlFullscreen) return;
            _htmlFullscreen = fullscreen;

            if (fullscreen)
            {
                _preFullscreenState = this.WindowState;
                _preFullscreenBorder = this.FormBorderStyle;
                _preFullscreenTopMost = this.TopMost;
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;
                this.TopMost = true;
            }
            else
            {
                this.TopMost = _preFullscreenTopMost;
                this.FormBorderStyle = _preFullscreenBorder;
                this.WindowState = _preFullscreenState;
            }
        }

        private void ApplyPageZoom()
        {
            if (!_webViewReady || _webView == null)
            {
                return;
            }

            double width = _webView.Width > 0 ? _webView.Width : this.ClientSize.Width;
            if (width <= 0)
            {
                return;
            }

            // Bilibili's desktop layout has a wide minimum width. Zooming out
            // on smaller windows keeps more of the page usable.
            double zoom = Math.Min(1.0, Math.Max(0.5, width / 1400.0));
            try
            {
                if (Math.Abs(_webView.ZoomFactor - zoom) > 0.01)
                {
                    _webView.ZoomFactor = zoom;
                }
            }
            catch
            {
            }
        }

        private void AdjustResponsiveLayout()
        {
            if (_bottomBar == null || _hotkeyLabel == null)
            {
                return;
            }

            bool showHotkeys = this.ClientSize.Width >= 620;
            _hotkeyLabel.Visible = showHotkeys;
            _bottomBar.ColumnStyles[1].Width = showHotkeys ? 380f : 0f;
        }

        private void UpdateNavigationButtons()
        {
            if (!_webViewReady) return;
            _backButton.Enabled = _webView.CoreWebView2.CanGoBack;
            _forwardButton.Enabled = _webView.CoreWebView2.CanGoForward;
        }

        private void GoBack()
        {
            if (_webViewReady && _webView.CoreWebView2.CanGoBack)
            {
                _webView.CoreWebView2.GoBack();
            }
        }

        private void GoForward()
        {
            if (_webViewReady && _webView.CoreWebView2.CanGoForward)
            {
                _webView.CoreWebView2.GoForward();
            }
        }

        private void ReloadPage()
        {
            if (_webViewReady)
            {
                _webView.CoreWebView2.Reload();
            }
        }

        private void NavigateFromInput()
        {
            string input = (_urlBox.Text ?? string.Empty).Trim();
            if (input.Length == 0) return;

            string url = BuildUrl(input);
            if (!string.IsNullOrEmpty(url))
            {
                NavigateTo(url);
            }
        }

        private static string BuildUrl(string input)
        {
            if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return input;
            }
            if (input.StartsWith("bilibili.com", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("www.bilibili.com", StringComparison.OrdinalIgnoreCase))
            {
                return "https://" + input;
            }
            if (input.StartsWith("BV", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("av", StringComparison.OrdinalIgnoreCase))
            {
                return "https://www.bilibili.com/video/" + input;
            }
            return "https://search.bilibili.com/all?keyword=" + Uri.EscapeDataString(input);
        }

        private void NavigateTo(string url)
        {
            if (_webViewReady)
            {
                _webView.CoreWebView2.Navigate(url);
            }
            else
            {
                _settings.LastUrl = url;
                SetStatus("内置浏览器还在启动，请稍候…", false);
            }
        }

        private static bool IsBilibiliUri(string uriString)
        {
            if (string.IsNullOrEmpty(uriString)) return false;
            if (string.Equals(uriString, "about:blank", StringComparison.OrdinalIgnoreCase)) return true;

            Uri uri;
            if (!Uri.TryCreate(uriString, UriKind.Absolute, out uri)) return false;
            if (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string host = uri.Host.ToLowerInvariant();
            return host == "bilibili.com" ||
                   host.EndsWith(".bilibili.com", StringComparison.Ordinal) ||
                   host == "b23.tv" ||
                   host.EndsWith(".b23.tv", StringComparison.Ordinal);
        }

        private static void OpenExternal(string url)
        {
            try
            {
                if (string.IsNullOrEmpty(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
            }
        }

        private void RegisterGlobalHotkeys()
        {
            UnregisterGlobalHotkeys();

            string error;
            if (TryRegisterBindings(_toggleBinding, _backBinding, _forwardBinding, out error))
            {
                UpdateHotkeyLabel();
                SetStatus("就绪 · " + ShortcutSummary(), false);
                return;
            }

            UnregisterGlobalHotkeys();
            SetStatus("快捷键注册失败：" + error, true);
            MessageBox.Show(
                "无法注册全局快捷键。\n\n" +
                error +
                "\n\n请打开右上角“设置”，换一个没有被其他软件占用的按键。\n" +
                "单键会被系统全局占用，请确认游戏没有使用该键。",
                "B站跟视频助手",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private bool TryRegisterBindings(
            HotkeyBinding toggle,
            HotkeyBinding back,
            HotkeyBinding forward,
            out string error)
        {
            error = null;
            if (!RegisterBinding(HotkeyToggle, toggle, "播放/暂停", out error) ||
                !RegisterBinding(HotkeyBack, back, "快退", out error) ||
                !RegisterBinding(HotkeyForward, forward, "快进", out error))
            {
                UnregisterGlobalHotkeys();
                return false;
            }
            return true;
        }

        private bool RegisterBinding(
            int id,
            HotkeyBinding binding,
            string actionName,
            out string error)
        {
            error = null;
            if (binding == null || binding.IsEmpty)
            {
                error = actionName + "：还没有设置快捷键";
                return false;
            }

            string validation = binding.Validate();
            if (validation != null)
            {
                error = actionName + "：" + validation;
                return false;
            }

            if (!NativeMethods.RegisterHotKey(
                this.Handle,
                id,
                binding.Modifiers | NativeMethods.MOD_NOREPEAT,
                (uint)binding.Key))
            {
                error = actionName + "：" + binding.DisplayText + " 已被其他程序占用";
                return false;
            }
            return true;
        }

        private void UnregisterGlobalHotkeys()
        {
            try
            {
                NativeMethods.UnregisterHotKey(this.Handle, HotkeyToggle);
                NativeMethods.UnregisterHotKey(this.Handle, HotkeyBack);
                NativeMethods.UnregisterHotKey(this.Handle, HotkeyForward);
            }
            catch
            {
            }
        }

        private void UpdateHotkeyLabel()
        {
            if (_hotkeyLabel == null) return;
            _hotkeyLabel.Text = ShortcutSummary();
        }

        private string ShortcutSummary()
        {
            return ShortcutSummary(_toggleBinding, _backBinding, _forwardBinding);
        }

        private static string ShortcutSummary(
            HotkeyBinding toggle,
            HotkeyBinding back,
            HotkeyBinding forward)
        {
            return toggle.DisplayText + " 播放/暂停   ·   " +
                   back.DisplayText + " 快退   ·   " +
                   forward.DisplayText + " 快进";
        }

        private static string ValidateBindings(
            HotkeyBinding toggle,
            HotkeyBinding back,
            HotkeyBinding forward)
        {
            if (toggle == null || back == null || forward == null)
            {
                return "请为三个动作都设置快捷键";
            }

            string error = toggle.Validate();
            if (error != null) return "播放/暂停：" + error;
            error = back.Validate();
            if (error != null) return "快退：" + error;
            error = forward.Validate();
            if (error != null) return "快进：" + error;

            if (toggle.SameKey(back) || toggle.SameKey(forward) || back.SameKey(forward))
            {
                return "三个动作不能使用同一个快捷键";
            }
            return null;
        }

        private static string BuildHotkeyWarnings(
            HotkeyBinding toggle,
            HotkeyBinding back,
            HotkeyBinding forward)
        {
            bool hasSingleKey = toggle.IsSingleKey || back.IsSingleKey || forward.IsSingleKey;
            return hasSingleKey
                ? "注意：单键会被系统全局占用，请确认游戏没有使用这些键。"
                : string.Empty;
        }

        public HotkeyApplyResult CheckHotkeys(
            HotkeyBinding toggle,
            HotkeyBinding back,
            HotkeyBinding forward)
        {
            string validation = ValidateBindings(toggle, back, forward);
            if (validation != null)
            {
                return HotkeyApplyResult.Failure(validation);
            }

            UnregisterGlobalHotkeys();
            string error;
            bool ok = TryRegisterBindings(toggle, back, forward, out error);
            UnregisterGlobalHotkeys();

            string restoreError;
            if (!TryRegisterBindings(_toggleBinding, _backBinding, _forwardBinding, out restoreError))
            {
                SetStatus("恢复原快捷键失败：" + restoreError, true);
            }

            if (!ok)
            {
                return HotkeyApplyResult.Failure("检测到冲突：" + error);
            }

            return HotkeyApplyResult.Success(
                "未检测到冲突：" + ShortcutSummary(toggle, back, forward),
                BuildHotkeyWarnings(toggle, back, forward));
        }

        public HotkeyApplyResult ApplyHotkeys(
            HotkeyBinding toggle,
            HotkeyBinding back,
            HotkeyBinding forward)
        {
            string validation = ValidateBindings(toggle, back, forward);
            if (validation != null)
            {
                return HotkeyApplyResult.Failure(validation);
            }

            HotkeyBinding oldToggle = _toggleBinding;
            HotkeyBinding oldBack = _backBinding;
            HotkeyBinding oldForward = _forwardBinding;

            UnregisterGlobalHotkeys();
            string error;
            if (!TryRegisterBindings(toggle, back, forward, out error))
            {
                string restoreError;
                if (!TryRegisterBindings(oldToggle, oldBack, oldForward, out restoreError))
                {
                    SetStatus("恢复原快捷键失败：" + restoreError, true);
                }
                return HotkeyApplyResult.Failure(error);
            }

            _toggleBinding = toggle;
            _backBinding = back;
            _forwardBinding = forward;
            UpdateHotkeyLabel();
            return HotkeyApplyResult.Success(
                "快捷键已应用：" + ShortcutSummary(),
                BuildHotkeyWarnings(toggle, back, forward));
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HotkeyToggle || id == HotkeyBack || id == HotkeyForward)
                {
                    OnHotkey(id);
                    return;
                }
            }
            base.WndProc(ref m);
        }

        private async void OnHotkey(int id)
        {
            if (!_webViewReady || _commandBusy) return;
            _commandBusy = true;

            string action = id == HotkeyToggle ? "toggle" : (id == HotkeyBack ? "back" : "forward");
            try
            {
                string script = _controlScript
                    .Replace("__ACTION__", action)
                    .Replace("__STEP__", _settings.SeekSeconds.ToString(CultureInfo.InvariantCulture));
                string result = await _webView.ExecuteScriptAsync(script);
                ShowCommandResult(action, result);
            }
            catch (Exception ex)
            {
                SetStatus("控制失败：" + ex.Message, true);
            }
            finally
            {
                _commandBusy = false;
            }
        }

        private void ShowCommandResult(string action, string rawResult)
        {
            if (string.IsNullOrEmpty(rawResult) || string.Equals(rawResult, "null", StringComparison.Ordinal))
            {
                SetStatus("未找到B站视频，请先在应用内打开视频页", true);
                return;
            }

            Dictionary<string, object> data = null;
            try
            {
                var serializer = new JavaScriptSerializer();
                data = serializer.Deserialize<Dictionary<string, object>>(rawResult);
            }
            catch
            {
                data = null;
            }

            if (data == null)
            {
                SetStatus("控制脚本没有返回有效结果", true);
                return;
            }

            if (!GetBool(data, "ok"))
            {
                string reason = GetString(data, "reason");
                if (reason == "no-video")
                {
                    SetStatus("未找到B站视频，请先在应用内打开视频页", true);
                }
                else
                {
                    SetStatus("控制失败：" + (string.IsNullOrEmpty(reason) ? "未知错误" : reason), true);
                }
                return;
            }

            bool paused = GetBool(data, "paused");
            bool wasPaused = GetBool(data, "wasPaused");
            double current = GetNumber(data, "currentTime");
            double duration = GetNumber(data, "duration");

            if (action == "toggle")
            {
                if (wasPaused && paused)
                {
                    SetStatus("浏览器拦截了播放：请先在视频上手动点一次播放", true);
                    return;
                }
                SetStatus(paused ? "已暂停" : "正在播放", false);
                return;
            }

            string timeText = FormatTime(current) + (duration > 0 ? " / " + FormatTime(duration) : " / 直播");
            SetStatus((action == "back" ? "快退 " : "快进 ") + _settings.SeekSeconds + " 秒 · " + timeText, false);
        }

        private static bool GetBool(Dictionary<string, object> data, string key)
        {
            object value;
            if (data != null && data.TryGetValue(key, out value) && value != null)
            {
                try
                {
                    return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                }
            }
            return false;
        }

        private static double GetNumber(Dictionary<string, object> data, string key)
        {
            object value;
            if (data != null && data.TryGetValue(key, out value) && value != null)
            {
                if (value is double) return (double)value;
                if (value is int) return (int)value;
                if (value is long) return (long)value;
                double parsed;
                if (double.TryParse(
                    Convert.ToString(value, CultureInfo.InvariantCulture),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out parsed))
                {
                    return parsed;
                }
            }
            return 0;
        }

        private static string GetString(Dictionary<string, object> data, string key)
        {
            object value;
            if (data != null && data.TryGetValue(key, out value) && value != null)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            return string.Empty;
        }

        private static string FormatTime(double seconds)
        {
            int total = (int)Math.Floor(Math.Max(0, seconds));
            int hours = total / 3600;
            int minutes = (total % 3600) / 60;
            int secs = total % 60;
            if (hours > 0)
            {
                return hours.ToString(CultureInfo.InvariantCulture) + ":" +
                       minutes.ToString("00", CultureInfo.InvariantCulture) + ":" +
                       secs.ToString("00", CultureInfo.InvariantCulture);
            }
            return minutes.ToString(CultureInfo.InvariantCulture) + ":" +
                   secs.ToString("00", CultureInfo.InvariantCulture);
        }

        private void SetStatus(string text, bool error)
        {
            if (_statusLabel == null) return;
            _statusLabel.Text = text;
            _statusLabel.ForeColor = error
                ? Color.FromArgb(255, 120, 120)
                : Color.FromArgb(150, 157, 172);
        }

        private void ShowSettingsDialog()
        {
            using (var dialog = new SettingsDialog(
                _settings,
                _toggleBinding,
                _backBinding,
                _forwardBinding,
                CheckHotkeys,
                ApplyHotkeys))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _settings.SeekSeconds = dialog.SeekSeconds;
                    _settings.TopMost = dialog.TopMostWindow;
                    _settings.HotkeyToggle = dialog.ToggleBinding.ToStorageString();
                    _settings.HotkeyBack = dialog.BackBinding.ToStorageString();
                    _settings.HotkeyForward = dialog.ForwardBinding.ToStorageString();
                    _topMostCheck.Checked = _settings.TopMost;
                    this.TopMost = _settings.TopMost;
                    _settings.Save();
                    UpdateHotkeyLabel();
                    SetStatus("设置已保存 · " + ShortcutSummary(), false);
                }
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_closing) return;
            _closing = true;

            UnregisterGlobalHotkeys();

            try
            {
                if (_webViewReady && _webView.CoreWebView2 != null)
                {
                    _settings.LastUrl = _webView.CoreWebView2.Source;
                }
            }
            catch
            {
            }

            _settings.TopMost = _topMostCheck != null && _topMostCheck.Checked;
            if (this.WindowState == FormWindowState.Normal)
            {
                _settings.WindowX = this.Left;
                _settings.WindowY = this.Top;
                _settings.WindowWidth = this.Width;
                _settings.WindowHeight = this.Height;
                _settings.HasWindowBounds = true;
            }
            else
            {
                Rectangle restore = this.RestoreBounds;
                if (restore.Width >= 900 && restore.Height >= 600)
                {
                    _settings.WindowX = restore.X;
                    _settings.WindowY = restore.Y;
                    _settings.WindowWidth = restore.Width;
                    _settings.WindowHeight = restore.Height;
                    _settings.HasWindowBounds = true;
                }
            }
            _settings.Save();

            try
            {
                if (_webView != null)
                {
                    _webView.Dispose();
                }
            }
            catch
            {
            }
        }
    }
}
