using PowerAwake.Core.Models;

namespace PowerAwake.App;

internal sealed class IntervalDialog : Form
{
    private readonly NumericUpDown minutesInput = new() { Minimum = 1, Maximum = 7 * 24 * 60, Value = 60, Dock = DockStyle.Fill };

    private IntervalDialog(AppText text)
    {
        Text = text.CustomInterval;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(300, 120);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 2 };
        layout.Controls.Add(new Label { Text = text.Minutes, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(minutesInput, 1, 0);
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        var ok = new Button { Text = text.Apply, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = text.Cancel, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 1);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public static TimeSpan? ShowCustom(IWin32Window? owner, AppText text)
    {
        using var dialog = new IntervalDialog(text);
        return dialog.ShowDialog(owner) == DialogResult.OK
            ? TimeSpan.FromMinutes((double)dialog.minutesInput.Value)
            : null;
    }
}