using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using AiSign.Core;

namespace AiSign.UI
{
    internal static class UiKit
    {
        private static readonly DropShadowEffect CardShadow;

        static UiKit()
        {
            DropShadowEffect e = new DropShadowEffect();
            e.BlurRadius = 18;
            e.ShadowDepth = 2;
            e.Opacity = 0.10;
            e.Color = Colors.Black;
            e.Freeze();
            CardShadow = e;
        }

        public static TextBlock Text(string text, double size, string brushKey, FontWeight weight)
        {
            TextBlock t = new TextBlock();
            t.Text = text == null ? string.Empty : text;
            t.FontSize = size;
            t.FontWeight = weight;
            t.FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            t.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
            return t;
        }

        public static TextBlock Title(string text) { return Text(text, 22, "TextBrush", FontWeights.SemiBold); }
        public static TextBlock Section(string text) { return Text(text, 15, "TextBrush", FontWeights.SemiBold); }
        public static TextBlock Body(string text) { return Text(text, 13, "TextBrush", FontWeights.Normal); }
        public static TextBlock Muted(string text) { return Text(text, 12, "MutedBrush", FontWeights.Normal); }

        public static Border Card(UIElement child)
        {
            Border b = new Border();
            b.CornerRadius = new CornerRadius(12);
            b.Padding = new Thickness(16);
            b.BorderThickness = new Thickness(1);
            b.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
            b.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            b.Effect = CardShadow;
            b.Child = child;
            return b;
        }

        public static Border Panel(UIElement child)
        {
            Border b = new Border();
            b.CornerRadius = new CornerRadius(10);
            b.Padding = new Thickness(12);
            b.BorderThickness = new Thickness(1);
            b.SetResourceReference(Border.BackgroundProperty, "SurfaceAltBrush");
            b.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            b.Child = child;
            return b;
        }

        public static Button Button(string text, string styleKey, RoutedEventHandler click)
        {
            Button b = new Button();
            b.Content = text;
            object style = Application.Current == null ? null : Application.Current.Resources[styleKey];
            if (style is Style) b.Style = (Style)style;
            if (click != null) b.Click += click;
            return b;
        }

        public static Button IconButton(string geometry, string tooltip, RoutedEventHandler click)
        {
            Button b = Button(string.Empty, "BtnIcon", click);
            b.Content = Icon(geometry, "MutedBrush", 16);
            b.ToolTip = tooltip;
            return b;
        }

        public static StackPanel H(double gap, params UIElement[] items)
        {
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.VerticalAlignment = VerticalAlignment.Center;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                FrameworkElement fe = items[i] as FrameworkElement;
                if (fe != null && i > 0)
                    fe.Margin = new Thickness(gap, fe.Margin.Top, fe.Margin.Right, fe.Margin.Bottom);
                sp.Children.Add(items[i]);
            }
            return sp;
        }

        public static StackPanel V(double gap, params UIElement[] items)
        {
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Vertical;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                FrameworkElement fe = items[i] as FrameworkElement;
                if (fe != null && i > 0)
                    fe.Margin = new Thickness(fe.Margin.Left, gap, fe.Margin.Right, fe.Margin.Bottom);
                sp.Children.Add(items[i]);
            }
            return sp;
        }

        public static Border Pill(string text, string fgKey, string bgKey)
        {
            Border b = new Border();
            b.CornerRadius = new CornerRadius(20);
            b.Padding = new Thickness(9, 3, 9, 3);
            b.SetResourceReference(Border.BackgroundProperty, bgKey);
            TextBlock t = Text(text, 11, fgKey, FontWeights.SemiBold);
            b.Child = t;
            return b;
        }

        public static Border StatusPill(string status)
        {
            string text;
            string fg;
            string bg;
            StatusStyle(status, out text, out fg, out bg);
            return Pill(text, fg, bg);
        }

        public static Border RecordPill(CheckinRecord record)
        {
            if (record == null) return Pill("未签到", "MutedBrush", "MutedSoftBrush");
            if (record.Status == CheckinStatus.Success && !record.Confirmed)
                return Pill("待确认", "WarningBrush", "WarningSoftBrush");
            return StatusPill(record.Status);
        }

        public static void StatusStyle(string status, out string text, out string fg, out string bg)
        {
            if (status == CheckinStatus.Success) { text = "已签到"; fg = "SuccessBrush"; bg = "SuccessSoftBrush"; return; }
            if (status == CheckinStatus.Already) { text = "今日已签"; fg = "AccentBrush"; bg = "AccentSoftBrush"; return; }
            if (status == CheckinStatus.Pending) { text = "待领取"; fg = "WarningBrush"; bg = "WarningSoftBrush"; return; }
            if (status == CheckinStatus.Auth) { text = "需授权"; fg = "DangerBrush"; bg = "DangerSoftBrush"; return; }
            if (status == CheckinStatus.Occupied) { text = "设备占用"; fg = "DangerBrush"; bg = "DangerSoftBrush"; return; }
            if (status == CheckinStatus.GivenUp) { text = "已放弃"; fg = "MutedBrush"; bg = "MutedSoftBrush"; return; }
            if (status == CheckinStatus.Failed) { text = "失败"; fg = "DangerBrush"; bg = "DangerSoftBrush"; return; }
            if (status == CheckinStatus.Skipped) { text = "已跳过"; fg = "MutedBrush"; bg = "MutedSoftBrush"; return; }
            text = "待签到"; fg = "MutedBrush"; bg = "MutedSoftBrush";
        }

        public static Border Avatar(string name, string softKey, string fgKey)
        {
            Border b = new Border();
            b.Width = 36;
            b.Height = 36;
            b.CornerRadius = new CornerRadius(18);
            b.SetResourceReference(Border.BackgroundProperty, softKey);
            string initial = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
            TextBlock t = Text(initial, 14, fgKey, FontWeights.SemiBold);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.VerticalAlignment = VerticalAlignment.Center;
            b.Child = t;
            return b;
        }

        public static Ellipse Dot(string brushKey, double size)
        {
            Ellipse e = new Ellipse();
            e.Width = size;
            e.Height = size;
            e.SetResourceReference(Shape.FillProperty, brushKey);
            e.VerticalAlignment = VerticalAlignment.Center;
            return e;
        }

        public static UIElement Icon(string geometry, string brushKey, double size)
        {
            Path p = new Path();
            p.Data = Geometry.Parse(geometry);
            p.Stretch = Stretch.Uniform;
            p.Width = size;
            p.Height = size;
            p.VerticalAlignment = VerticalAlignment.Center;
            p.SetResourceReference(Shape.FillProperty, brushKey);
            return p;
        }

        public static string PlatformBrush(string platform)
        {
            if (platform == PlatformIds.Trae) return "TraeBrush";
            if (platform == PlatformIds.WorkBuddy) return "WorkBuddyBrush";
            return "QoderBrush";
        }

        public static string PlatformSoft(string platform)
        {
            if (platform == PlatformIds.Trae) return "TraeSoftBrush";
            if (platform == PlatformIds.WorkBuddy) return "WorkBuddySoftBrush";
            return "QoderSoftBrush";
        }

        public static string PlatformIcon(string platform)
        {
            if (platform == PlatformIds.Trae)
                return "M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zm0 2c-4.4 0-8 2.2-8 5v1h16v-1c0-2.8-3.6-5-8-5z";
            if (platform == PlatformIds.WorkBuddy)
                return "M4 5h16a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H9l-5 4v-4H4a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2z";
            return "M12 2l8 4v6c0 5-3.4 8.4-8 10-4.6-1.6-8-5-8-10V6l8-4z";
        }

        public static StackPanel PageHeader(string title, string subtitle, UIElement right)
        {
            StackPanel left = new StackPanel();
            left.Children.Add(Title(title));
            if (!string.IsNullOrEmpty(subtitle))
            {
                TextBlock sub = Muted(subtitle);
                sub.Margin = new Thickness(0, 4, 0, 0);
                left.Children.Add(sub);
            }
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(left);
            if (right != null)
            {
                Grid.SetColumn(right, 1);
                FrameworkElement fe = right as FrameworkElement;
                if (fe != null)
                {
                    fe.HorizontalAlignment = HorizontalAlignment.Right;
                    fe.VerticalAlignment = VerticalAlignment.Center;
                }
                g.Children.Add(right);
            }
            StackPanel holder = new StackPanel();
            holder.Margin = new Thickness(0, 0, 0, 18);
            holder.Children.Add(g);
            return holder;
        }

        public static Border SeparatorLine()
        {
            Border b = new Border();
            b.Height = 1;
            b.Margin = new Thickness(0, 12, 0, 12);
            b.SetResourceReference(Border.BackgroundProperty, "BorderBrush");
            return b;
        }

        public static string MaskUid(string uid)
        {
            return Hash.Mask(uid, 6, 4);
        }
    }
}
