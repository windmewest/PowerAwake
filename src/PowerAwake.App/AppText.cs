using System.Globalization;

namespace PowerAwake.App;

internal sealed record AppText(
    string SettingsTitle,
    string SleepPolicy,
    string DisplayPolicy,
    string Scope,
    string Language,
    string SystemDefault,
    string Sleep,
    string Hibernate,
    string Dim,
    string DisplayOff,
    string DimBrightness,
    string SmoothDimming,
    string SmoothDimmingDuration,
    string ApplyToAc,
    string ApplyToDc,
    string Apply,
    string Cancel,
    string Never,
    string Custom,
    string Minutes,
    string AwakeOff,
    string KeepAwakeIndefinitely,
    string KeepAwakeInterval,
    string KeepScreenOn,
    string Settings,
    string StartWithWindows,
    string Exit,
    string CustomInterval,
    string OpenedTitle,
    string OpenedIndefinite,
    string OpenedInterval,
    string ClosedTitle,
    string ClosedMessage,
    string ExpiredMessage,
    string ErrorTitle,
    string ExternalPlanChanged)
{
    public static IReadOnlyList<LanguageOption> Languages { get; } = new[]
    {
        new LanguageOption("system", "System default"),
        new LanguageOption("zh", "中文"),
        new LanguageOption("en", "English"),
        new LanguageOption("es", "Español")
    };

    public static AppText For(string? languageCode) => (languageCode == "system" ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName : languageCode) switch
    {
        "es" => Spanish,
        "en" => English,
        _ => Chinese
    };

    private static readonly AppText English = new(
        "powerAwake Settings", "Sleep policy", "Display policy", "Apply to", "Language", "System default", "Sleep", "Hibernate", "Dim display", "Turn off display", "Dim brightness",
        "Smooth screen dimming",
        "Fade duration",
        "Apply to AC power", "Apply to battery power", "Apply", "Cancel", "Never", "Custom...", "minutes",
        "Awake off", "Keep awake indefinitely", "Keep awake for", "Keep screen on", "Settings...", "Start with Windows", "Exit powerAwake", "Custom awake duration",
        "powerAwake is on", "Keeping the system awake indefinitely.", "Keeping the system awake for {0}.",
        "powerAwake is off", "Normal power settings have been restored.", "The awake interval ended. Normal power settings have been restored.",
        "powerAwake error", "The power plan changed. Awake was safely turned off.");

    private static readonly AppText Spanish = new(
        "Configuracion de powerAwake", "Politica de suspension", "Politica de pantalla", "Aplicar a", "Idioma", "Predeterminado del sistema", "Suspension", "Hibernacion", "Atenuar pantalla", "Apagar pantalla", "Brillo al atenuar",
        "Atenuacion suave de pantalla",
        "Duracion de la transicion",
        "Aplicar con corriente AC", "Aplicar con bateria", "Aplicar", "Cancelar", "Nunca", "Personalizado...", "minutos",
        "Awake desactivado", "Mantener activo indefinidamente", "Mantener activo durante", "Mantener pantalla encendida", "Configuracion...", "Iniciar con Windows", "Salir de powerAwake", "Duracion personalizada",
        "powerAwake esta activo", "El sistema permanecera activo indefinidamente.", "El sistema permanecera activo durante {0}.",
        "powerAwake esta desactivado", "Se restauraron los ajustes normales de energia.", "El intervalo activo termino. Se restauraron los ajustes normales de energia.",
        "Error de powerAwake", "El plan de energia cambio. Awake se desactivo de forma segura.");

    private static readonly AppText Chinese = new(
        "powerAwake 设置", "正常睡眠策略", "显示策略", "应用范围", "语言", "系统默认", "自动睡眠", "自动休眠", "屏幕变暗", "关闭屏幕", "变暗亮度",
        "渐变屏幕变暗",
        "渐变时长",
        "应用于 AC（接通电源）", "应用于 DC（使用电池）", "应用", "取消", "从不", "自定义…", "分钟",
        "Awake 关闭", "无限期保持唤醒", "定时保持唤醒", "保持屏幕开启", "设置…", "开机自启动", "退出 powerAwake", "自定义保持唤醒时间",
        "powerAwake 已开启", "系统将无限期保持唤醒。", "系统将保持唤醒 {0}。",
        "powerAwake 已关闭", "已恢复正常电源策略。", "定时保持唤醒已结束，已恢复正常电源策略。",
        "powerAwake 错误", "检测到电源计划已切换，Awake 已安全关闭。");
}

    internal sealed record LanguageOption(string Code, string DisplayName);
