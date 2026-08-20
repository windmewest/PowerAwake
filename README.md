# PowerAwake

[English](#english) | [Espanol](#espanol) | [中文](#中文)

PowerAwake is a lightweight Windows 11 tray application that manages the active Windows power plan to keep a computer awake. It is designed for cases where a temporary execution-state request is not sufficient, including lock-screen and Modern Standby scenarios.

## 中文

### 功能

- 常驻系统托盘，双击无穷图标可在“Awake 关闭”和“无限期保持唤醒”之间切换。
- 无限期或定时保持唤醒；关闭、到期、退出或下一次启动时会恢复保存的电源策略。
- 支持 `Keep screen on`、屏幕变暗/关闭时间、变暗亮度。
- 可单独选择将设置应用到 AC（接通电源）和 DC（使用电池）。
- Windows 通知、中文/English/Español 界面、当前用户自启动与单实例运行。
- 不收集输入、屏幕内容或遥测数据，也不联网。

### 安装

1. 在 GitHub Releases 下载 `PowerAwake-Setup-1.0.0-win-x64.exe`。
2. 运行安装程序。程序安装到当前用户目录，不需要管理员权限。
3. 在任务栏通知区域找到无穷图标。右键可使用完整菜单；双击可快捷切换开关。

### 发布者

创建并推送类似 `v1.0.0` 的 Git 标签后，仓库中的 GitHub Actions 会自动测试、构建安装包并创建 GitHub Release。

### 卸载

在 Windows 的“已安装的应用”中卸载 PowerAwake，或运行安装目录中的卸载程序。

### 注意事项

- 仅支持 Windows 11 x64。
- Awake 不会阻止手动睡眠、合盖睡眠、低电量休眠、Windows 更新重启或强制关机。
- 如果外部程序切换了电源计划，PowerAwake 会安全关闭并恢复此前修改的旧计划设置。

## English

### Features

- Runs in the Windows notification area. Double-click the infinity icon to toggle between Awake off and indefinite Awake.
- Keeps the system awake indefinitely or for a timed interval, then restores the saved power policy when disabled, expired, exited, or recovered at the next start.
- Supports **Keep screen on**, display dim/off timeouts, and dim brightness.
- Apply settings independently to AC power and battery power.
- Windows notifications, Chinese/English/Spanish UI, per-user startup, and single-instance operation.
- No telemetry, network activity, input capture, or screen-content collection.

### Install

1. Download `PowerAwake-Setup-1.0.0-win-x64.exe` from GitHub Releases.
2. Run the installer. It installs for the current user and does not require administrator rights.
3. Find the infinity icon in the notification area. Right-click for the full menu; double-click for the fast toggle.

### Publishing

Create and push a Git tag such as `v1.0.0`. The included GitHub Actions workflow tests the project, builds the installer, and creates a GitHub Release automatically.

### Uninstall

Uninstall PowerAwake from Windows **Installed apps**, or run the uninstaller in the application directory.

### Notes

- Windows 11 x64 only.
- Awake does not prevent manual sleep, lid-close sleep, low-battery hibernation, Windows Update restarts, or forced shutdowns.
- If another application changes the active power plan, PowerAwake safely turns Awake off and restores the settings it modified in the old plan.

## Espanol

### Funciones

- Se ejecuta en el area de notificacion de Windows. Haz doble clic en el icono de infinito para alternar entre Awake desactivado y Awake indefinido.
- Mantiene el sistema activo de forma indefinida o durante un intervalo y restaura la politica de energia guardada al desactivarse, vencer el intervalo, salir o recuperarse en el siguiente inicio.
- Incluye **Mantener pantalla encendida**, tiempos de atenuacion/apagado y brillo de atenuacion.
- Permite aplicar la configuracion de forma independiente con corriente AC y con bateria.
- Notificaciones de Windows, interfaz en chino/ingles/espanol, inicio automatico por usuario y una sola instancia.
- Sin telemetria, actividad de red, captura de entrada ni recopilacion del contenido de pantalla.

### Instalacion

1. Descarga `PowerAwake-Setup-1.0.0-win-x64.exe` desde GitHub Releases.
2. Ejecuta el instalador. Se instala para el usuario actual y no requiere permisos de administrador.
3. Busca el icono de infinito en el area de notificacion. Haz clic derecho para el menu completo y doble clic para el cambio rapido.

### Publicacion

Crea y envia una etiqueta de Git como `v1.0.0`. El flujo de GitHub Actions incluido prueba el proyecto, genera el instalador y crea automaticamente una version de GitHub Releases.

### Desinstalacion

Desinstala PowerAwake desde **Aplicaciones instaladas** de Windows o ejecuta el desinstalador en el directorio de la aplicacion.

### Notas

- Solo Windows 11 x64.
- Awake no bloquea la suspension manual, la suspension al cerrar la tapa, la hibernacion por bateria baja, los reinicios de Windows Update ni los apagados forzados.
- Si otra aplicacion cambia el plan de energia activo, PowerAwake desactiva Awake de forma segura y restaura los ajustes modificados en el plan anterior.