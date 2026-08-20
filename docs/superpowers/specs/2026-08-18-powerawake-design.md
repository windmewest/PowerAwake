# powerAwake 产品与技术设计规格

- 文档状态：待用户复核
- 版本：1.0
- 日期：2026-08-18
- 目标平台：Windows 11 x64
- 建议技术栈：.NET 8、WinForms、Windows 原生电源管理 API

## 1. 项目目标

powerAwake 是一个常驻 Windows 通知区域（系统托盘）的轻量电源策略管理程序。它解决 PowerToys Awake 在锁屏和 Modern Standby 场景下可能无法持续阻止系统睡眠的问题。

powerAwake 不依赖临时的线程执行状态请求来保持系统唤醒。启用 Awake 时，它直接修改当前 Windows 电源计划，把插电和电池模式的自动睡眠、自动休眠时间设为“从不”；关闭 Awake 时，它恢复启用前保存的正常睡眠和休眠时间。

程序同时管理屏幕自动变暗、变暗亮度和关闭屏幕时间。插电与电池模式使用相同设置。

## 2. 范围

### 2.1 必须实现

1. 通知区域常驻图标与右键菜单。
2. Awake 关闭、无限期、定时三种状态。
3. 启用 Awake 时，把当前电源计划的 AC/DC 睡眠和休眠时间设为“从不”。
4. 关闭 Awake、定时到期或正常退出程序时，恢复启用前的设置。
5. `Keep screen on` 功能。
6. 独立设置屏幕变暗时间、变暗亮度和屏幕关闭时间。
7. 插电和电池模式始终写入相同数值。
8. 开机登录后自启动选项。
9. 图标随 Awake 状态变化。
10. 单实例运行、异常恢复、操作日志和清晰的失败提示。

### 2.2 首版不实现

1. 多用户之间同步配置。
2. 远程控制、云同步或遥测。
3. 根据运行中的应用自动切换 Awake。
4. 阻止用户手动睡眠、合盖睡眠、低电量休眠、系统更新重启或强制关机。
5. 修改锁屏、登录、Windows Hello 等安全策略。
6. 创建或删除 Windows 电源计划。

## 3. 术语与关键原则

- **程序运行**：powerAwake 进程正在通知区域驻留。
- **Awake 开启**：无限期或定时保持唤醒状态。
- **Awake 关闭**：程序仍在运行，但不阻止系统按正常时间睡眠。
- **正常策略**：Awake 关闭时使用的睡眠、休眠和显示设置。
- **恢复快照**：每次进入 Awake 前，从当前电源计划读取并落盘的实际设置。
- **Keep screen on**：仅在 Awake 开启时生效；使屏幕不自动变暗且不自动关闭。

核心安全原则：先成功保存恢复快照，再修改 Windows 电源计划。没有有效快照时，不得进入 Awake。

## 4. 用户体验设计

### 4.1 托盘菜单

右键单击图标显示以下菜单：

```text
powerAwake — 当前状态
────────────────────────
○ Awake 关闭
○ Keep awake indefinitely
○ Keep awake interval
    15 分钟
    30 分钟
    1 小时
    2 小时
    自定义…
☐ Keep screen on
────────────────────────
剩余时间：01:24:36          （仅定时模式显示）
设置…
☑ 开机自启动
退出 powerAwake
```

交互规则：

- 三种 Awake 模式互斥。
- 单击一种模式后立即应用；不需要额外“确定”。
- `Keep screen on` 可以预先勾选，但只在 Awake 开启时改变电源计划。
- 定时模式到期后切换为 Awake 关闭，程序继续驻留。
- 双击托盘图标打开设置窗口。
- 鼠标悬停提示显示当前模式；定时模式同时显示预计结束时间。

### 4.2 托盘图标状态

必须使用形状和颜色共同表达状态，不能只依赖颜色：

| 状态 | 图标建议 | 提示文本 |
|---|---|---|
| Awake 关闭 | 灰色月牙或空心电源符号 | `powerAwake：已关闭` |
| 无限期 | 绿色实心电源符号并带无限标记 | `powerAwake：无限期保持唤醒` |
| 定时 | 橙色时钟符号 | `powerAwake：定时保持唤醒，剩余 …` |
| 写入或恢复失败 | 红色叹号覆盖层 | `powerAwake：电源策略错误` |

图标资源至少提供 16、20、24、32、48 和 256 像素尺寸，并兼顾浅色、深色任务栏的辨识度。

### 4.3 设置窗口

设置窗口包含以下区域。

#### 正常睡眠策略

- 自动睡眠：`从不`、1–180 分钟或自定义。
- 自动休眠：`从不`、1–720 分钟或自定义。
- 明确显示：“下列设置同时应用于插电和电池模式”。

正常策略值是 Awake 关闭时应使用的值。每次进入 Awake 前，程序先把正常策略同步写入当前方案的 AC/DC，再回读并创建恢复快照；因此关闭 Awake 时能够精确恢复到本次启用前由 powerAwake 管理的正常策略。

#### 显示策略

- 屏幕变暗时间：`从不`、**30 秒**、1、2、3、5、10、15、30、45、60 分钟或自定义。
- 变暗亮度：0%–100% 滑条，同时显示精确百分比。
- 关闭屏幕时间：`从不`、1、2、3、5、10、15、30、45、60 分钟或自定义。
- 显示设置同时应用于插电和电池模式。

时间控件采用“常用选项下拉框 + 自定义数字输入”，不采用难以精确定位的纯滑条。亮度使用滑条并允许键盘方向键精调。

#### 程序选项

- 登录 Windows 后自动启动。
- 可选的“启动后显示通知”，默认关闭。
- “立即读取当前电源计划”按钮。
- “应用”与“取消”按钮。

### 4.4 参数校验

1. 如果变暗和关闭屏幕都不是“从不”，关闭屏幕时间必须晚于变暗时间。
2. 违反规则时禁止保存，并在对应控件旁显示说明；程序不得静默改值。
3. 自定义 Awake 间隔允许 1 分钟至 7 天。
4. 睡眠、休眠和屏幕时间内部统一以无符号秒数存储；`0` 表示“从不”。
5. 变暗亮度只允许 0–100。

## 5. 状态机

### 5.1 状态

```text
Off
AwakeIndefinite
AwakeInterval(endTimeUtc)
Error(recoveryAvailable)
```

`KeepScreenOn` 是 Awake 状态上的独立布尔选项，不构成第四种 Awake 模式。

从 Off 进入任一 Awake 状态时，动作顺序固定为：校验配置 → 将正常策略同步到当前方案的 AC/DC → 回读验证 → 原子保存恢复快照 → 写入 Awake 覆盖值 → 回读验证。任一步失败都不得报告 Awake 已开启。

### 5.2 状态转换

| 起始状态 | 操作或事件 | 目标状态 | 必须执行的动作 |
|---|---|---|---|
| Off | 开启无限期 | AwakeIndefinite | 建立快照；睡眠/休眠设为从不；按 Keep Screen On 应用显示策略 |
| Off | 开启定时 | AwakeInterval | 同上；保存 UTC 截止时间并启动计时 |
| AwakeInterval | 到期 | Off | 恢复快照；删除恢复文件；程序继续驻留 |
| 任意 Awake | 用户关闭 Awake | Off | 恢复快照；删除恢复文件 |
| 任意 Awake | 切换为另一 Awake 模式 | 新 Awake 状态 | 沿用原始快照，不重复覆盖；更新截止时间 |
| 任意 Awake | 切换 Keep Screen On | 原 Awake 状态 | 开启时显示超时设为从不；关闭时恢复快照中的显示策略 |
| 任意 Awake | 退出程序或注销 | 进程退出 | 先恢复快照；恢复成功后退出 |
| 任意状态 | 写入失败 | Error | 停止后续写入；尽最大努力回滚；保留恢复文件；显示错误状态 |
| 启动 | 发现有效恢复文件 | Off | 先恢复快照，再完成启动；不自动恢复 Awake |
| 启动 | 无恢复文件 | Off | 读取配置与当前电源计划；Awake 保持关闭 |

### 5.3 重启与计时规则

- powerAwake 可以随 Windows 登录自动启动。
- 每次新进程启动时，Awake 默认关闭。
- 不续接上次的无限期状态。
- 不续接重启前未完成的定时间隔。
- 定时使用 UTC 截止时间计算，不依赖逐秒递减计数，避免系统时钟暂停或程序短暂阻塞造成累计误差。
- 系统进入睡眠后，进程计时器可能暂停；恢复运行时必须立即比较当前 UTC 时间，到期则恢复策略并关闭 Awake。

## 6. 电源策略行为

### 6.1 受管理的 Windows 设置

| 设置 | 子组 GUID | 设置 GUID |
|---|---|---|
| 自动睡眠 | `238c9fa8-0aad-41ed-83f4-97be242c8f20` | `29f6c1db-86da-48c5-9fdb-f2b67b1f44da` |
| 自动休眠 | `238c9fa8-0aad-41ed-83f4-97be242c8f20` | `9d7815a6-7ee4-497e-8888-515a05f02364` |
| 屏幕变暗时间 | `7516b95f-f776-4464-8c53-06167f40cc99` | `17aaa29b-8b43-4b94-aafe-35f64daaf1ee` |
| 关闭屏幕时间 | `7516b95f-f776-4464-8c53-06167f40cc99` | `3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e` |
| 变暗亮度 | `7516b95f-f776-4464-8c53-06167f40cc99` | `f1fbfde2-a960-4165-9f88-50667911ce96` |

不得修改合盖动作、电源按钮动作、锁屏策略、自适应亮度、唤醒计时器或其他未列出的电源参数。

### 6.2 原生 API

通过 `powrprof.dll` P/Invoke 使用：

- `PowerGetActiveScheme`
- `PowerReadACValueIndex`
- `PowerReadDCValueIndex`
- `PowerWriteACValueIndex`
- `PowerWriteDCValueIndex`
- `PowerSetActiveScheme`

完成一组写入后调用 `PowerSetActiveScheme` 重新激活同一方案，使设置立即生效。必须检查所有 Win32 返回码并转换为用户可理解的错误信息。原生内存必须通过 `LocalFree` 正确释放。

首版不以解析 `powercfg.exe` 的本地化文本作为主要实现方式。`powercfg /query` 可以保留为诊断命令，但不参与核心逻辑。

### 6.3 Awake 关闭

Awake 关闭时应用正常策略：

- AC/DC 自动睡眠使用相同的正常睡眠时间。
- AC/DC 自动休眠使用相同的正常休眠时间。
- AC/DC 屏幕变暗时间、变暗亮度和关闭屏幕时间使用相同设置。
- `Keep screen on` 不改变电源计划。

### 6.4 Awake 开启且 Keep screen on 关闭

- AC/DC 自动睡眠：0（从不）。
- AC/DC 自动休眠：0（从不）。
- 屏幕变暗时间、变暗亮度和关闭屏幕时间保持进入 Awake 前的值。

### 6.5 Awake 开启且 Keep screen on 开启

- AC/DC 自动睡眠：0（从不）。
- AC/DC 自动休眠：0（从不）。
- AC/DC 屏幕变暗时间：0（从不）。
- AC/DC 关闭屏幕时间：0（从不）。
- 变暗亮度无需改写，因为变暗超时已禁用。

取消 `Keep screen on` 时，只恢复显示相关快照，Awake 仍保持开启，睡眠和休眠仍为“从不”。

## 7. 首次运行与“上次正常时间”

程序需要持久保存最近一次有效的正常睡眠和休眠时间。

首次运行时：

1. 读取当前活动电源计划的 AC/DC 值，但不立即写入。
2. 如果 AC 与 DC 值相同，直接作为建议正常值。
3. 如果二者不同，按以下确定性规则生成建议值：
   - 睡眠：优先采用非零的 AC 值；AC 为零且 DC 非零时采用 DC 值；两者均为零时建议 15 分钟。
   - 休眠：优先采用非零的 AC 值；AC 为零且 DC 非零时采用 DC 值；两者均为零时建议“从不”。
4. 设置窗口清楚显示建议值；在用户单击“应用”或首次启用 Awake 前，不统一写入 AC/DC。
5. 用户保存后，该值成为“上次正常时间”。以后关闭 Awake 时恢复该值。

每次从 Off 进入 Awake 时还要保存实际值到恢复快照。快照用于本次事务回滚；正常配置用于长期偏好。二者不得混用。

## 8. 配置与恢复文件

数据目录：

```text
%LOCALAPPDATA%\PowerAwake\
  settings.json
  recovery.json
  Logs\powerawake-YYYY-MM-DD.log
```

### 8.1 settings.json 建议结构

```json
{
  "schemaVersion": 1,
  "normalSleepSeconds": 900,
  "normalHibernateSeconds": 0,
  "displayDimSeconds": 120,
  "displayOffSeconds": 180,
  "dimBrightnessPercent": 50,
  "keepScreenOn": false,
  "startWithWindows": true,
  "showStartupNotification": false
}
```

不得把当前 Awake 状态作为跨重启自动恢复的设置保存。定时截止时间只属于当前运行会话。

### 8.2 recovery.json 建议结构

```json
{
  "schemaVersion": 1,
  "sessionId": "GUID",
  "createdUtc": "2026-08-18T10:00:00Z",
  "schemeGuid": "381b4222-f694-41f0-9685-ff5bb260df2e",
  "original": {
    "sleepAc": 900,
    "sleepDc": 900,
    "hibernateAc": 0,
    "hibernateDc": 0,
    "dimAc": 120,
    "dimDc": 120,
    "displayOffAc": 180,
    "displayOffDc": 180,
    "dimBrightnessAc": 50,
    "dimBrightnessDc": 50
  }
}
```

文件写入必须使用同目录临时文件、刷新并原子替换，避免半写入 JSON。恢复成功前不得删除 `recovery.json`。

## 9. 电源计划在运行期间被外部切换

进入 Awake 时绑定并记录当前活动方案 GUID。

如果 Awake 期间发现活动方案被其他程序或用户切换：

1. 不得把旧方案的快照写入新方案。
2. 尝试恢复原方案中被 powerAwake 修改的值。
3. 自动关闭 Awake。
4. 保持新方案为活动方案，不擅自切回旧方案。
5. 显示通知：“检测到电源计划已切换，Awake 已安全关闭。”

检测时机至少包括托盘菜单打开、设置窗口打开、计时器周期检查以及 Windows 电源模式变化事件。

## 10. 自启动与单实例

### 10.1 自启动

使用当前用户注册表项，无需管理员权限：

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
Name: powerAwake
Value: "<完整 exe 路径>" --startup
```

写入时必须正确引用含空格的路径。关闭自启动时只删除名为 `powerAwake` 且指向当前可执行文件的值，避免删除其他程序的数据。

### 10.2 单实例

使用带用户作用域的命名 Mutex。第二个实例启动时：

1. 不得修改电源计划。
2. 通知首个实例显示设置窗口。
3. 随后退出。

可使用命名管道完成实例间通知。

## 11. 进程退出、注销与异常恢复

### 11.1 正常退出

- 如果 Awake 已关闭，直接退出，不修改正常策略。
- 如果 Awake 已开启，先恢复恢复快照。
- 只有恢复成功才能正常退出。
- 恢复失败时显示阻塞性提示，保留托盘进程与恢复文件，并提供“重试恢复”和“强制退出”两项；强制退出必须警告系统可能继续保持“从不睡眠”。

### 11.2 Windows 注销或关机

在系统允许的时间内尝试同步恢复，但不能假定关机回调一定完成。因此恢复文件必须在修改电源计划前落盘，下次启动继续恢复。

### 11.3 崩溃或断电

下次启动发现 `recovery.json` 时：

1. 校验 JSON 结构和方案 GUID。
2. 将快照恢复到其所属方案。
3. 不改变用户当前选择的活动方案。
4. 恢复成功后删除恢复文件并进入 Off。
5. 恢复失败则进入 Error，显示可复制的错误详情和诊断日志路径。

## 12. 日志与隐私

- 不收集鼠标、触控板、键盘或屏幕内容。
- 不联网，不发送遥测。
- 日志只记录时间、模式转换、方案 GUID、设置名称、旧值、新值和 Win32 错误码。
- 不记录用户名、文件内容或输入事件。
- 日志默认保留 14 天；启动时删除更早日志。

## 13. 建议代码结构

```text
PowerAwake.sln
src/
  PowerAwake.App/
    Program.cs
    TrayApplicationContext.cs
    UI/
      SettingsForm.cs
      IntervalDialog.cs
    Icons/
    app.manifest
  PowerAwake.Core/
    Models/
      AppSettings.cs
      PowerSnapshot.cs
      AwakeState.cs
    Services/
      AwakeController.cs
      SettingsService.cs
      RecoveryService.cs
      StartupService.cs
      SingleInstanceService.cs
      Clock.cs
  PowerAwake.Windows/
    Power/
      IPowerPolicy.cs
      WindowsPowerPolicy.cs
      PowrProfNative.cs
    Notifications/
tests/
  PowerAwake.Core.Tests/
  PowerAwake.Windows.Tests/
docs/
  superpowers/specs/
```

边界要求：

- `PowerAwake.Core` 不引用 WinForms 或 P/Invoke，便于纯单元测试。
- 所有系统电源读写通过 `IPowerPolicy`。
- 所有时间判断通过 `IClock`，定时测试不得依赖真实等待。
- UI 只发送用户意图和展示状态，不直接调用原生 API。

## 14. 错误处理

1. 读取失败：保持当前状态，不执行写入，显示错误。
2. 快照持久化失败：禁止开启 Awake。
3. 部分写入失败：按快照回滚已经修改的项目，并进入 Error。
4. 回滚失败：保留恢复文件和红色错误图标，提供错误码与日志路径。
5. 配置文件损坏：备份为 `.corrupt-时间戳`，使用首次运行流程重新生成；不得据此修改电源计划。
6. 设置 GUID 在硬件上不受支持：标记对应功能不可用；睡眠核心功能不可用时禁止进入 Awake，单独的变暗亮度不支持时可降级并告知用户。

## 15. 测试策略

### 15.1 单元测试

至少覆盖：

1. Off → Indefinite：先保存快照，再把 AC/DC 睡眠和休眠设为零。
2. Off → Interval：正确保存 UTC 截止时间。
3. Interval 到期：恢复快照但不退出应用。
4. Awake 模式之间切换：不覆盖初始快照。
5. Keep Screen On 开关：只修改显示相关值。
6. 关闭 Awake：恢复所有快照字段。
7. 写入中途失败：执行回滚并保留恢复文件。
8. 启动发现恢复文件：先恢复，再进入 Off。
9. 重启不恢复 Awake 状态。
10. 外部切换电源方案：恢复旧方案、保持新方案活动、关闭 Awake。
11. AC/DC 写入值始终一致，恢复快照时除外；恢复必须还原原始 AC/DC 各自值。
12. 30 秒变暗选项转换为 30 秒。
13. 关闭屏幕早于或等于变暗时间时校验失败。
14. 单实例第二进程不接触电源策略。

### 15.2 集成测试

在隔离的临时电源计划或可恢复的测试方案上执行：

1. 原生 API 能正确读写并重新读取五个受管设置。
2. 开启 Awake 后 `powercfg /query` 显示 AC/DC 睡眠和休眠均为 0。
3. Keep Screen On 开启后显示变暗和关闭超时均为 0。
4. 关闭 Awake 后所有值与快照完全相同。
5. 自启动注册表值的创建、路径引用和删除正确。

集成测试不得直接修改用户日常使用的电源计划，除非测试明确建立快照并在 `finally` 中恢复。

### 15.3 手工验收

1. 托盘图标在四类状态下清晰可辨。
2. 定时模式菜单每分钟更新剩余时间，悬停提示合理。
3. 计时到期后应用仍在托盘，图标变为 Off。
4. 设置窗口支持 125%、150%、200% DPI。
5. 深色和浅色任务栏上图标均清楚。
6. Windows 登录后自动启动时不弹出设置窗口。
7. 强制结束 Awake 状态下的进程，再启动后能恢复原策略并提示用户。

## 16. 验收标准

以下条件全部满足才算首版完成：

1. `Keep awake indefinitely` 能通过电源计划阻止 AC/DC 自动睡眠和休眠。
2. `Keep awake interval` 支持预设和自定义时间，到期后只关闭 Awake。
3. 关闭 Awake 后，睡眠、休眠和显示值精确恢复到本次启用前的恢复快照，程序仍驻留。
4. 显示变暗、变暗亮度、显示关闭可独立设置，包含 30 秒变暗选项。
5. `Keep screen on` 在 Awake 期间阻止变暗和熄屏，关闭后恢复显示策略。
6. AC/DC 正常配置保持一致。
7. 图标准确反映 Off、无限期、定时和错误状态。
8. 开机自启动可开关，重启后 Awake 默认关闭。
9. 程序异常结束后，下一次启动可依据落盘快照恢复。
10. 应用不读取输入设备数据、不联网、不要求管理员权限。
11. 自动化测试、Release 构建和实际电源值回读验证全部通过。

## 17. 构建与交付建议

- 目标框架：`net8.0-windows`
- UI：WinForms，`UseWindowsForms=true`
- 平台：`win-x64`
- Release 发布：单文件；优先使用框架依赖模式以减少体积，本机需安装 .NET 8 Desktop Runtime。
- 禁用 trimming，避免 WinForms 和序列化反射路径受损。
- 输出包含：`powerAwake.exe`、README、许可证、SHA-256 校验值和测试结果摘要。
- 应用清单使用 `asInvoker`；首版不要求管理员权限。

## 18. 非目标保证与用户提示

powerAwake 只控制 Windows 电源计划中的空闲超时。它不能保证阻止以下事件：

- 用户主动点击“睡眠”或“休眠”；
- 合上笔记本盖触发的动作；
- 电池达到临界电量；
- Windows 更新、固件、驱动或硬件导致的重启；
- 断电、蓝屏、强制关机；
- 企业策略或其他电源管理软件随后覆盖设置。

关于这一边界必须在 README 和设置窗口的帮助文本中清楚说明。
