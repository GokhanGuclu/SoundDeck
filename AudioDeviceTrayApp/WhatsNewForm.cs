using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AudioDeviceTrayApp
{
    /// <summary>
    /// A small themed dialog that shows the release notes after an update.
    /// </summary>
    public class WhatsNewForm : Form
    {
        private static readonly Color Bg = Color.FromArgb(24, 24, 27);
        private static readonly Color Sidebar = Color.FromArgb(30, 30, 33);
        private static readonly Color Surface = Color.FromArgb(39, 39, 42);
        private static readonly Color Accent = Color.FromArgb(139, 92, 246);
        private static readonly Color AccentHover = Color.FromArgb(124, 58, 237);
        private static readonly Color TextMain = Color.FromArgb(228, 228, 231);
        private static readonly Color TextDim = Color.FromArgb(161, 161, 170);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        public WhatsNewForm(string version, string notes, Icon? appIcon)
        {
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(480, 420);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = TextMain;
            Font = new Font("Segoe UI", 9.5F);
            ShowInTaskbar = false;
            TopMost = true;
            if (appIcon != null) Icon = appIcon;

            // Title bar
            var titleBar = new Panel { Left = 0, Top = 0, Width = ClientSize.Width, Height = 48, BackColor = Sidebar };
            titleBar.Paint += (s, e) =>
            {
                using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(0, 0, titleBar.Width, 3),
                    Accent, Color.FromArgb(59, 130, 246),
                    System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
                e.Graphics.FillRectangle(brush, 0, 0, titleBar.Width, 3);
            };
            titleBar.MouseDown += Drag;
            Controls.Add(titleBar);

            var emoji = new Label
            {
                Text = "🎉",
                Font = new Font("Segoe UI Emoji", 14F),
                AutoSize = true,
                Left = 16,
                Top = 11,
                BackColor = Color.Transparent
            };
            emoji.MouseDown += Drag;
            titleBar.Controls.Add(emoji);

            var title = new Label
            {
                Text = Localization.T("whats_new_heading", version),
                Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
                ForeColor = Accent,
                AutoSize = true,
                Left = 50,
                Top = 13,
                BackColor = Color.Transparent
            };
            title.MouseDown += Drag;
            titleBar.Controls.Add(title);

            // Notes
            var box = new RichTextBox
            {
                Left = 16,
                Top = 62,
                Width = ClientSize.Width - 32,
                Height = ClientSize.Height - 62 - 66,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Surface,
                ForeColor = TextMain,
                Font = new Font("Segoe UI", 10F),
                Text = CleanNotes(notes),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Cursor = Cursors.Default
            };
            box.Padding = new Padding(10);
            Controls.Add(box);

            // Close button
            var close = new Button
            {
                Text = Localization.T("btn_close"),
                Width = 130,
                Height = 38,
                Left = ClientSize.Width - 130 - 16,
                Top = ClientSize.Height - 38 - 16,
                BackColor = Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            close.FlatAppearance.BorderSize = 0;
            close.MouseEnter += (s, e) => close.BackColor = AccentHover;
            close.MouseLeave += (s, e) => close.BackColor = Accent;
            close.Click += (s, e) => Close();
            Controls.Add(close);
            AcceptButton = close;
        }

        private static string CleanNotes(string notes)
        {
            // Light Markdown cleanup so the raw text reads nicely in a plain text box.
            return notes
                .Replace("\r\n", "\n")
                .Replace("### ", "")
                .Replace("## ", "")
                .Replace("# ", "")
                .Replace("**", "")
                .Trim();
        }

        private void Drag(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
        }
    }
}
