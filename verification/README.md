# ForkPlus 运行验证（Linux / Wine 基准）

本目录存放 **ForkPlus 原版 Windows 客户端**在 Ubuntu + Wine 环境下的运行验证产物，用于 **Avalonia 跨平台迁移**的界面与行为对照基准。验证环境均为无图形界面的沙箱，借助 Wine 11 + Xvfb 虚拟显示启动原版，无需 Windows 真机。

## 目录结构

| 版本 | 位置 | 内容 |
|------|------|------|
| **v3.8.3** | `verification/`（本目录根） | 6 张截图（`fp-*.png`、`forkplus-final2.png`）+ `验证报告.md` |
| **v3.9.0** | [`verification/v3.9.0/`](v3.9.0/) | 51 张功能截图 + `验证报告.md` + `index.html` 索引 |

## 验证环境

```
Ubuntu 22.04（无 GUI） + Wine 11.0 + Xvfb(:99)
+ 手工拼装的 .NET 10 WindowsDesktop 运行时（NuGet 包）
+ PortableGit + fontforge 字体伪装（Microsoft YaHei UI / Segoe UI / Consolas）
```

## 关键结论（两版一致）

- 原版可在纯 Linux 沙箱完整启动并跑通完整 Git 工作流；
- 仅两处 WinRT 调用（系统主题感知）在 Wine 下需 IL 补丁绕过，真 Windows 无影响；
- **v3.9.0 重点重构了 AI 辅助界面（WebView2 流式渲染）**，是 Avalonia 迁移中替换工作量最大的区域。

## 用途

迁移 Avalonia 时，可随时用该 Wine 环境启动原版截图，作为每个页面 / 交互的「标准答案」做像素级与行为级回归对比。
