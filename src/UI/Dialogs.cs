using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using AiSign.Core;
using AiSign.Services;

namespace AiSign.UI
{
    internal class ThemedDialog : Window
    {
        protected readonly Grid BodyHost;

        public ThemedDialog(string title, double width, double height)
        {
            Title = title;
            Width = width;
            Height = height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
            SetResourceReference(Window.BackgroundProperty, "BgBrush");

            WindowChrome chrome = new WindowChrome();
            chrome.CaptionHeight = 42;
            chrome.ResizeBorderThickness = new Thickness(0);
            chrome.GlassFrameThickness = new Thickness(0);
            chrome.UseAeroCaptionButtons = false;
            WindowChrome.SetWindowChrome(this, chrome);

            Grid root = new Grid();
            root.SetResourceReference(Panel.BackgroundProperty, "BgBrush");
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Border header = new Border();
            header.BorderThickness = new Thickness(0, 0, 0, 1);
            header.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            header.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
            header.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };
            Grid hg = new Grid();
            hg.ColumnDefinitions.Add(new ColumnDefinition());
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            TextBlock t = UiKit.Text(title, 13, "TextBrush", FontWeights.SemiBold);
            t.Margin = new Thickness(16, 0, 0, 0);
            t.VerticalAlignment = VerticalAlignment.Center;
            hg.Children.Add(t);
            Button close = UiKit.Button(string.Empty, "BtnIcon", delegate { Close(); });
            close.Content = "\u2715";
            close.FontSize = 12;
            close.Margin = new Thickness(0, 0, 8, 0);
            close.VerticalAlignment = VerticalAlignment.Center;
            WindowChrome.SetIsHitTestVisibleInChrome(close, true);
            Grid.SetColumn(close, 1);
            hg.Children.Add(close);
            header.Child = hg;
            root.Children.Add(header);

            BodyHost = new Grid();
            BodyHost.Margin = new Thickness(20);
            Grid.SetRow(BodyHost, 1);
            root.Children.Add(BodyHost);
            Content = root;
        }

        internal void SetBody(UIElement body)
        {
            BodyHost.Children.Clear();
            BodyHost.Children.Add(body);
        }
    }

    internal static class ConfirmDialog
    {
        public static bool Show(Window owner, string title, string message)
        {
            ThemedDialog dlg = new ThemedDialog(title, 420, 210);
            if (owner != null) dlg.Owner = owner;

            StackPanel sp = new StackPanel();
            TextBlock msg = UiKit.Body(message);
            msg.TextWrapping = TextWrapping.Wrap;
            sp.Children.Add(msg);
            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 20, 0, 0);
            Button cancel = UiKit.Button("取消", "BtnGhost", delegate { dlg.DialogResult = false; });
            Button ok = UiKit.Button("确定", "BtnDanger", delegate { dlg.DialogResult = true; });
            cancel.Margin = new Thickness(0, 0, 8, 0);
            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);
            sp.Children.Add(buttons);
            dlg.SetBody(sp);
            return dlg.ShowDialog() == true;
        }
    }

    internal static class PromptDialog
    {
        public static string Show(Window owner, string title, string label, string initial)
        {
            ThemedDialog dlg = new ThemedDialog(title, 440, 230);
            if (owner != null) dlg.Owner = owner;

            StackPanel sp = new StackPanel();
            sp.Children.Add(UiKit.Muted(label));
            TextBox input = new TextBox();
            input.Style = (Style)Application.Current.Resources["Input"];
            input.Text = initial == null ? string.Empty : initial;
            input.Margin = new Thickness(0, 8, 0, 0);
            sp.Children.Add(input);
            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 18, 0, 0);
            Button cancel = UiKit.Button("取消", "BtnGhost", delegate { dlg.DialogResult = false; });
            Button ok = UiKit.Button("保存", "BtnPrimary", delegate { dlg.DialogResult = true; });
            cancel.Margin = new Thickness(0, 0, 8, 0);
            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);
            sp.Children.Add(buttons);
            dlg.SetBody(sp);
            input.Focus();
            input.SelectAll();
            bool okResult = dlg.ShowDialog() == true;
            return okResult ? input.Text : null;
        }
    }

    internal sealed class ScanItem
    {
        public AccountDraft Draft;
        public CheckBox Check;
    }

    internal sealed class AddAccountDialog : ThemedDialog
    {
        private readonly AppController _app;
        private string _platform;
        private string _mode;
        private bool _busy;
        private bool _oauthCancelled;

        private readonly Grid _content;
        private readonly TextBlock _status;
        private readonly StackPanel _platformRow;
        private readonly StackPanel _modeRow;
        private Button _addButton;

        private readonly List<ScanItem> _scanItems = new List<ScanItem>();

        public AddAccountDialog(Window owner, AppController app, string initialMode)
            : this(owner, app, initialMode, null)
        {
        }

        public AddAccountDialog(Window owner, AppController app, string initialMode, string initialPlatform)
            : base("添加账号", 760, 620)
        {
            _app = app;
            _platform = PlatformIds.Trae;
            if (initialPlatform == PlatformIds.Trae || initialPlatform == PlatformIds.WorkBuddy ||
                initialPlatform == PlatformIds.Qoder) _platform = initialPlatform;
            _mode = string.IsNullOrEmpty(initialMode) ? "scan" : initialMode;
            if (_mode != "scan" && _mode != "token" && _mode != "oauth") _mode = "scan";
            if (_platform == PlatformIds.Trae && _mode == "oauth") _mode = "scan";
            if (owner != null) Owner = owner;

            StackPanel body = new StackPanel();

            _platformRow = new StackPanel();
            _platformRow.Orientation = Orientation.Horizontal;
            body.Children.Add(_platformRow);
            body.Children.Add(UiKit.SeparatorLine());

            _modeRow = new StackPanel();
            _modeRow.Orientation = Orientation.Horizontal;
            _modeRow.Margin = new Thickness(0, 0, 0, 14);
            body.Children.Add(_modeRow);

            _content = new Grid();
            _content.MinHeight = 330;
            ScrollViewer contentScroll = new ScrollViewer();
            contentScroll.Height = 340;
            contentScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            contentScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            contentScroll.Content = _content;
            body.Children.Add(contentScroll);

            _status = UiKit.Muted(string.Empty);
            _status.Margin = new Thickness(0, 12, 0, 0);
            _status.TextWrapping = TextWrapping.Wrap;
            body.Children.Add(_status);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 12, 0, 0);
            Button cancel = UiKit.Button("取消", "BtnGhost", delegate { Close(); });
            _addButton = UiKit.Button("添加所选", "BtnPrimary", delegate { OnAdd(); });
            cancel.Margin = new Thickness(0, 0, 8, 0);
            buttons.Children.Add(cancel);
            buttons.Children.Add(_addButton);
            body.Children.Add(buttons);

            SetBody(body);
            RebuildSwitchers();
            RebuildContent();
            Closed += delegate { _oauthCancelled = true; };
        }

        private void RebuildSwitchers()
        {
            _platformRow.Children.Clear();
            string[] platforms = PlatformIds.All();
            for (int i = 0; i < platforms.Length; i++)
            {
                string p = platforms[i];
                Button b = UiKit.Button(PlatformIds.DisplayName(p), p == _platform ? "BtnGhostAccent" : "BtnGhost", delegate
                {
                    _platform = p;
                    if (p == PlatformIds.Trae && _mode == "oauth") _mode = "scan";
                    RebuildSwitchers();
                    RebuildContent();
                });
                b.Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0);
                b.Padding = new Thickness(14, 6, 14, 6);
                _platformRow.Children.Add(b);
            }

            _modeRow.Children.Clear();
            AddMode("扫描本机", "scan", true);
            AddMode("浏览器授权", "oauth", _platform == PlatformIds.WorkBuddy || _platform == PlatformIds.Qoder);
            AddMode("粘贴 Token", "token", true);
        }

        private void AddMode(string label, string mode, bool enabled)
        {
            Button b = UiKit.Button(label, mode == _mode ? "BtnGhostAccent" : "BtnGhost", delegate
            {
                _mode = mode;
                RebuildSwitchers();
                RebuildContent();
            });
            b.IsEnabled = enabled;
            b.Margin = new Thickness(_modeRow.Children.Count == 0 ? 0 : 8, 0, 0, 0);
            b.Padding = new Thickness(12, 5, 12, 5);
            b.FontSize = 12;
            if (!enabled) b.ToolTip = "Trae 不支持网页授权";
            _modeRow.Children.Add(b);
        }

        private void RebuildContent()
        {
            _content.Children.Clear();
            _scanItems.Clear();
            if (_mode == "oauth") BuildOAuth();
            else if (_mode == "token") BuildToken();
            else BuildScan();
        }

        private void BuildScan()
        {
            StackPanel sp = new StackPanel();
            TextBlock hint = UiKit.Muted(_platform == PlatformIds.Trae
                ? "读取本机 Trae 客户端的登录态（只读）。Trae 仅支持单账号，添加后会替换现有 Trae 账号。"
                : _platform == PlatformIds.WorkBuddy
                    ? "读取本机 WorkBuddy / CodeBuddy 的 auth 文件。5.6+ 客户端凭证已加密，需改用浏览器授权。"
                    : "读取本机 Qoder CN 客户端的 auth.v1.dat（只读）。");
            hint.TextWrapping = TextWrapping.Wrap;
            sp.Children.Add(hint);

            Button scan = UiKit.Button("开始扫描", "BtnPrimary", delegate { StartScan(); });
            scan.HorizontalAlignment = HorizontalAlignment.Left;
            scan.Margin = new Thickness(0, 12, 0, 12);
            sp.Children.Add(scan);

            StackPanel result = new StackPanel();
            result.Name = "ScanResults";
            sp.Children.Add(result);
            _content.Children.Add(sp);
            _addButton.Content = "添加所选";
            _addButton.IsEnabled = true;
        }

        private void StartScan()
        {
            if (_busy) return;
            _busy = true;
            _status.Text = "正在扫描本机登录态…";
            _addButton.IsEnabled = false;
            string platform = _platform;
            ThreadPool.QueueUserWorkItem(delegate(object ignored)
            {
                List<AccountDraft> found = null;
                string error = null;
                try { found = _app.ScanPlatform(platform); }
                catch (Exception ex) { error = ex.Message; }
                Application.Current.Dispatcher.BeginInvoke(new Action(delegate
                {
                    _busy = false;
                    _addButton.IsEnabled = true;
                    StackPanel host = FindScanHost();
                    if (host == null) return;
                    host.Children.Clear();
                    _scanItems.Clear();
                    if (error != null)
                    {
                        _status.Text = "扫描失败：" + error;
                        return;
                    }
                    if (found == null || found.Count == 0)
                    {
                        _status.Text = "未找到登录态。请先打开对应客户端登录一次，再回来扫描。";
                        return;
                    }
                    _status.Text = "找到 " + found.Count + " 个候选账号，请勾选要添加的账号。";
                    for (int i = 0; i < found.Count; i++)
                    {
                        AccountDraft d = found[i];
                        Border card = ScanRow(d);
                        host.Children.Add(card);
                    }
                }));
            });
        }

        private StackPanel FindScanHost()
        {
            if (_content.Children.Count == 0) return null;
            StackPanel sp = _content.Children[0] as StackPanel;
            if (sp == null) return null;
            for (int i = 0; i < sp.Children.Count; i++)
            {
                StackPanel child = sp.Children[i] as StackPanel;
                if (child != null && child.Name == "ScanResults") return child;
            }
            return null;
        }

        private Border ScanRow(AccountDraft d)
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            CheckBox cb = new CheckBox();
            cb.IsChecked = d.Secret != null;
            cb.IsEnabled = d.Secret != null;
            cb.VerticalAlignment = VerticalAlignment.Center;
            cb.Margin = new Thickness(0, 0, 10, 0);
            g.Children.Add(cb);
            StackPanel info = new StackPanel();
            info.Children.Add(UiKit.Text(string.IsNullOrEmpty(d.Name) ? "未命名账号" : d.Name, 12.5, "TextBrush", FontWeights.Medium));
            TextBlock sub = UiKit.Muted(UiKit.MaskUid(d.Uid) + (d.Secret == null ? string.Empty : " · " + SourceText(d.Source)));
            sub.Margin = new Thickness(0, 2, 0, 0);
            info.Children.Add(sub);
            if (!string.IsNullOrEmpty(d.Warning))
            {
                TextBlock w = UiKit.Text(d.Warning, 11, "WarningBrush", FontWeights.Normal);
                w.Margin = new Thickness(0, 4, 0, 0);
                w.TextWrapping = TextWrapping.Wrap;
                info.Children.Add(w);
            }
            Grid.SetColumn(info, 1);
            g.Children.Add(info);
            ScanItem item = new ScanItem();
            item.Draft = d;
            item.Check = cb;
            _scanItems.Add(item);
            Border card = UiKit.Panel(g);
            card.Margin = new Thickness(0, 0, 0, 8);
            return card;
        }

        private void BuildOAuth()
        {
            StackPanel sp = new StackPanel();
            string hintText = _platform == PlatformIds.Qoder
                ? "将打开无痕浏览器完成 Qoder 阿里云 SSO 登录；授权完成后本窗口自动接收凭证。每个 Qoder 账号执行一次。"
                : "将打开无痕浏览器完成腾讯 SSO 授权。每个 WorkBuddy 账号执行一次，多账号请重复添加。";
            TextBlock hint = UiKit.Muted(hintText);
            hint.TextWrapping = TextWrapping.Wrap;
            sp.Children.Add(hint);
            TextBlock step = UiKit.Body("1. 点击下方按钮打开浏览器\n2. 在浏览器中登录并确认授权\n3. 本窗口会自动接收结果");
            step.Margin = new Thickness(0, 12, 0, 12);
            sp.Children.Add(step);
            Button start = UiKit.Button("打开浏览器授权", "BtnPrimary", delegate { StartOAuth(); });
            start.HorizontalAlignment = HorizontalAlignment.Left;
            sp.Children.Add(start);
            TextBlock note = UiKit.Muted("若无痕窗口仍复用已登录账号，请先在浏览器中退出该账号，或使用另一浏览器。");
            note.Margin = new Thickness(0, 12, 0, 0);
            note.TextWrapping = TextWrapping.Wrap;
            sp.Children.Add(note);
            _content.Children.Add(sp);
            _addButton.Content = "等待授权…";
            _addButton.IsEnabled = false;
        }

        private void StartOAuth()
        {
            if (_busy) return;
            _busy = true;
            _oauthCancelled = false;
            _status.Text = "正在申请授权…";
            _addButton.IsEnabled = false;
            string platform = _platform;
            ThreadPool.QueueUserWorkItem(delegate(object ignored)
            {
                AccountDraft draft = null;
                string error = null;
                try
                {
                    draft = _app.RunOAuth(platform, delegate(string text, int progress)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(delegate { _status.Text = text; }));
                    }, delegate { return _oauthCancelled; });
                }
                catch (Exception ex) { error = ex.Message; }
                Application.Current.Dispatcher.BeginInvoke(new Action(delegate
                {
                    _busy = false;
                    _addButton.IsEnabled = true;
                    if (error != null)
                    {
                        _status.Text = "授权失败：" + error;
                        return;
                    }
                    if (draft != null)
                    {
                        string err;
                        Account a = _app.AddDraft(draft, out err);
                        if (a == null)
                        {
                            _status.Text = "保存失败：" + err;
                            return;
                        }
                        DialogResult = true;
                    }
                }));
            });
        }

        private void BuildToken()
        {
            StackPanel sp = new StackPanel();
            string hintText;
            if (_platform == PlatformIds.Trae) hintText = "粘贴 Trae 的 Cloud-IDE-JWT（可带前缀，也可粘贴 iCube 密文）。";
            else if (_platform == PlatformIds.WorkBuddy) hintText = "粘贴 WorkBuddy access token，或整段 *.info JSON。";
            else hintText = "粘贴 Qoder Access Token / PAT（pt-）/ Refresh Token，或账号 JSON。";
            TextBlock hint = UiKit.Muted(hintText);
            hint.TextWrapping = TextWrapping.Wrap;
            sp.Children.Add(hint);

            TextBox input = new TextBox();
            input.Style = (Style)Application.Current.Resources["Input"];
            input.Height = 160;
            input.AcceptsReturn = true;
            input.TextWrapping = TextWrapping.Wrap;
            input.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            input.Margin = new Thickness(0, 10, 0, 8);
            sp.Children.Add(input);

            StackPanel tools = new StackPanel();
            tools.Orientation = Orientation.Horizontal;
            Button paste = UiKit.Button("从剪贴板粘贴", "BtnGhost", delegate
            {
                try { if (Clipboard.ContainsText()) input.Text = Clipboard.GetText(); } catch { }
            });
            Button submit = UiKit.Button("验证并添加", "BtnPrimary", delegate { ValidateAndAdd(input.Text); });
            paste.Margin = new Thickness(0, 0, 8, 0);
            tools.Children.Add(paste);
            tools.Children.Add(submit);
            sp.Children.Add(tools);
            _content.Children.Add(sp);
            _addButton.Content = "验证并添加";
            _addButton.IsEnabled = false;
            _status.Text = "粘贴后点击「验证并添加」，会先调用平台接口验证凭证。";
        }

        private void ValidateAndAdd(string raw)
        {
            if (_busy) return;
            if (string.IsNullOrEmpty(raw) || raw.Trim().Length == 0)
            {
                _status.Text = "请输入 Token";
                return;
            }
            _busy = true;
            _status.Text = "正在验证…";
            string platform = _platform;
            string text = raw.Trim();
            ThreadPool.QueueUserWorkItem(delegate(object ignored)
            {
                AccountDraft draft = null;
                string error = null;
                try { draft = _app.ValidatePasted(platform, text); }
                catch (Exception ex) { error = ex.Message; }
                Application.Current.Dispatcher.BeginInvoke(new Action(delegate
                {
                    _busy = false;
                    if (error != null)
                    {
                        _status.Text = "验证失败：" + error;
                        return;
                    }
                    string err;
                    Account a = _app.AddDraft(draft, out err);
                    if (a == null)
                    {
                        _status.Text = "保存失败：" + err;
                        return;
                    }
                    DialogResult = true;
                }));
            });
        }

        private void OnAdd()
        {
            if (_mode == "token")
            {
                // 粘贴页由“验证并添加”按钮处理
                return;
            }
            if (_mode == "oauth") return;
            if (_scanItems.Count == 0)
            {
                _status.Text = "请先扫描并选择账号";
                return;
            }
            int added = 0;
            List<AccountDraft> chosen = new List<AccountDraft>();
            for (int i = 0; i < _scanItems.Count; i++)
            {
                ScanItem it = _scanItems[i];
                if (it.Check.IsChecked == true && it.Draft.Secret != null) chosen.Add(it.Draft);
            }
            if (chosen.Count == 0)
            {
                _status.Text = "请至少勾选一个账号";
                return;
            }
            if (_platform == PlatformIds.Trae && chosen.Count > 1)
                chosen.RemoveRange(1, chosen.Count - 1);

            for (int i = 0; i < chosen.Count; i++)
            {
                string err;
                Account a = _app.AddDraft(chosen[i], out err);
                if (a != null) added++;
                else _status.Text = "添加失败：" + err;
            }
            if (added > 0)
            {
                DialogResult = true;
            }
        }

        private static string SourceText(string source)
        {
            if (source == "oauth") return "浏览器授权";
            if (source == "token") return "粘贴";
            return "本机扫描";
        }
    }
}
