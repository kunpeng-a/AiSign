using System;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace AiSign.UI
{
    internal sealed class ToggleSwitch : ToggleButton
    {
        public ToggleSwitch()
        {
            object style = Application.Current == null ? null : Application.Current.Resources["Switch"];
            if (style is Style) Style = (Style)style;
            VerticalAlignment = VerticalAlignment.Center;
        }
    }
}
