# ForkPlus 跨平台（Avalonia）重构验证资料

本目录存放 ForkPlus v3.9.0 在 **Ubuntu / Wine** 环境下运行原版客户端得到的**界面基线**，用于支撑后续迁移到 Avalonia 的逐页回归验证——不依赖 Windows 真机。

## 目录结构

- `v3.9.0/` — v3.9.0 验证资料（主目录）
  - `*.png`（**62 张**）— 功能截图，按编号分组
  - `验证报告.md` — 验证结论、可复现环境、两处 WinRT 崩溃点补丁、截图映射、迁移对照价值
  - `操作步骤.md` — 每张截图的操作路径（鼠标坐标 / 菜单路径 / 键盘操作）+ 环境限制与未覆盖项
  - `源码映射.md` — **界面 → 代码文件 → 平台 API → Avalonia 替代**的精确映射（基于 v3.9.0 真实源码审计）
  - `index.html` — 62 张图索引页
- `README.md` — 本文件

## 三件套配合使用

| 文档 | 回答的问题 |
|------|------|
| 截图（`*.png` + `index.html`） | 界面长什么样、点了什么 |
| `操作步骤.md` | 怎么操作到的（按钮坐标、菜单路径、截图有效性说明） |
| `源码映射.md` | 这段代码在哪、平台依赖是什么、Avalonia 端怎么替换 |

## 关键发现（迁移前必读）

- **v3.9.0 核心改动**：AI 辅助界面（WebView2 承载的流式 Markdown 渲染）统一重构——这是 Avalonia 迁移中替换成本最高的区域。
- **最大利好**：Git 引擎 `biturbo`（libgit2 封装）已发布 **Windows / Linux / macOS 三平台原生库**，核心 Git 逻辑**无需重写**。
- **架构接缝已存在**：`ServiceLocator` 已抽象 7 个平台接口（`IDispatcher`/`IClipboardService`/`IToastNotificationService`/`IWindowManagerService`/`IAppContext`/`IDesignModeService`/`ITimerService`），各有一个 `Wpf*` 实现——Avalonia 端只需补 `Avalonia*` 实现。
- **最大替换成本**：WebView2 / AI 渲染、AvalonEdit、OxyPlot.Wpf、主题检测（UISettings + 注册表）、WindowsCredentialManager、Shell 集成、Git 路径硬编码（`git.exe`/`bash.exe`）。
- **UI 工作量**：245 个 WPF `.xaml` 需做语法转换。

## 使用方式

打开 `v3.9.0/index.html` 浏览全部截图；重构某界面时，对照 `源码映射.md` 第 3 节的代码文件定位，按需阅读对应 `.xaml/.cs` 源码。

## 环境

Ubuntu 22.04 沙箱（无图形界面）+ Wine 11 + Xvfb(:99) + 手工拼装的 .NET 10 WindowsDesktop 运行时。可复现步骤见 `v3.9.0/验证报告.md` 第 3 节。
