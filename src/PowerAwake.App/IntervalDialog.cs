using PowerAwake.App.Ui;

namespace PowerAwake.App;

internal sealed class IntervalDialog : Form
{
    private readonly FluentTextBox minutesInput;
    private readonly FluentButton ok;

    private IntervalDialog(AppText text)
    {
        var palette = Theme.Current;
        Text = text.CustomInterval;
        Font = Fonts.Create(9f);
        BackColor = palette.WindowBack;
        ForeColor = palette.Text;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(this.S(360), this.S(150));
        Icon = StateIconFactory.Create(TrayIconState.Interval, Theme.TaskbarUsesDark);

        var caption = new Label
        {
            Text = text.CustomInterval,
            Font = Fonts.Create(10.5f, FontStyle.Bold),
            ForeColor = palette.Text,
            BackColor = palette.WindowBack,
            AutoSize = true,
            Location = new Point(this.S(24), this.S(20))
        };
        minutesInput = new FluentTextBox(palette, 120, digitsOnly: true) { Value = "60", Location = new Point(this.S(24), this.S(56)) };
        var unit = new Label
        {
            Text = text.Minutes,
            ForeColor = palette.SubText,
            BackColor = palette.WindowBack,
            AutoSize = true,
            Location = new Point(this.S(24) + minutesInput.Width + this.S(10), this.S(62))
        };
        ok = new FluentButton(palette, text.Apply, primary: true) { DialogResult = DialogResult.OK };
        var cancel = new FluentButton(palette, text.Cancel, primary: false) { DialogResult = DialogResult.Cancel };
        ok.Location = new Point(ClientSize.Width - this.S(24) - ok.Width, ClientSize.Height - this.S(18) - ok.Height);
        cancel.Location = new Point(ok.Left - this.S(8) - cancel.Width, ok.Top);
        minutesInput.TextChanged2 += (_, _) => ok.Enabled = MinutesValue() is not null;

        Controls.AddRange(new Control[] { caption, minutesInput, unit, ok, cancel });
        AcceptButton = ok;
        CancelButton = cancel;
        HandleCreated += (_, _) => NativeUi.ApplyWindowTheme(Handle, palette);
    }

    private int? MinutesValue() =>
        int.TryParse(minutesInput.Value, out var minutes) && minutes is >= 1 and <= 7 * 24 * 60 ? minutes : null;

    public static TimeSpan? ShowCustom(IWin32Window? owner, AppText text)
    {
        using var dialog = new IntervalDialog(text);
        return dialog.ShowDialog(owner) == DialogResult.OK && dialog.MinutesValue() is { } minutes
            ? TimeSpan.FromMinutes(minutes)
            : null;
    }
}
