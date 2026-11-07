using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using AiSign.Core;
using AiSign.Services;

namespace AiSign.UI
{
    internal sealed class NavItem
    {
        public string Key;
        public Button Button;
        public TextBlock Label;
        public Path Icon;
    }

    internal sealed class MainWindow : Window
    {
        private readonly AppHost _host;
        private readonly AppController _controller;
        private readonly List<NavItem> _nav = new List<NavItem>();

        private Grid _contentHost;
        private TextBlock _statusText;
        private Border _statusPill;
        private string _current = "accounts";
        private bool _forceClose;
        private ToggleSwitch _autoStartToggle;
        private bool _syncingToggle;
        private AccountsPage _accountsPage;
        private CheckinPage _checkinPage;
        private SettingsPage _settingsPage;

        public MainWindow(AppHost host, AppController controller)
        {
            _host = host;
            _controller = controller;

            Title = "AiSign · AI 签到助手";
            Width = 1080;
            Height = 720;
            MinWidth = 900;
            MinHeight = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            try
            {
                if (controller.Settings.Window.W >= 900) Width = controller.Settings.Window.W;
                if (controller.Settings.Window.H >= 600) Height = controller.Settings.Window.H;
            }
            catch { }
            SetResourceReference(Window.BackgroundProperty, "BgBrush");
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            WindowChrome chrome = new WindowChrome();
            chrome.CaptionHeight = 48;
            chrome.ResizeBorderThickness = new Thickness(6);
            chrome.GlassFrameThickness = new Thickness(0);
            chrome.UseAeroCaptionButtons = false;
            WindowChrome.SetWindowChrome(this, chrome);

            Content = BuildShell();
            _controller.AccountsChanged += OnDataChanged;
            _controller.ResultsChanged += OnDataChanged;
            _controller.StatusText += OnStatus;
            Closing += OnClosing;
            RefreshChrome();
            string last = "accounts";
            try { last = _controller.Settings.Window.LastPage; } catch { }
            NavigateTo(last);
        }

        public void ForceClose()
        {
            _forceClose = true;
        }

        public void NavigateTo(string key)
        {
            if (string.IsNullOrEmpty(key)) key = "accounts";
            _current = key;
            _contentHost.Children.Clear();
            UIElement page;
            if (key == "checkin")
            {
                if (_checkinPage == null) _checkinPage = new CheckinPage(this, _controller);
                else _checkinPage.Refresh();
                page = _checkinPage.Root;
            }
            else if (key == "settings")
            {
                if (_settingsPage == null) _settingsPage = new SettingsPage(this, _controller);
                else _settingsPage.Refresh();
                page = _settingsPage.Root;
            }
            else
            {
                if (_accountsPage == null) _accountsPage = new AccountsPage(this, _controller);
                else _accountsPage.Refresh();
                page = _accountsPage.Root;
            }
            _contentHost.Children.Add(page);
            UpdateNav();
            RefreshChrome();
        }

        public void RefreshChrome()
        {
            try
            {
                List<Account> accounts = _controller.Accounts.All();
                Dictionary<string, CheckinRecord> today = _controller.Today();
                int enabled = 0;
                int done = 0;
                for (int i = 0; i < accounts.Count; i++)
                {
                    if (!accounts[i].Enabled) continue;
                    enabled++;
                    CheckinRecord r;
                    if (today.TryGetValue(accounts[i].Id, out r) && r != null &&
                        (r.Status == CheckinStatus.Success || r.Status == CheckinStatus.Already)) done++;
                }
                if (_statusText != null)
                {
                    _statusText.Text = enabled == 0 ? "暂无启用账号" : "今日 " + done + "/" + enabled + " 已完成";
                }
                if (_autoStartToggle != null)
                {
                    _syncingToggle = true;
                    _autoStartToggle.IsChecked = AutostartService.IsEnabled();
                    _syncingToggle = false;
                }
            }
            catch { }
        }

        private UIElement BuildShell()
        {
            Grid root = new Grid();
            root.SetResourceReference(Panel.BackgroundProperty, "BgBrush");
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            UIElement titleBar = BuildTitleBar();
            root.Children.Add(titleBar);

            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(208) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(body, 1);
            root.Children.Add(body);

            body.Children.Add(BuildSidebar());

            _contentHost = new Grid();
            _contentHost.Background = Brushes.Transparent;
            Grid.SetColumn(_contentHost, 1);
            body.Children.Add(_contentHost);

            return root;
        }

        private UIElement BuildTitleBar()
        {
            Border bar = new Border();
            bar.SetResourceReference(Border.BackgroundProperty, "BgBrush");
            bar.BorderThickness = new Thickness(0, 0, 0, 1);
            bar.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            bar.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2) ToggleMaximize();
                else if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            Border logo = new Border();
            logo.Width = 26;
            logo.Height = 26;
            logo.CornerRadius = new CornerRadius(8);
            logo.Margin = new Thickness(16, 0, 10, 0);
            logo.VerticalAlignment = VerticalAlignment.Center;
            logo.SetResourceReference(Border.BackgroundProperty, "AccentSoftBrush");
            TextBlock letter = new TextBlock();
            letter.Text = "A";
            letter.FontSize = 15;
            letter.FontWeight = FontWeights.Bold;
            letter.HorizontalAlignment = HorizontalAlignment.Center;
            letter.VerticalAlignment = VerticalAlignment.Center;
            letter.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            logo.Child = letter;
            g.Children.Add(logo);

            StackPanel name = new StackPanel();
            name.VerticalAlignment = VerticalAlignment.Center;
            TextBlock app = new TextBlock();
            app.Text = "AiSign";
            app.FontSize = 14;
            app.FontWeight = FontWeights.SemiBold;
            app.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            TextBlock sub = new TextBlock();
            sub.Text = "AI 签到助手";
            sub.FontSize = 10;
            sub.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            name.Children.Add(app);
            name.Children.Add(sub);
            Grid.SetColumn(name, 1);
            g.Children.Add(name);

            _statusPill = new Border();
            _statusPill.CornerRadius = new CornerRadius(20);
            _statusPill.Padding = new Thickness(10, 3, 10, 3);
            _statusPill.Margin = new Thickness(18, 0, 0, 0);
            _statusPill.VerticalAlignment = VerticalAlignment.Center;
            _statusPill.SetResourceReference(Border.BackgroundProperty, "SurfaceAltBrush");
            _statusText = new TextBlock();
            _statusText.FontSize = 11;
            _statusText.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            _statusPill.Child = _statusText;
            Grid.SetColumn(_statusPill, 2);
            _statusPill.HorizontalAlignment = HorizontalAlignment.Left;
            g.Children.Add(_statusPill);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.VerticalAlignment = VerticalAlignment.Center;
            buttons.Margin = new Thickness(0, 0, 6, 0);

            Button min = MakeCaptionButton("\u2014");
            min.Click += delegate { WindowState = WindowState.Minimized; };
            Button max = MakeCaptionButton("\u25A1");
            max.Click += delegate { ToggleMaximize(); };
            Button close = MakeCaptionButton("\u2715");
            close.Click += delegate { Close(); };
            close.MouseEnter += delegate { close.SetResourceReference(Control.BackgroundProperty, "DangerSoftBrush"); };
            close.MouseLeave += delegate { close.SetResourceReference(Control.BackgroundProperty, "TransparentBrush"); };
            buttons.Children.Add(min);
            buttons.Children.Add(max);
            buttons.Children.Add(close);
            Grid.SetColumn(buttons, 3);
            g.Children.Add(buttons);

            bar.Child = g;
            return bar;
        }

        private Button MakeCaptionButton(string glyph)
        {
            Button b = UiKit.Button(string.Empty, "BtnIcon", null);
            TextBlock t = new TextBlock();
            t.Text = glyph;
            t.FontSize = 12;
            t.FontFamily = new FontFamily("Segoe UI Symbol");
            t.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            b.Content = t;
            b.Width = 40;
            b.Height = 30;
            b.Margin = new Thickness(2, 0, 2, 0);
            b.VerticalAlignment = VerticalAlignment.Center;
            WindowChrome.SetIsHitTestVisibleInChrome(b, true);
            return b;
        }

        private UIElement BuildSidebar()
        {
            Border side = new Border();
            side.SetResourceReference(Border.BackgroundProperty, "SidebarBrush");
            side.BorderThickness = new Thickness(0, 0, 1, 0);
            side.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

            Grid g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            StackPanel nav = new StackPanel();
            nav.Margin = new Thickness(10, 16, 10, 0);
            AddNav(nav, "accounts", "账号管理", "M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zm0 2c-4.4 0-8 2.2-8 5v1h16v-1c0-2.8-3.6-5-8-5z");
            AddNav(nav, "checkin", "签到记录", "M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2zm2 4v2h10V7H7zm0 4v2h10v-2H7zm0 4v2h6v-2H7z");
            AddNav(nav, "settings", "设置", "M19.4 13c.04-.32.07-.64.07-.98s-.03-.66-.07-.98l2.1-1.65c.2-.15.25-.42.13-.64l-2-3.46c-.12-.22-.39-.3-.61-.22l-2.5 1a7.6 7.6 0 0 0-1.7-.98L14.5 2.4c-.03-.24-.24-.42-.49-.42h-4c-.25 0-.46.18-.49.42l-.38 2.67c-.61.25-1.17.58-1.69.98l-2.49-1c-.22-.08-.49 0-.61.22l-2 3.46c-.13.22-.07.49.12.64L4.6 11c-.04.32-.07.65-.07.98s.03.66.07.98l-2.11 1.65c-.19.15-.24.42-.12.64l2 3.46c.12.22.39.3.61.22l2.49-1c.52.4 1.08.73 1.69.98l.38 2.67c.03.24.24.42.49.42h4c.25 0 .46-.18.49-.42l.38-2.67c.61-.25 1.17-.58 1.69-.98l2.49 1c.22.08.49 0 .61-.22l2-3.46c.12-.22.07-.49-.12-.64L19.4 13zM12 15.5A3.5 3.5 0 1 1 12 8a3.5 3.5 0 0 1 0 7.5z");
            g.Children.Add(nav);

            StackPanel bottom = new StackPanel();
            bottom.Margin = new Thickness(14, 10, 14, 14);
            Border sep = new Border();
            sep.Height = 1;
            sep.Margin = new Thickness(0, 0, 0, 12);
            sep.SetResourceReference(Border.BackgroundProperty, "BorderBrush");
            bottom.Children.Add(sep);

            Grid auto = new Grid();
            auto.ColumnDefinitions.Add(new ColumnDefinition());
            auto.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            TextBlock label = new TextBlock();
            label.Text = "开机自启";
            label.FontSize = 12;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            auto.Children.Add(label);
            _autoStartToggle = new ToggleSwitch();
            _autoStartToggle.Checked += delegate { OnAutoStartChanged(); };
            _autoStartToggle.Unchecked += delegate { OnAutoStartChanged(); };
            Grid.SetColumn(_autoStartToggle, 1);
            auto.Children.Add(_autoStartToggle);
            bottom.Children.Add(auto);

            TextBlock version = new TextBlock();
            version.Text = "AiSign v1.0.0";
            version.FontSize = 10;
            version.Margin = new Thickness(0, 10, 0, 0);
            version.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            bottom.Children.Add(version);

            Grid.SetRow(bottom, 1);
            g.Children.Add(bottom);
            side.Child = g;
            return side;
        }

        private void AddNav(StackPanel parent, string key, string text, string geometry)
        {
            Button b = UiKit.Button(string.Empty, "BtnGhost", delegate { NavigateTo(key); });
            b.Height = 40;
            b.Padding = new Thickness(12, 0, 12, 0);
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.Margin = new Thickness(0, 2, 0, 2);
            b.SetResourceReference(Control.BackgroundProperty, "TransparentBrush");

            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.VerticalAlignment = VerticalAlignment.Center;
            Path icon = (Path)UiKit.Icon(geometry, "MutedBrush", 17);
            icon.Margin = new Thickness(0, 0, 11, 0);
            TextBlock label = new TextBlock();
            label.Text = text;
            label.FontSize = 13;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            row.Children.Add(icon);
            row.Children.Add(label);
            b.Content = row;

            NavItem item = new NavItem();
            item.Key = key;
            item.Button = b;
            item.Label = label;
            item.Icon = icon;
            _nav.Add(item);
            parent.Children.Add(b);
        }

        private void UpdateNav()
        {
            for (int i = 0; i < _nav.Count; i++)
            {
                NavItem n = _nav[i];
                bool active = n.Key == _current;
                n.Button.SetResourceReference(Control.BackgroundProperty, active ? "AccentSoftBrush" : "TransparentBrush");
                n.Label.SetResourceReference(TextBlock.ForegroundProperty, active ? "AccentBrush" : "MutedBrush");
                n.Icon.SetResourceReference(Shape.FillProperty, active ? "AccentBrush" : "MutedBrush");
            }
        }

        private void OnAutoStartChanged()
        {
            if (_syncingToggle) return;
            bool enabled = _autoStartToggle.IsChecked == true;
            AutostartService.SetEnabled(enabled);
            _controller.Settings.LaunchAtLogin = enabled;
            _controller.SaveSettings();
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void OnDataChanged()
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                RefreshChrome();
                if (_current == "checkin" && _checkinPage != null) _checkinPage.Refresh();
            }));
        }

        private void OnStatus(string text)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (_statusText != null && !string.IsNullOrEmpty(text)) _statusText.Text = text;
            }));
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            try
            {
                _controller.Settings.Window.W = Width;
                _controller.Settings.Window.H = Height;
                _controller.Settings.Window.LastPage = _current;
                _controller.SaveSettings();
            }
            catch { }

            if (_forceClose) return;
            if (_controller.Settings.CloseToTray)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                _forceClose = true;
                Dispatcher.BeginInvoke(new Action(delegate { _host.Exit(); }));
            }
        }
    }
}
