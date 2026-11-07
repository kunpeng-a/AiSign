using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using AiSign.Core;
using AiSign.Services;

namespace AiSign.UI
{
    internal sealed class CheckinPage
    {
        private readonly Window _owner;
        private readonly AppController _app;
        private readonly StackPanel _body;
        private readonly TextBlock _footer;
        private bool _rebuilding;

        public UIElement Root { get; private set; }

        public CheckinPage(Window owner, AppController app)
        {
            _owner = owner;
            _app = app;

            StackPanel content = new StackPanel();
            content.Margin = new Thickness(24);

            Button run = UiKit.Button("立即签到", "BtnPrimary", delegate { RunNow(); });
            Button refresh = UiKit.Button("刷新", "BtnGhost", delegate { Rebuild(); });
            run.Margin = new Thickness(0, 0, 8, 0);
            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.Children.Add(run);
            actions.Children.Add(refresh);
            content.Children.Add(UiKit.PageHeader("签到记录", "今天的执行结果与最近 7 天状态", actions));

            _body = new StackPanel();
            content.Children.Add(_body);

            _footer = UiKit.Muted(string.Empty);
            _footer.Margin = new Thickness(2, 12, 0, 0);
            content.Children.Add(_footer);

            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.Content = content;
            Root = sv;

            _app.AccountsChanged += OnChanged;
            _app.ResultsChanged += OnChanged;
            _app.StatusText += OnStatus;
            Rebuild();
        }

        private void OnChanged()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(Rebuild));
        }

        private void OnStatus(string text)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(delegate
            {
                if (_footer != null) _footer.Text = text;
            }));
        }

        private void RunNow()
        {
            _app.RunNow(false, null, null, delegate(int total, int ok)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(delegate
                {
                    _footer.Text = "本轮完成 " + ok + "/" + total;
                    Rebuild();
                }));
            });
            _footer.Text = "已开始签到…";
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
                _body.Children.Clear();
                List<Account> accounts = _app.Accounts.All();
                Dictionary<string, CheckinRecord> today = _app.Today();
                int enabled = 0;
                int done = 0;
                int waiting = 0;
                int failed = 0;
                for (int i = 0; i < accounts.Count; i++)
                {
                    if (!accounts[i].Enabled) continue;
                    enabled++;
                    CheckinRecord r;
                    if (today.TryGetValue(accounts[i].Id, out r) && r != null)
                    {
                        if (r.Status == CheckinStatus.Success || r.Status == CheckinStatus.Already) done++;
                        else if (r.Status == CheckinStatus.Auth || r.Status == CheckinStatus.Failed ||
                                 r.Status == CheckinStatus.Occupied) failed++;
                        else waiting++;
                    }
                    else waiting++;
                }

                UniformGrid stats = new UniformGrid();
                stats.Columns = 4;
                stats.Margin = new Thickness(0, 0, 0, 16);
                stats.Children.Add(StatCard("今日完成", done + " / " + enabled, "成功或今日已签", "AccentBrush", 0));
                stats.Children.Add(StatCard("待处理", waiting.ToString(), "等待目标时刻或重试", "WarningBrush", 10));
                stats.Children.Add(StatCard("异常", failed.ToString(), "需授权 / 失败 / 设备占用", "DangerBrush", 10));
                stats.Children.Add(StatCard("下次执行", NextRunText(accounts), "按平台目标时刻自动执行", "MutedBrush", 10));
                _body.Children.Add(stats);

                StackPanel toolbar = new StackPanel();
                toolbar.Orientation = Orientation.Horizontal;
                toolbar.Margin = new Thickness(0, 0, 0, 16);
                Button pause = UiKit.Button(_app.Settings.AutoCheckin ? "暂停自动签到" : "恢复自动签到", "BtnSubtle", delegate
                {
                    _app.SetAutoCheckin(!_app.Settings.AutoCheckin);
                    Rebuild();
                });
                pause.Margin = new Thickness(0, 0, 8, 0);
                Button retry = UiKit.Button("立即预检（不领取）", "BtnSubtle", delegate
                {
                    _app.RunNow(true, null, null, delegate(int total, int ok)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(delegate
                        {
                            _footer.Text = "预检完成 " + ok + "/" + total;
                            Rebuild();
                        }));
                    });
                    _footer.Text = "正在预检…";
                });
                toolbar.Children.Add(pause);
                toolbar.Children.Add(retry);
                _body.Children.Add(toolbar);

                if (accounts.Count == 0)
                {
                    TextBlock empty = UiKit.Muted("还没有账号，请先到「账号管理」添加");
                    empty.Margin = new Thickness(4, 20, 0, 20);
                    _body.Children.Add(empty);
                    return;
                }

                string[] platforms = PlatformIds.All();
                for (int i = 0; i < platforms.Length; i++)
                {
                    List<Account> subset = new List<Account>();
                    for (int j = 0; j < accounts.Count; j++)
                        if (accounts[j].Platform == platforms[i]) subset.Add(accounts[j]);
                    if (subset.Count == 0) continue;
                    _body.Children.Add(PlatformCard(platforms[i], subset, today));
                }
            }
            finally
            {
                _rebuilding = false;
            }
        }

        private UIElement PlatformCard(string platform, List<Account> accounts, Dictionary<string, CheckinRecord> today)
        {
            StackPanel rows = new StackPanel();
            int done = 0;
            for (int i = 0; i < accounts.Count; i++)
            {
                CheckinRecord r;
                today.TryGetValue(accounts[i].Id, out r);
                if (r != null && (r.Status == CheckinStatus.Success || r.Status == CheckinStatus.Already)) done++;
                if (i > 0) rows.Children.Add(UiKit.SeparatorLine());
                rows.Children.Add(AccountRow(accounts[i], r));
            }

            StackPanel head = new StackPanel();
            head.Orientation = Orientation.Horizontal;
            Ellipse dot = UiKit.Dot(UiKit.PlatformBrush(platform), 8);
            dot.Margin = new Thickness(0, 0, 8, 0);
            TextBlock name = UiKit.Section(PlatformIds.DisplayName(platform));
            name.VerticalAlignment = VerticalAlignment.Center;
            PlatformSetting p = _app.Settings.Platform(platform);
            TextBlock time = UiKit.Muted("每日 " + (p == null ? "--" : p.Target));
            time.Margin = new Thickness(12, 0, 0, 0);
            time.VerticalAlignment = VerticalAlignment.Center;
            TextBlock summary = UiKit.Muted(done + "/" + accounts.Count + " 完成");
            summary.Margin = new Thickness(12, 0, 0, 0);
            summary.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(dot);
            head.Children.Add(name);
            head.Children.Add(time);
            head.Children.Add(summary);

            StackPanel body = new StackPanel();
            body.Margin = new Thickness(0, 12, 0, 0);
            body.Children.Add(head);
            body.Children.Add(rows);
            Border card = UiKit.Card(body);
            card.Margin = new Thickness(0, 0, 0, 12);
            return card;
        }

        private UIElement AccountRow(Account a, CheckinRecord r)
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            Border avatar = UiKit.Avatar(a.Name, UiKit.PlatformSoft(a.Platform), UiKit.PlatformBrush(a.Platform));
            avatar.Width = 30;
            avatar.Height = 30;
            avatar.CornerRadius = new CornerRadius(15);
            avatar.Margin = new Thickness(0, 2, 10, 2);
            g.Children.Add(avatar);

            StackPanel info = new StackPanel();
            info.VerticalAlignment = VerticalAlignment.Center;
            TextBlock name = UiKit.Text(a.Name + (a.Enabled ? string.Empty : "（已停用）"), 12.5, "TextBrush", FontWeights.Medium);
            TextBlock msg = UiKit.Muted(r == null ? "今日尚未执行" : (string.IsNullOrEmpty(r.Message) ? StatusText(r.Status) : r.Message));
            msg.Margin = new Thickness(0, 2, 0, 0);
            info.Children.Add(name);
            info.Children.Add(msg);
            Grid.SetColumn(info, 1);
            g.Children.Add(info);

            if (r != null && r.Credits > 0)
            {
                TextBlock credits = UiKit.Text("+" + r.Credits, 12, "SuccessBrush", FontWeights.SemiBold);
                credits.VerticalAlignment = VerticalAlignment.Center;
                credits.Margin = new Thickness(0, 0, 12, 0);
                Grid.SetColumn(credits, 2);
                g.Children.Add(credits);
            }

            Border status = UiKit.RecordPill(r);
            status.VerticalAlignment = VerticalAlignment.Center;
            status.Margin = new Thickness(0, 0, 12, 0);
            Grid.SetColumn(status, 3);
            g.Children.Add(status);

            g.Children.Add(WeekStrip(a, 4));
            Grid.SetColumn(g.Children[g.Children.Count - 1], 4);
            return g;
        }

        private UIElement WeekStrip(Account a, int column)
        {
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.VerticalAlignment = VerticalAlignment.Center;
            List<CheckinRecord> records = _app.LastDays(a.Id, 7);
            Dictionary<string, CheckinRecord> map = new Dictionary<string, CheckinRecord>();
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i] == null || string.IsNullOrEmpty(records[i].FinishedAt)) continue;
                string day = records[i].FinishedAt.Substring(0, Math.Min(10, records[i].FinishedAt.Length));
                map[day] = records[i];
            }
            DateTime start = DateTime.Now.Date.AddDays(-6);
            for (int i = 0; i < 7; i++)
            {
                DateTime d = start.AddDays(i);
                CheckinRecord r;
                map.TryGetValue(d.ToString("yyyy-MM-dd"), out r);
                Border sq = Square(r);
                sq.Margin = new Thickness(i == 0 ? 0 : 3, 0, 0, 0);
                sq.ToolTip = d.ToString("MM-dd") + " " + (r == null ? "无记录" : StatusText(r.Status));
                sp.Children.Add(sq);
            }
            Grid.SetColumn(sp, column);
            return sp;
        }

        private Border Square(CheckinRecord r)
        {
            Border b = new Border();
            b.Width = 20;
            b.Height = 20;
            b.CornerRadius = new CornerRadius(6);
            string bg = "MutedSoftBrush";
            string fg = "MutedBrush";
            string glyph = "·";
            if (r != null)
            {
                if (r.Status == CheckinStatus.Success && !r.Confirmed) { bg = "WarningSoftBrush"; fg = "WarningBrush"; glyph = "!"; }
                else if (r.Status == CheckinStatus.Success) { bg = "SuccessSoftBrush"; fg = "SuccessBrush"; glyph = "✓"; }
                else if (r.Status == CheckinStatus.Already) { bg = "AccentSoftBrush"; fg = "AccentBrush"; glyph = "✓"; }
                else if (r.Status == CheckinStatus.Pending) { bg = "WarningSoftBrush"; fg = "WarningBrush"; glyph = "!"; }
                else if (r.Status == CheckinStatus.Failed || r.Status == CheckinStatus.Auth ||
                         r.Status == CheckinStatus.Occupied) { bg = "DangerSoftBrush"; fg = "DangerBrush"; glyph = "×"; }
            }
            b.SetResourceReference(Border.BackgroundProperty, bg);
            TextBlock t = new TextBlock();
            t.Text = glyph;
            t.FontSize = 10;
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.VerticalAlignment = VerticalAlignment.Center;
            t.SetResourceReference(TextBlock.ForegroundProperty, fg);
            b.Child = t;
            return b;
        }

        private Border StatCard(string label, string value, string sub, string accentKey, double leftMargin)
        {
            StackPanel sp = new StackPanel();
            StackPanel head = new StackPanel();
            head.Orientation = Orientation.Horizontal;
            Ellipse dot = UiKit.Dot(accentKey, 6);
            dot.Margin = new Thickness(0, 0, 6, 0);
            TextBlock l = UiKit.Muted(label);
            head.Children.Add(dot);
            head.Children.Add(l);
            TextBlock v = UiKit.Text(value, 22, "TextBrush", FontWeights.SemiBold);
            v.Margin = new Thickness(0, 4, 0, 2);
            TextBlock s = UiKit.Muted(sub);
            s.FontSize = 10;
            sp.Children.Add(head);
            sp.Children.Add(v);
            sp.Children.Add(s);
            Border card = UiKit.Card(sp);
            card.Margin = new Thickness(leftMargin, 0, 6, 0);
            return card;
        }

        private string NextRunText(List<Account> accounts)
        {
            DateTime now = DateTime.Now;
            DateTime? best = null;
            for (int i = 0; i < accounts.Count; i++)
            {
                Account a = accounts[i];
                if (!a.Enabled) continue;
                PlatformSetting p = _app.Settings.Platform(a.Platform);
                if (p == null || !p.Enabled) continue;
                DateTime t = SchedulerPolicy.TargetAt(now, p.Target, SchedulerPolicy.JitterSec(a.Id, now.ToString("yyyy-MM-dd"), p.JitterSec));
                if (t <= now) t = t.AddDays(1);
                if (!best.HasValue || t < best.Value) best = t;
            }
            if (!best.HasValue) return "--";
            return (best.Value.Date > now.Date ? "明日 " : string.Empty) + best.Value.ToString("HH:mm");
        }

        private static string StatusText(string status)
        {
            if (status == CheckinStatus.Success) return "签到成功";
            if (status == CheckinStatus.Already) return "今日已签到";
            if (status == CheckinStatus.Pending) return "待领取";
            if (status == CheckinStatus.Auth) return "需要重新授权";
            if (status == CheckinStatus.Occupied) return "本设备今日已被占用";
            if (status == CheckinStatus.GivenUp) return "今日已放弃";
            if (status == CheckinStatus.Failed) return "签到失败";
            return "未签到";
        }
    }
}
