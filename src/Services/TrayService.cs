using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using AiSign.Core;
using AiSign.Stores;

namespace AiSign.Services
{
    internal sealed class TrayService : IDisposable
    {
        private readonly AppController _app;
        private readonly Action _showWindow;
        private readonly Action _showCheckin;
        private readonly Action _exit;
        private readonly System.Windows.Threading.Dispatcher _dispatcher;

        private NotifyIcon _tray;
        private ContextMenuStrip _menu;
        private ToolStripMenuItem _pauseItem;
        private ToolStripMenuItem _autostartItem;
        private Icon _currentIcon;
        private bool _disposed;

        public TrayService(AppController app, Action showWindow, Action showCheckin, Action exit)
        {
            _app = app;
            _showWindow = showWindow;
            _showCheckin = showCheckin;
            _exit = exit;
            _dispatcher = System.Windows.Application.Current == null ? null : System.Windows.Application.Current.Dispatcher;
        }

        public void Create()
        {
            _menu = new ContextMenuStrip();
            _menu.ShowImageMargin = false;
            _menu.BackColor = Color.FromArgb(0x17, 0x1A, 0x21);
            _menu.ForeColor = Color.FromArgb(0xE8, 0xEA, 0xF0);
            _menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());
            _menu.Items.Add("打开主界面", null, delegate { Show(); });
            _menu.Items.Add("立即签到", null, delegate { RunNow(); });
            _pauseItem = new ToolStripMenuItem("暂停自动签到");
            _pauseItem.Click += delegate { TogglePause(); };
            _menu.Items.Add(_pauseItem);
            _menu.Items.Add(new ToolStripSeparator());
            _autostartItem = new ToolStripMenuItem("开机自启");
            _autostartItem.Click += delegate { ToggleAutostart(); };
            _menu.Items.Add(_autostartItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("退出", null, delegate { if (_exit != null) _exit(); });

            _tray = new NotifyIcon();
            _tray.Text = "AiSign · AI 签到助手";
            _tray.Icon = IconFactory.Create(Color.FromArgb(0x8B, 0x6D, 0xFF), 32);
            _tray.ContextMenuStrip = _menu;
            _tray.Visible = true;
            _tray.MouseClick += TrayMouseClick;
            _tray.DoubleClick += delegate { if (_showCheckin != null) _showCheckin(); };

            _app.AccountsChanged += OnChanged;
            _app.ResultsChanged += OnChanged;
            _app.AccountCompleted += OnCompleted;
            Refresh();
        }

        private void TrayMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) Show();
        }

        private void Show()
        {
            if (_showWindow != null) _showWindow();
        }

        private void RunNow()
        {
            _app.RunNow(false, null, null, null);
            Balloon("AiSign", "已开始签到…", ToolTipIcon.Info);
        }

        private void TogglePause()
        {
            bool next = !_app.Settings.AutoCheckin;
            _app.SetAutoCheckin(next);
            Refresh();
            Balloon("AiSign", next ? "自动签到已开启" : "自动签到已暂停", ToolTipIcon.Info);
        }

        private void ToggleAutostart()
        {
            bool next = !AutostartService.IsEnabled();
            AutostartService.SetEnabled(next);
            Refresh();
            Balloon("AiSign", next ? "已开启开机自启" : "已关闭开机自启", ToolTipIcon.Info);
        }

        private void OnChanged()
        {
            Post(Refresh);
        }

        private void OnCompleted(Account account, CheckinOutcome outcome)
        {
            Post(delegate
            {
                Refresh();
                if (!_app.Settings.Notify.Balloon) return;
                string mode = _app.Settings.Notify.Mode;
                bool isSuccess = outcome != null && (outcome.Kind == OutcomeKind.Success || outcome.Kind == OutcomeKind.Already);
                if (mode == "failures" && isSuccess) return;
                if (mode == "none") return;
                string title = "AiSign · " + PlatformIds.DisplayName(account.Platform);
                string text = outcome == null ? "签到完成" : outcome.Message;
                Balloon(title, text, isSuccess ? ToolTipIcon.Info : ToolTipIcon.Warning);
            });
        }

        private void Post(Action action)
        {
            if (action == null) return;
            if (_dispatcher != null) _dispatcher.BeginInvoke(action);
            else action();
        }

        public void Refresh()
        {
            try
            {
                List<Account> accounts = _app.Accounts.All();
                Dictionary<string, CheckinRecord> today = _app.Today();
                int enabled = 0;
                int done = 0;
                int failed = 0;
                int waiting = 0;
                for (int i = 0; i < accounts.Count; i++)
                {
                    Account a = accounts[i];
                    if (!a.Enabled) continue;
                    enabled++;
                    CheckinRecord r;
                    if (today.TryGetValue(a.Id, out r) && r != null)
                    {
                        if (r.Status == CheckinStatus.Success || r.Status == CheckinStatus.Already) done++;
                        else if (r.Status == CheckinStatus.Auth || r.Status == CheckinStatus.Failed) failed++;
                        else waiting++;
                    }
                    else waiting++;
                }

                Color color;
                string state;
                if (enabled == 0) { color = Color.FromArgb(0x6B, 0x72, 0x80); state = "无启用账号"; }
                else if (failed > 0) { color = Color.FromArgb(0xF8, 0x71, 0x71); state = "有失败/需授权"; }
                else if (done >= enabled) { color = Color.FromArgb(0x34, 0xD3, 0x99); state = "全部完成"; }
                else { color = Color.FromArgb(0xFB, 0xBF, 0x24); state = "进行中"; }

                Icon next = IconFactory.Create(color, 32);
                Icon old = _currentIcon;
                _tray.Icon = next;
                _currentIcon = next;
                if (old != null) old.Dispose();

                string text = "AiSign · 今日 " + done + "/" + enabled + " 完成 · " + state;
                if (text.Length > 63) text = text.Substring(0, 63);
                _tray.Text = text;
                if (_pauseItem != null) _pauseItem.Checked = !_app.Settings.AutoCheckin;
                if (_autostartItem != null) _autostartItem.Checked = AutostartService.IsEnabled();
            }
            catch { }
        }

        private void Balloon(string title, string text, ToolTipIcon icon)
        {
            try
            {
                if (_tray == null) return;
                _tray.BalloonTipTitle = title;
                _tray.BalloonTipText = text == null ? string.Empty : text;
                _tray.BalloonTipIcon = icon;
                _tray.ShowBalloonTip(3000);
            }
            catch { }
        }

        private sealed class DarkMenuColors : ProfessionalColorTable
        {
            public override Color MenuItemSelected { get { return Color.FromArgb(0x26, 0x2B, 0x36); } }
            public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(0x26, 0x2B, 0x36); } }
            public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(0x26, 0x2B, 0x36); } }
            public override Color MenuItemBorder { get { return Color.FromArgb(0x8B, 0x6D, 0xFF); } }
            public override Color MenuBorder { get { return Color.FromArgb(0x26, 0x2B, 0x36); } }
            public override Color ToolStripDropDownBackground { get { return Color.FromArgb(0x17, 0x1A, 0x21); } }
            public override Color ImageMarginGradientBegin { get { return Color.FromArgb(0x17, 0x1A, 0x21); } }
            public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(0x17, 0x1A, 0x21); } }
            public override Color ImageMarginGradientEnd { get { return Color.FromArgb(0x17, 0x1A, 0x21); } }
            public override Color SeparatorDark { get { return Color.FromArgb(0x26, 0x2B, 0x36); } }
            public override Color SeparatorLight { get { return Color.FromArgb(0x26, 0x2B, 0x36); } }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                _app.AccountsChanged -= OnChanged;
                _app.ResultsChanged -= OnChanged;
                _app.AccountCompleted -= OnCompleted;
            }
            catch { }
            try
            {
                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.Dispose();
                    _tray = null;
                }
                if (_currentIcon != null) { _currentIcon.Dispose(); _currentIcon = null; }
                if (_menu != null) { _menu.Dispose(); _menu = null; }
            }
            catch { }
        }
    }
}
