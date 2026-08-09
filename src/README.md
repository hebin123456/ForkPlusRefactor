# ForkPlus · Avalonia 跨平台迁移工程

本目录（`src/`）是 [ForkPlus](https://github.com/hebin123456/ForkPlus) 从 **WPF (net10.0-windows)** 迁移到 **Avalonia (net10.0)** 的跨平台工程。
目标是让 ForkPlus 能原生运行在 Windows / Linux / macOS，核心 Git 逻辑（`biturbo` 引擎）保持不变。

## 当前状态：迁移起点骨架（可构建）

已搭好第一个可编译的工程，并完成了**架构接缝**的跨平台化——这是后续 245 个 XAML、226 个 Git 命令搬运的锚点。

### 工程结构
```
src/
└── ForkPlus.Avalonia/               主工程（net10.0，Avalonia.Desktop 12.1.1）
    ├── Program.cs                   Avalonia 跨平台入口（UsePlatformDetect）
    ├── App.axaml / App.axaml.cs     应用启动 + ServiceLocator 初始化
    ├── MainWindow.axaml(.cs)        迁移起点占位窗口（展示已接入服务）
    └── Services/
        ├── IAppContext.cs           7 个平台抽象接口（从原 WPF 工程原样搬，namespace ForkPlus.Services）
        ├── IClipboardService.cs
        ├── IDesignModeService.cs
        ├── IDispatcher.cs
        ├── ITimerService.cs
        ├── IToastNotificationService.cs
        ├── IWindowManagerService.cs
        ├── ServiceLocator.cs         轻量服务定位器（业务层取服务的统一入口，零改动）
        └── Avalonia/                7 个接口的 Avalonia 实现（替换原 Wpf* 实现）
            ├── AvaloniaAppContext.cs
            ├── AvaloniaClipboardService.cs
            ├── AvaloniaDesignModeService.cs
            ├── AvaloniaDispatcher.cs
            ├── AvaloniaTimerService.cs
            ├── AvaloniaToastNotificationService.cs
            ├── AvaloniaWindowManagerService.cs
            └── ToastWindow.cs        in-app Toast 浮层（解析 WinRT Toast XML）
```

### 接缝清单（已落地）
原 WPF 工程通过 `ServiceLocator` 把 7 类平台能力抽象为接口，各有 `Wpf*` 实现。
本工程保持接口与 `ServiceLocator`（namespace `ForkPlus.Services`）不变，新增 `Avalonia*` 实现，
**业务层（未来搬运的 ViewModel / Git Commands）引用不变**。

| 接口 | 原 WPF 实现要点 | 本工程 Avalonia 实现要点 |
|---|---|---|
| `IAppContext` | 取 `App.ForkDirectoryPath` 等静态路径 | 按相同规则算 `LocalApplicationData/ForkPlus` 等，**兼容原有用户数据目录** |
| `IClipboardService` | WPF `Clipboard` 同步 + 重试 | 异步 `IClipboard` 包装为同步契约 |
| `IDesignModeService` | `DesignerProperties` | `Design.IsDesignMode` |
| `IDispatcher` | WPF `Dispatcher` | `Dispatcher.UIThread`（同线程直接执行避免死锁） |
| `ITimerService` | `DispatcherTimer` | `Avalonia.DispatcherTimer`（同语义） |
| `IToastNotificationService` | WinRT Toast XML | **解析同一份 WinRT Toast XML**，in-app 浮层显示 |
| `IWindowManagerService` | `Application.Current.Windows` + 硬判断 `AiCodeReviewWindow` | 泛化为按 Title 匹配任意窗口 |

### 关键设计决策（基于阅读 ForkPlus v3.9.0 源码，非猜测）
- **数据目录兼容**：`AppDataDirectory` / `RepositoriesFilePath` 严格沿用 `LocalApplicationData/ForkPlus`、`.../ForkPlusData/repositories.toml`，迁移后用户仓库列表不丢。
- **Toast 契约保留 WinRT XML**：`NotificationManager` 已把通知内容序列化为 WinRT Toast XML，Avalonia 端直接解析，不引入新协议。
- **不在原 WPF 工程做 UI 解耦**：原工程 TFM 为 windows-only，无法跨平台编译验证；UI 重写统一在 Avalonia 工程进行。

## 构建
CI（`.github/workflows/build.yml`）在每次 push / PR 到 `main` 时，于 `ubuntu-latest` 上用 .NET 10 SDK 构建本工程。

本地构建（需 .NET 10 SDK + Avalonia 12.1.1）：
```bash
dotnet build src/ForkPlus.Avalonia/ForkPlus.Avalonia.csproj -c Release
```

## 下一步（按工作量评估 §6 路线）
1. **P2 核心视图 PoC**：用 `AvaloniaEdit` 实现最小 Diff 视图，喂 `biturbo` 真实数据，验证着色/边距/选区（最大风险点）。
2. **P3 平台适配**：主题（替换 `SystemThemeHelper` 的 `UISettings`/`Registry`）、凭据（`WindowsCredentialManager` → 跨平台凭据存储）、文件对话框（`OpenDialog` 的 CodePack Shell）。
3. **P4 AI 界面**：WebView2 → `Markdown.Avalonia` 或原生 webview。
4. **P5 测试与 CI**：FlaUI 测试 → Avalonia UI 测试；三平台打包。

> 配套资料：`../verification/v3.9.0/`（62 张运行截图 + 源码映射 + 操作步骤 + 工作量评估），作为逐界面回归基线。
