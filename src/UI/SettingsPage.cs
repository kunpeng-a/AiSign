using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AiSign.Core;
using AiSign.Services;

namespace AiSign.UI
{
    internal sealed class SettingsPage
    {
        private readonly MainWindow _win;
        private readonly AppController _app;
        private readonly StackPanel _content;
        private bool _rebuilding;

        public UIElement Root { get; private set; }

        public SettingsPage(MainWindow win, AppController app)
        {
            _win = win;
            _app = app;

            _content = new StackPanel();
            _content.Margin = new Thickness(24);

            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.Content = _content;
            Root = sv;
            Rebuild();
        }

        public void Refresh()
        {
            Rebuild();
        }

        private void Rebuild()
        {
            if (_rebuilding) return;
            _rebuilding = true;
            try
            {
                _content.Children.Clear();
                Settings s = _app.Settings;
                _content.Children.Add(UiKit.PageHeader("设置", "外观、启动、签到与通知", null));

                _content.Children.Add(Section("外观", "主题会立即生效",
                    LabelRow("主题"),
                    Segmented(new string[] { "深色", "浅色", "跟随系统" },
                        s.Theme == "light" ? 1 : (s.Theme == "system" ? 2 : 0),
                        delegate(int idx)
                        {
                            s.Theme = idx == 1 ? "light" : (idx == 2 ? "system" : "dark");
                            _app.SaveSettings();
                            Theme.Apply(s.Theme);
                            Rebuild();
                        })));

                _content.Children.Add(Section("启动与托盘", "开机自启使用 HKCU Run，无需管理员权限",
                    ToggleRow("开机自动启动", "开机后以最小化方式运行", AutostartService.IsEnabled(), delegate(bool on)
                    {
                        AutostartService.SetEnabled(on);
                        s.LaunchAtLogin = on;
                        _app.SaveSettings();
                        _win.RefreshChrome();
                    }),
                    ToggleRow("启动时最小化到托盘", "仅影响自启启动，手动双击仍显示窗口", s.StartMinimized, delegate(bool on)
                    {
                        s.StartMinimized = on;
                        _app.SaveSettings();
                    }),
                    ToggleRow("关闭窗口时最小化到托盘", "关闭按钮不退出程序", s.CloseToTray, delegate(bool on)
                    {
                        s.CloseToTray = on;
                        _app.SaveSettings();
                    })));

                _content.Children.Add(Section("签到", "每个账号独立执行，失败自动退避重试",
                    ToggleRow("自动签到", "关闭后仅保留手动签到", s.AutoCheckin, delegate(bool on)
                    {
                        s.AutoCheckin = on;
                        _app.SaveSettings();
                    }),
                    PlatformRow(PlatformIds.Trae, s),
                    PlatformRow(PlatformIds.WorkBuddy, s),
                    PlatformRow(PlatformIds.Qoder, s)));

                _content.Children.Add(Section("通知", "托盘气泡与应用内状态提示",
                    LabelRow("通知内容"),
                    Segmented(new string[] { "全部", "仅失败", "不通知" },
                        s.Notify.Mode == "failures" ? 1 : (s.Notify.Mode == "none" ? 2 : 0),
                        delegate(int idx)
                        {
                            s.Notify.Mode = idx == 1 ? "failures" : (idx == 2 ? "none" : "all");
                            _app.SaveSettings();
                            Rebuild();
                        }),
                    ToggleRow("托盘气泡", "窗口隐藏时通过系统通知提示结果", s.Notify.Balloon, delegate(bool on)
                    {
                        s.Notify.Balloon = on;
                        _app.SaveSettings();
                    })));

                _content.Children.Add(Section("数据", "所有数据均保存在本机，不上传服务器",
                    LabelRow("数据目录"),
                    ValueRow(AppPaths.DataDir),
                    ButtonRow(
                        UiKit.Button("打开数据目录", "BtnGhost", delegate { OpenDataDir(); }),
                        UiKit.Button("清空签到历史", "BtnDanger", delegate { ClearHistory(); }))));

                _content.Children.Add(Section("关于", "AiSign v1.0.0 · .NET Framework 4.8",
                    UiKit.Body("支持 Trae CN（单账号）、WorkBuddy / CodeBuddy（多账号）、Qoder CN（多账号）。"),
                    UiKit.Muted("本工具仅供学习研究，与各平台官方无任何关联。自动化签到可能违反平台服务条款，账号风险由使用者自行承担。"),
                    UiKit.Muted("签到算法参考 TraeSign（MIT）；多账号与凭证隔离设计参考 AI Work 助手（MIT）。")));
            }
            finally
            {
                _rebuilding = false;
            }
        }

        private Border Section(string title, string subtitle, params UIElement[] children)
        {
            StackPanel sp = new StackPanel();
            TextBlock t = UiKit.Section(title);
            sp.Children.Add(t);
            if (!string.IsNullOrEmpty(subtitle))
            {
                TextBlock sub = UiKit.Muted(subtitle);
                sub.Margin = new Thickness(0, 3, 0, 12);
                sp.Children.Add(sub);
            }
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] == null) continue;
                FrameworkElement fe = children[i] as FrameworkElement;
                if (fe != null) fe.Margin = new Thickness(0, i == 0 ? 0 : 10, 0, 0);
                sp.Children.Add(children[i]);
            }
            Border card = UiKit.Card(sp);
            card.Margin = new Thickness(0, 0, 0, 12);
            return card;
        }

        private UIElement LabelRow(string text)
        {
            return UiKit.Text(text, 12.5, "TextBrush", FontWeights.Medium);
        }

        private UIElement ValueRow(string text)
        {
            TextBlock t = UiKit.Muted(text);
            t.TextWrapping = TextWrapping.Wrap;
            t.Margin = new Thickness(0, 2, 0, 0);
            return t;
        }

        private UIElement ButtonRow(params UIElement[] items)
        {
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                FrameworkElement fe = items[i] as FrameworkElement;
                if (fe != null && i > 0) fe.Margin = new Thickness(8, 0, 0, 0);
                sp.Children.Add(items[i]);
            }
            return sp;
        }

        private UIElement Segmented(string[] options, int active, Action<int> changed)
        {
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                Button b = UiKit.Button(options[i], idx == active ? "BtnGhostAccent" : "BtnGhost", delegate { changed(idx); });
                b.Margin = new Thickness(idx == 0 ? 0 : 6, 0, 0, 0);
                b.Padding = new Thickness(12, 5, 12, 5);
                b.FontSize = 12;
                sp.Children.Add(b);
            }
            return sp;
        }

        private UIElement ToggleRow(string title, string desc, bool value, Action<bool> changed)
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            StackPanel left = new StackPanel();
            left.Children.Add(UiKit.Text(title, 12.5, "TextBrush", FontWeights.Medium));
            if (!string.IsNullOrEmpty(desc))
            {
                TextBlock d = UiKit.Muted(desc);
                d.Margin = new Thickness(0, 2, 0, 0);
                left.Children.Add(d);
            }
            g.Children.Add(left);
            ToggleSwitch tg = new ToggleSwitch();
            tg.IsChecked = value;
            tg.VerticalAlignment = VerticalAlignment.Center;
            tg.Checked += delegate { changed(true); };
            tg.Unchecked += delegate { changed(false); };
            Grid.SetColumn(tg, 1);
            g.Children.Add(tg);
            return g;
        }

        private UIElement PlatformRow(string platform, Settings s)
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            ToggleSwitch tg = new ToggleSwitch();
            tg.IsChecked = s.Platform(platform).Enabled;
            tg.VerticalAlignment = VerticalAlignment.Center;
            tg.Checked += delegate { _app.SetPlatformEnabled(platform, true); };
            tg.Unchecked += delegate { _app.SetPlatformEnabled(platform, false); };
            g.Children.Add(tg);

            StackPanel info = new StackPanel();
            info.Margin = new Thickness(10, 0, 0, 0);
            info.VerticalAlignment = VerticalAlignment.Center;
            info.Children.Add(UiKit.Text(PlatformIds.DisplayName(platform), 12.5, "TextBrush", FontWeights.Medium));
            TextBlock sub = UiKit.Muted(platform == PlatformIds.Trae ? "单账号 · 每日签到" : "多账号 · 每日签到");
            sub.Margin = new Thickness(0, 2, 0, 0);
            info.Children.Add(sub);
            Grid.SetColumn(info, 1);
            g.Children.Add(info);

            TextBlock label = UiKit.Muted("时间");
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Margin = new Thickness(0, 0, 6, 0);
            Grid.SetColumn(label, 2);
            g.Children.Add(label);

            TextBox time = new TextBox();
            time.Style = (Style)Application.Current.Resources["Input"];
            time.Width = 78;
            time.Text = s.Platform(platform).Target;
            time.TextAlignment = TextAlignment.Center;
            time.VerticalAlignment = VerticalAlignment.Center;
            time.LostFocus += delegate
            {
                string v = time.Text == null ? string.Empty : time.Text.Trim();
                TimeSpan ts;
                if (!TimeSpan.TryParseExact(v, "hh\\:mm", CultureInfo.InvariantCulture, out ts))
                {
                    time.Text = _app.Settings.Platform(platform).Target;
                    return;
                }
                _app.SetPlatformTarget(platform, v);
            };
            time.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) { time.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); }
            };
            Grid.SetColumn(time, 3);
            g.Children.Add(time);
            return g;
        }

        private void OpenDataDir()
        {
            try { Process.Start("explorer.exe", AppPaths.DataDir); } catch { }
        }

        private void ClearHistory()
        {
            if (!ConfirmDialog.Show(_win, "清空签到历史", "确定清空全部签到记录？此操作不可恢复。")) return;
            _app.Results.Clear();
            Rebuild();
        }
    }
}
