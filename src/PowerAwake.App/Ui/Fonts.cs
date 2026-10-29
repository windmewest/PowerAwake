namespace PowerAwake.App.Ui;

internal static class Fonts
{
    private static readonly bool HasVariable = FontFamily.Families.Any(f => f.Name == "Segoe UI Variable Text");

    public static Font Create(float size, FontStyle style = FontStyle.Regular) =>
        new(HasVariable ? "Segoe UI Variable Text" : "Segoe UI", size, style);
}
