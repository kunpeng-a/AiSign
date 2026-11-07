using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Data;

namespace AiSign.UI
{
    internal static class Styles
    {
        public static void Install(Application app)
        {
            Style baseButton = BuildBaseButton();
            app.Resources[typeof(Button)] = baseButton;
            app.Resources["BtnPrimary"] = BuildButton(baseButton, "AccentBrush", "WhiteBrush", "TransparentBrush", null);
            app.Resources["BtnGhost"] = BuildButton(baseButton, "TransparentBrush", "MutedBrush", "TransparentBrush", null);
            app.Resources["BtnGhostAccent"] = BuildButton(baseButton, "AccentSoftBrush", "AccentBrush", "TransparentBrush", null);
            app.Resources["BtnDanger"] = BuildButton(baseButton, "TransparentBrush", "DangerBrush", "DangerBrush", null);
            app.Resources["BtnSubtle"] = BuildButton(baseButton, "SurfaceAltBrush", "TextBrush", "BorderBrush", null);

            Style icon = new Style(typeof(Button), baseButton);
            icon.Setters.Add(new Setter(Control.WidthProperty, 30.0));
            icon.Setters.Add(new Setter(Control.HeightProperty, 30.0));
            icon.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
            icon.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("TransparentBrush")));
            icon.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("TransparentBrush")));
            icon.Setters.Add(new Setter(Control.FontSizeProperty, 14.0));
            app.Resources["BtnIcon"] = icon;

            app.Resources["Input"] = BuildInput();
            app.Resources["Switch"] = BuildSwitch();
        }

        private static Style BuildBaseButton()
        {
            Style s = new Style(typeof(Button));
            s.Setters.Add(new Setter(Control.TemplateProperty, BuildButtonTemplate()));
            s.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("SurfaceAltBrush")));
            s.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("TextBrush")));
            s.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("BorderBrush")));
            s.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 8, 14, 8)));
            s.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
            s.Setters.Add(new Setter(Control.FontFamilyProperty, new DynamicResourceExtension("BodyFont")));
            s.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
            s.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            s.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            s.Setters.Add(new Setter(UIElement.SnapsToDevicePixelsProperty, true));

            Trigger hover = new Trigger();
            hover.Property = UIElement.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.88));
            s.Triggers.Add(hover);

            Trigger press = new Trigger();
            press.Property = ButtonBase.IsPressedProperty;
            press.Value = true;
            press.Setters.Add(new Setter(UIElement.OpacityProperty, 0.72));
            s.Triggers.Add(press);

            Trigger disabled = new Trigger();
            disabled.Property = UIElement.IsEnabledProperty;
            disabled.Value = false;
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
            s.Triggers.Add(disabled);
            return s;
        }

        private static ControlTemplate BuildButtonTemplate()
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "Bd";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            border.SetValue(Border.SnapsToDevicePixelsProperty, true);

            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            content.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            border.AppendChild(content);

            ControlTemplate t = new ControlTemplate(typeof(Button));
            t.VisualTree = border;
            return t;
        }

        private static Style BuildButton(Style baseStyle, string bgKey, string fgKey, string borderKey, string unused)
        {
            Style s = new Style(typeof(Button), baseStyle);
            s.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension(bgKey)));
            s.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension(fgKey)));
            s.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension(borderKey)));
            return s;
        }

        private static Style BuildInput()
        {
            Style s = new Style(typeof(TextBox));
            s.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("InputBrush")));
            s.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("TextBrush")));
            s.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("BorderBrush")));
            s.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 7, 10, 7)));
            s.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
            s.Setters.Add(new Setter(Control.FontFamilyProperty, new DynamicResourceExtension("BodyFont")));
            s.Setters.Add(new Setter(TextBox.CaretBrushProperty, new DynamicResourceExtension("AccentBrush")));
            s.Setters.Add(new Setter(TextBox.SelectionBrushProperty, new DynamicResourceExtension("AccentSoftBrush")));
            s.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            s.Setters.Add(new Setter(Control.TemplateProperty, BuildInputTemplate()));
            return s;
        }

        private static ControlTemplate BuildInputTemplate()
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "Bd";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));

            FrameworkElementFactory sv = new FrameworkElementFactory(typeof(ScrollViewer));
            sv.Name = "PART_ContentHost";
            sv.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            sv.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            border.AppendChild(sv);

            ControlTemplate t = new ControlTemplate(typeof(TextBox));
            t.VisualTree = border;
            return t;
        }

        private static Style BuildSwitch()
        {
            Style s = new Style(typeof(ToggleButton));
            s.Setters.Add(new Setter(Control.WidthProperty, 42.0));
            s.Setters.Add(new Setter(Control.HeightProperty, 24.0));
            s.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
            s.Setters.Add(new Setter(Control.FocusableProperty, false));
            s.Setters.Add(new Setter(Control.TemplateProperty, BuildSwitchTemplate()));

            Trigger disabled = new Trigger();
            disabled.Property = UIElement.IsEnabledProperty;
            disabled.Value = false;
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4));
            s.Triggers.Add(disabled);
            return s;
        }

        private static ControlTemplate BuildSwitchTemplate()
        {
            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));
            grid.SetValue(FrameworkElement.WidthProperty, 42.0);
            grid.SetValue(FrameworkElement.HeightProperty, 24.0);

            FrameworkElementFactory track = new FrameworkElementFactory(typeof(Border));
            track.Name = "Track";
            track.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
            track.SetValue(Border.BackgroundProperty, new DynamicResourceExtension("BorderBrush"));
            grid.AppendChild(track);

            FrameworkElementFactory thumb = new FrameworkElementFactory(typeof(Ellipse));
            thumb.Name = "Thumb";
            thumb.SetValue(FrameworkElement.WidthProperty, 18.0);
            thumb.SetValue(FrameworkElement.HeightProperty, 18.0);
            thumb.SetValue(Shape.FillProperty, new DynamicResourceExtension("WhiteBrush"));
            thumb.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            thumb.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            thumb.SetValue(FrameworkElement.MarginProperty, new Thickness(3, 0, 0, 0));
            grid.AppendChild(thumb);

            ControlTemplate t = new ControlTemplate(typeof(ToggleButton));
            t.VisualTree = grid;

            Trigger on = new Trigger();
            on.Property = ToggleButton.IsCheckedProperty;
            on.Value = true;
            on.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("AccentBrush"), "Track"));
            on.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right, "Thumb"));
            on.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 3, 0), "Thumb"));
            t.Triggers.Add(on);
            return t;
        }
    }
}
