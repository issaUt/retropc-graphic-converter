using System.Diagnostics;
using System.Text.Json;
namespace MzRubyConvGui;
public partial class MainForm
{
    private sealed class CenteredConfirmDialog : Form
    {
        public CenteredConfirmDialog(string title, string message)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Size = new Size(520, 300);
            MinimumSize = new Size(420, 240);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(14)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            var text = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Control,
                ScrollBars = ScrollBars.Vertical,
                Text = message,
                TabStop = false
            };
            root.Controls.Add(text, 0, 0);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            var yesButton = new Button
            {
                Text = "Yes",
                DialogResult = DialogResult.Yes,
                Width = 88
            };
            var noButton = new Button
            {
                Text = "No",
                DialogResult = DialogResult.No,
                Width = 88
            };
            buttons.Controls.Add(noButton);
            buttons.Controls.Add(yesButton);
            root.Controls.Add(buttons, 0, 1);

            AcceptButton = yesButton;
            CancelButton = noButton;
            Controls.Add(root);
            Shown += (_, _) =>
            {
                text.SelectionLength = 0;
                text.SelectionStart = 0;
                noButton.Focus();
            };
        }

        public DialogResult ShowDialogCentered(Form owner)
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(
                owner.Left + Math.Max(0, (owner.Width - Width) / 2),
                owner.Top + Math.Max(0, (owner.Height - Height) / 2));
            return ShowDialog(owner);
        }
    }
}
