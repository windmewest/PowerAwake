using System.Globalization;

namespace PowerAwake.App;

/// <summary>Strings for the redesigned settings window and tray menu (kept separate from <see cref="AppText"/>).</summary>
internal sealed record SettingsText
{
    public string SleepDesc { get; init; } = "";
    public string HibernateDesc { get; init; } = "";
    public string DimDesc { get; init; } = "";
    public string DisplayOffDesc { get; init; } = "";
    public string DimBrightnessDesc { get; init; } = "";
    public string SmoothSection { get; init; } = "";
    public string SmoothDesc { get; init; } = "";
    public string FadeDesc { get; init; } = "";
    public string SoftwareDimming { get; init; } = "";
    public string SoftwareDimmingDesc { get; init; } = "";
    public string DetectedTitle { get; init; } = "";
    public string DetectInternal { get; init; } = "";
    public string DetectDdc { get; init; } = "";
    public string DetectSoftware { get; init; } = "";
    public string DetectNone { get; init; } = "";
    public string DetectNeedSoftware { get; init; } = "";
    public string ApplyAcDesc { get; init; } = "";
    public string ApplyDcDesc { get; init; } = "";
    public string DesktopScopeNote { get; init; } = "";
    public string LanguageDesc { get; init; } = "";
    public string Seconds { get; init; } = "";
    public string Hours { get; init; } = "";
    public string DimDisabledHint { get; init; } = "";
    public string ErrNeedScope { get; init; } = "";
    public string ErrOffAfterDim { get; init; } = "";
    public string UntilTime { get; init; } = "";
    public string AboutSection { get; init; } = "";
    public string VersionTitle { get; init; } = "";
    public string VersionDesc { get; init; } = "";
    public string CheckUpdate { get; init; } = "";
    public string Checking { get; init; } = "";
    public string UpToDate { get; init; } = "";
    public string NewVersion { get; init; } = "";
    public string UpdateNow { get; init; } = "";
    public string Downloading { get; init; } = "";
    public string UpdateFailed { get; init; } = "";
    public string UpdatePortable { get; init; } = "";
    public string OpenReleasePage { get; init; } = "";

    public static SettingsText For(string? languageCode) => (languageCode == "system" ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName : languageCode) switch
    {
        "es" => Spanish,
        "en" => English,
        _ => Chinese
    };

    private static readonly SettingsText Chinese = new()
    {
        SleepDesc = "无操作一段时间后，电脑自动进入睡眠",
        HibernateDesc = "无操作一段时间后，电脑自动进入休眠",
        DimDesc = "无操作一段时间后，降低屏幕亮度",
        DisplayOffDesc = "无操作一段时间后，关闭屏幕",
        DimBrightnessDesc = "屏幕变暗后保持的亮度",
        SmoothSection = "渐变调光",
        SmoothDesc = "用平滑过渡代替 Windows 的突然变暗与恢复",
        FadeDesc = "变暗与恢复的过渡时间",
        SoftwareDimming = "软件调光",
        SoftwareDimmingDesc = "显示器不支持硬件调光（多数台式机）时，用半透明遮罩模拟变暗",
        DetectedTitle = "检测到的调光方式",
        DetectInternal = "内置屏幕 × {0}",
        DetectDdc = "外接显示器 DDC/CI × {0}",
        DetectSoftware = "软件遮罩 × {0}",
        DetectNone = "当前显示器均不支持硬件调光，变暗将使用 Windows 原生行为",
        DetectNeedSoftware = "未检测到支持硬件调光的显示器，开启“软件调光”即可使用渐变",
        ApplyAcDesc = "接通电源时使用这些设置",
        ApplyDcDesc = "使用电池时使用这些设置",
        DesktopScopeNote = "未检测到电池，此电脑始终使用电源（AC）设置",
        LanguageDesc = "选择界面语言",
        Seconds = "秒",
        Hours = "小时",
        DimDisabledHint = "已将“屏幕变暗”设为从不，以下选项暂不可用",
        ErrNeedScope = "请至少选择应用于 AC 或 DC。",
        ErrOffAfterDim = "关闭屏幕时间必须晚于屏幕变暗时间。",
        UntilTime = "保持唤醒至 {0}",
        AboutSection = "关于",
        VersionTitle = "PowerAwake {0}",
        VersionDesc = "从 GitHub 检查是否有新版本",
        CheckUpdate = "检查更新",
        Checking = "正在检查…",
        UpToDate = "已是最新版本",
        NewVersion = "发现新版本 {0}",
        UpdateNow = "立即更新",
        Downloading = "正在下载 {0}%",
        UpdateFailed = "更新失败：{0}",
        UpdatePortable = "当前不是安装版，将打开下载页面手动更新",
        OpenReleasePage = "打开页面"
    };

    private static readonly SettingsText English = new()
    {
        SleepDesc = "Put the PC to sleep after it has been idle",
        HibernateDesc = "Hibernate the PC after it has been idle",
        DimDesc = "Lower the screen brightness after the PC has been idle",
        DisplayOffDesc = "Turn the screen off after the PC has been idle",
        DimBrightnessDesc = "Brightness kept while the screen is dimmed",
        SmoothSection = "Smooth dimming",
        SmoothDesc = "Fade smoothly instead of Windows' abrupt dim and restore",
        FadeDesc = "How long the dim and restore transitions take",
        SoftwareDimming = "Software dimming",
        SoftwareDimmingDesc = "For monitors without hardware brightness control (most desktops), emulate dimming with a translucent overlay",
        DetectedTitle = "Detected dimming methods",
        DetectInternal = "Built-in screen × {0}",
        DetectDdc = "External monitor DDC/CI × {0}",
        DetectSoftware = "Software overlay × {0}",
        DetectNone = "No display supports hardware dimming; Windows' native dimming is used",
        DetectNeedSoftware = "No display with hardware dimming was found. Turn on Software dimming to enable fading",
        ApplyAcDesc = "Use these settings when plugged in",
        ApplyDcDesc = "Use these settings on battery",
        DesktopScopeNote = "No battery detected: this PC always uses the plugged-in (AC) settings",
        LanguageDesc = "Choose the display language",
        Seconds = "seconds",
        Hours = "hours",
        DimDisabledHint = "Dim display is set to Never, so these options are unavailable",
        ErrNeedScope = "Select at least AC or DC.",
        ErrOffAfterDim = "Turn off display must be later than Dim display.",
        UntilTime = "Awake until {0}",
        AboutSection = "About",
        VersionTitle = "PowerAwake {0}",
        VersionDesc = "Check GitHub for a newer version",
        CheckUpdate = "Check for updates",
        Checking = "Checking...",
        UpToDate = "You are on the latest version",
        NewVersion = "New version {0} is available",
        UpdateNow = "Update now",
        Downloading = "Downloading {0}%",
        UpdateFailed = "Update failed: {0}",
        UpdatePortable = "This copy was not installed with the installer; the download page will open instead",
        OpenReleasePage = "Open page"
    };

    private static readonly SettingsText Spanish = new()
    {
        SleepDesc = "Suspender el equipo tras un periodo de inactividad",
        HibernateDesc = "Hibernar el equipo tras un periodo de inactividad",
        DimDesc = "Reducir el brillo de la pantalla tras un periodo de inactividad",
        DisplayOffDesc = "Apagar la pantalla tras un periodo de inactividad",
        DimBrightnessDesc = "Brillo que se mantiene con la pantalla atenuada",
        SmoothSection = "Atenuacion suave",
        SmoothDesc = "Transicion gradual en lugar del cambio brusco de Windows",
        FadeDesc = "Duracion de las transiciones de atenuar y restaurar",
        SoftwareDimming = "Atenuacion por software",
        SoftwareDimmingDesc = "Para monitores sin control de brillo por hardware (la mayoria de equipos de escritorio), simula la atenuacion con una capa translucida",
        DetectedTitle = "Metodos de atenuacion detectados",
        DetectInternal = "Pantalla integrada × {0}",
        DetectDdc = "Monitor externo DDC/CI × {0}",
        DetectSoftware = "Capa por software × {0}",
        DetectNone = "Ninguna pantalla admite atenuacion por hardware; se usa la de Windows",
        DetectNeedSoftware = "No se encontro ninguna pantalla con control por hardware. Activa la atenuacion por software para usar la transicion",
        ApplyAcDesc = "Usar estos ajustes con corriente",
        ApplyDcDesc = "Usar estos ajustes con bateria",
        DesktopScopeNote = "No se detecto bateria: este equipo siempre usa los ajustes de corriente (AC)",
        LanguageDesc = "Elige el idioma de la interfaz",
        Seconds = "segundos",
        Hours = "horas",
        DimDisabledHint = "Atenuar pantalla esta en Nunca, por lo que estas opciones no estan disponibles",
        ErrNeedScope = "Selecciona al menos AC o DC.",
        ErrOffAfterDim = "Apagar pantalla debe ser posterior a Atenuar pantalla.",
        UntilTime = "Activo hasta las {0}",
        AboutSection = "Acerca de",
        VersionTitle = "PowerAwake {0}",
        VersionDesc = "Busca nuevas versiones en GitHub",
        CheckUpdate = "Buscar actualizaciones",
        Checking = "Buscando...",
        UpToDate = "Ya tienes la ultima version",
        NewVersion = "Nueva version {0} disponible",
        UpdateNow = "Actualizar ahora",
        Downloading = "Descargando {0}%",
        UpdateFailed = "No se pudo actualizar: {0}",
        UpdatePortable = "Esta copia no se instalo con el instalador; se abrira la pagina de descargas",
        OpenReleasePage = "Abrir pagina"
    };
}
