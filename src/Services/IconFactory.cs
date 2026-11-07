using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace AiSign.Services
{
    internal static class IconFactory
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static Icon Create(Color color, int size)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush b = new SolidBrush(color))
                {
                    g.FillEllipse(b, 1, 1, size - 3, size - 3);
                }
                using (Pen p = new Pen(Color.White, Math.Max(1.6f, size / 16f)))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawLines(p, new PointF[]
                    {
                        new PointF(size * 0.28f, size * 0.52f),
                        new PointF(size * 0.44f, size * 0.68f),
                        new PointF(size * 0.73f, size * 0.35f)
                    });
                }
            }
            IntPtr h = bmp.GetHicon();
            try
            {
                using (Icon tmp = Icon.FromHandle(h))
                {
                    return (Icon)tmp.Clone();
                }
            }
            finally
            {
                DestroyIcon(h);
                bmp.Dispose();
            }
        }
    }
}
