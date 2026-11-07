using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AiSign.Core;
using AiSign.Services;

namespace AiSign.UI
{
    internal sealed class AccountsPage
    {
        private readonly Window _owner;
        private readonly AppController _app;
        private readonly StackPanel _filters;
        private readonly StackPanel _list;
        private string _filter = "all";
        private bool _suppress;

        public UIElement Root { get; private set; }

        public AccountsPage(Window owner, AppController app)
        {
            _owner = owner;
            _app = app;

            StackPanel content = new StackPanel();
            content.Margin = new Thickness(24);

            Button add = UiKit.Button("添加账号", "BtnPrimary", delegate { OpenAdd(null, null); });
            content.Children.Add(UiKit.PageHeader("账号管理", "管理 Trae / WorkBuddy / Qoder 的多账号登录态", add));

            _filters = new StackPanel();
            _filters.Orientation = Orientation.Horizontal;
            _filters.Margin = new Thickness(0, 0, 0, 16);
            content.Children.Add(_filters);

            _list = new StackPanel();
            content.Children.Add(_list);

            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.Content = content;
            Root = sv;

            _app.AccountsChanged += OnChanged;
            _app.ResultsChanged += OnChanged;
            Rebuild();
        }

        private void OnChanged()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(Rebuild));
        }

        public void Refresh()
        {
            Rebuild();
        }

        private void Rebuild()
        {
            if (_suppress) return;
            _suppress = true;
            try
            {
                List<Account> all = _app.Accounts.All();
                RebuildFilters(all);
                _list.Children.Clear();

                if (all.Count == 0)
                {
                    _list.Children.Add(EmptyState());
                    return;
                }

                string[] platforms = PlatformIds.All();
                bool any = false;
                for (int i = 0; i < platforms.Length; i++)
                {
                    if (_filter != "all" && _filter != platforms[i]) continue;
                    List<Account> subset = new List<Account>();
                    for (int j = 0; j < all.Count; j++)
                        if (all[j].Platform == platforms[i]) subset.Add(all[j]);
                    if (subset.Count == 0 && _filter == "all") continue;
                    any = true;
                    _list.Children.Add(GroupHeader(platforms[i], subset.Count));
                    if (subset.Count == 0)
                    {
                        TextBlock empty = UiKit.Muted("该平台还没有账号");
                        empty.Margin = new Thickness(4, 4, 0, 14);
                        _list.Children.Add(empty);
                    }
                    for (int j = 0; j < subset.Count; j++)
                        _list.Children.Add(AccountCard(subset[j]));
                }
                if (!any) _list.Children.Add(EmptyState());
            }
            finally
            {
                _suppress = false;
            }
        }

        private void RebuildFilters(List<Account> all)
        {
            _filters.Children.Clear();
            int total = all.Count;
            _filters.Children.Add(FilterButton("全部", "all", total));
            string[] platforms = PlatformIds.All();
            for (int i = 0; i < platforms.Length; i++)
            {
                int count = 0;
                for (int j = 0; j < all.Count; j++)
                    if (all[j].Platform == platforms[i]) count++;
                _filters.Children.Add(FilterButton(PlatformIds.DisplayName(platforms[i]), platforms[i], count));
            }
        }

        private Button FilterButton(string text, string key, int count)
        {
            bool active = _filter == key;
            Button b = UiKit.Button(text + "  " + count, active ? "BtnGhostAccent" : "BtnGhost", delegate
            {
                _filter = key;
                Rebuild();
            });
            b.Margin = new Thickness(0, 0, 8, 0);
            b.Padding = new Thickness(12, 6, 12, 6);
            b.FontSize = 12;
            return b;
        }

        private UIElement GroupHeader(string platform, int count)
        {
            StackPanel left = new StackPanel();
            left.Orientation = Orientation.Horizontal;
            left.VerticalAlignment = VerticalAlignment.Center;
            Ellipse dot = UiKit.Dot(UiKit.PlatformBrush(platform), 8);
            dot.Margin = new Thickness(0, 0, 8, 0);
            TextBlock name = UiKit.Text(PlatformIds.DisplayName(platform), 14, "TextBrush", FontWeights.SemiBold);
            name.VerticalAlignment = VerticalAlignment.Center;
            Border tag = UiKit.Pill(platform == PlatformIds.Trae ? "单账号" : "多账号",
                platform == PlatformIds.Trae ? "MutedBrush" : "AccentBrush",
                platform == PlatformIds.Trae ? "MutedSoftBrush" : "AccentSoftBrush");
            tag.Margin = new Thickness(10, 0, 0, 0);
            left.Children.Add(dot);
            left.Children.Add(name);
            left.Children.Add(tag);

            StackPanel right = new StackPanel();
            right.Orientation = Orientation.Horizontal;
            TextBlock countText = UiKit.Muted(count + " 个账号");
            countText.VerticalAlignment = VerticalAlignment.Center;
            countText.Margin = new Thickness(0, 0, 10, 0);
            Button add = UiKit.Button("+ 添加", "BtnGhost", delegate { OpenAdd(platform, null); });
            add.FontSize = 12;
            add.Padding = new Thickness(10, 5, 10, 5);
            right.Children.Add(countText);
            right.Children.Add(add);

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            g.Children.Add(left);
            Grid.SetColumn(right, 1);
            g.Children.Add(right);

            Border wrap = new Border();
            wrap.Margin = new Thickness(0, 6, 0, 8);
            wrap.Child = g;
            return wrap;
        }

        private UIElement AccountCard(Account a)
        {
            Border card = new Border();
            card.CornerRadius = new CornerRadius(12);
            card.BorderThickness = new Thickness(1);
            card.Padding = new Thickness(0);
            card.Margin = new Thickness(0, 0, 0, 8);
            card.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
            card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            Border bar = new Border();
            bar.CornerRadius = new CornerRadius(12, 0, 0, 12);
            bar.SetResourceReference(Border.BackgroundProperty, UiKit.PlatformBrush(a.Platform));
            g.Children.Add(bar);

            Border avatar = UiKit.Avatar(a.Name, UiKit.PlatformSoft(a.Platform), UiKit.PlatformBrush(a.Platform));
            avatar.HorizontalAlignment = HorizontalAlignment.Center;
            avatar.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(avatar, 1);
            g.Children.Add(avatar);

            StackPanel info = new StackPanel();
            info.VerticalAlignment = VerticalAlignment.Center;
            info.Margin = new Thickness(0, 10, 10, 10);
            TextBlock name = UiKit.Text(a.Name, 13.5, "TextBrush", FontWeights.SemiBold);
            StackPanel meta = new StackPanel();
            meta.Orientation = Orientation.Horizontal;
            meta.Margin = new Thickness(0, 3, 0, 0);
            TextBlock uid = UiKit.Muted(UiKit.MaskUid(a.Uid));
            uid.Margin = new Thickness(0, 0, 8, 0);
            Border source = UiKit.Pill(SourceText(a.Source), "MutedBrush", "MutedSoftBrush");
            source.Margin = new Thickness(0, 0, 8, 0);
            Ellipse authDot = UiKit.Dot(AuthBrush(a.AuthState), 7);
            authDot.Margin = new Thickness(0, 0, 4, 0);
            TextBlock auth = UiKit.Muted(AuthText(a.AuthState));
            meta.Children.Add(uid);
            meta.Children.Add(source);
            meta.Children.Add(authDot);
            meta.Children.Add(auth);
            info.Children.Add(name);
            info.Children.Add(meta);
            Grid.SetColumn(info, 2);
            g.Children.Add(info);

            CheckinRecord rec = _app.RecordOf(a.Id);
            Border status = UiKit.RecordPill(rec);
            status.VerticalAlignment = VerticalAlignment.Center;
            status.Margin = new Thickness(0, 0, 12, 0);
            if (rec != null && !string.IsNullOrEmpty(rec.Message)) status.ToolTip = rec.Message;
            Grid.SetColumn(status, 3);
            g.Children.Add(status);

            ToggleSwitch toggle = new ToggleSwitch();
            toggle.IsChecked = a.Enabled;
            toggle.VerticalAlignment = VerticalAlignment.Center;
            toggle.Margin = new Thickness(0, 0, 8, 0);
            string accountId = a.Id;
            toggle.Checked += delegate
            {
                if (!_suppress) Application.Current.Dispatcher.BeginInvoke(new Action(delegate { _app.SetAccountEnabled(accountId, true); }));
            };
            toggle.Unchecked += delegate
            {
                if (!_suppress) Application.Current.Dispatcher.BeginInvoke(new Action(delegate { _app.SetAccountEnabled(accountId, false); }));
            };
            Grid.SetColumn(toggle, 4);
            g.Children.Add(toggle);

            Button more = UiKit.Button("⋯", "BtnIcon", null);
            more.FontSize = 16;
            more.VerticalAlignment = VerticalAlignment.Center;
            more.Margin = new Thickness(0, 0, 10, 0);
            more.Click += delegate { OpenMenu(more, a); };
            Grid.SetColumn(more, 5);
            g.Children.Add(more);

            card.Child = g;
            return card;
        }

        private void OpenMenu(Button anchor, Account a)
        {
            ContextMenu menu = new ContextMenu();
            MenuItem rename = new MenuItem();
            rename.Header = "重命名";
            rename.Click += delegate
            {
                string name = PromptDialog.Show(_owner, "重命名账号", "名称", a.Name);
                if (!string.IsNullOrEmpty(name)) _app.RenameAccount(a.Id, name);
            };
            MenuItem copy = new MenuItem();
            copy.Header = "复制账号 ID";
            copy.Click += delegate
            {
                try { Clipboard.SetText(a.Id); } catch { }
            };
            MenuItem remove = new MenuItem();
            remove.Header = "删除账号";
            remove.Foreground = Theme.Brush(Theme.Current.Danger);
            remove.Click += delegate
            {
                if (ConfirmDialog.Show(_owner, "删除账号", "确定删除「" + a.Name + "」？凭证与签到记录将一并移除。"))
                    _app.RemoveAccount(a.Id);
            };
            menu.Items.Add(rename);
            menu.Items.Add(copy);
            menu.Items.Add(new Separator());
            menu.Items.Add(remove);
            menu.PlacementTarget = anchor;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private UIElement EmptyState()
        {
            StackPanel sp = new StackPanel();
            sp.HorizontalAlignment = HorizontalAlignment.Center;
            sp.VerticalAlignment = VerticalAlignment.Center;
            sp.Margin = new Thickness(0, 50, 0, 50);

            UIElement icon = UiKit.Icon("M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zm0 2c-4.4 0-8 2.2-8 5v1h16v-1c0-2.8-3.6-5-8-5z", "MutedBrush", 40);
            TextBlock title = UiKit.Text("还没有账号", 16, "TextBrush", FontWeights.SemiBold);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.Margin = new Thickness(0, 12, 0, 4);
            TextBlock desc = UiKit.Muted("扫描本机登录态，或粘贴 Token 添加第一个账号");
            desc.HorizontalAlignment = HorizontalAlignment.Center;
            desc.Margin = new Thickness(0, 0, 0, 14);

            Button scan = UiKit.Button("扫描本机", "BtnPrimary", delegate { OpenAdd(null, "scan"); });
            Button paste = UiKit.Button("粘贴 Token", "BtnGhost", delegate { OpenAdd(null, "token"); });
            scan.Margin = new Thickness(0, 0, 8, 0);
            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.HorizontalAlignment = HorizontalAlignment.Center;
            row.Children.Add(scan);
            row.Children.Add(paste);

            sp.Children.Add(icon);
            sp.Children.Add(title);
            sp.Children.Add(desc);
            sp.Children.Add(row);

            Border card = UiKit.Card(sp);
            card.Margin = new Thickness(0, 20, 0, 0);
            return card;
        }

        private void OpenAdd(string platform, string mode)
        {
            AddAccountDialog dlg = new AddAccountDialog(_owner, _app, mode, platform);
            dlg.ShowDialog();
        }

        private static string SourceText(string source)
        {
            if (source == "oauth") return "浏览器";
            if (source == "token") return "Token";
            return "本机";
        }

        private static string AuthBrush(string state)
        {
            if (state == AccountState.NeedsReauth || state == AccountState.Locked) return "DangerBrush";
            if (state == AccountState.Expiring) return "WarningBrush";
            return "SuccessBrush";
        }

        private static string AuthText(string state)
        {
            if (state == AccountState.NeedsReauth) return "需授权";
            if (state == AccountState.Locked) return "凭证锁定";
            if (state == AccountState.Expiring) return "即将过期";
            return "正常";
        }
    }
}
