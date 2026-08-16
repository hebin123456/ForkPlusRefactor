# ForkPlus 跨平台（Avalonia）重构验证资料

本目录分两条线，分别管理**重构前**的 Windows-only 原版基线和**重构后**的 Avalonia 跨平台版验证产物。

## 目录结构

```
verification/
├── README.md                  ← 本文件
├── wpf-v3.9.0/                ← **重构前** 原版 Windows-only（WPF）基线
│   ├── 0?-*.png × 14          ← 主窗口 / 文件树 / 标签页 / 上下文菜单等
│   ├── 5?-*.png × 5           ← 外观 / 主题 / AI 助手
│   ├── 6?-*.png × 0           ← （预留）
│   ├── 7?-*.png × 6           ← 各种右键上下文菜单
│   ├── 8?-*.png × 0           ← （预留）
│   ├── 9?-*.png × 0           ← （预留）
│   ├── v2-*.png × ~30         ← v2 历史截图，作为"WPF 在 Wine 下能跑"的额外证据
│   ├── index.html             ← 全部 WPF 截图索引页
│   ├── 验证报告.md            ← 验证结论、可复现环境、WinRT 崩溃补丁、截图映射
│   ├── 操作步骤.md            ← 每张截图的操作路径 + 环境限制
│   ├── 源码映射.md            ← 界面 → 代码文件 → 平台 API → Avalonia 替代
│   └── 工作量评估.md          ← 迁移工作量评估
└── avalonia/                  ← **重构后** Avalonia 跨平台版（按 milestone 分目录）
    ├── m2-commits/            ← M2：提交列表
    │   ├── avalonia-m2-commits.png
    │   └── avalonia-m2-commits.txt
    ├── m3-diff/               ← M3：提交 diff 视图
    │   ├── avalonia-m3-diff.png
    │   ├── avalonia-m3-diff.txt
    │   └── 进度-M3.md         ← M3 设计取舍 / 视觉断言细节
    ├── m4-working-tree/       ← M4：工作区改动（相对 HEAD）
    │   ├── avalonia-m4-working-tree.png
    │   ├── avalonia-m4-working-tree.txt
    │   └── 进度-M4.md         ← M4 设计取舍 / 视觉断言细节
    ├── m5-file-tree/          ← M5：文件树 + 文件内容预览
    │   ├── avalonia-m5-file-tree.png
    │   ├── avalonia-m5-file-tree-mainwindow.png
    │   ├── avalonia-m5-file-tree.txt
    │   └── 进度-M5.md         ← M5 设计取舍 / 视觉断言细节
    └── m6-stash/              ← M6：贮藏 (stash) 列表 + apply/pop/drop/show diff
        ├── avalonia-m6-stash.png
        ├── avalonia-m6-stash-mainwindow.png
        ├── avalonia-m6-stash.txt
        └── 进度-M6.md         ← M6 设计取舍 / 视觉断言细节
```

## 区分原则

| 子目录 | 谁写 | 给谁看 | 何时更新 |
|--------|------|--------|----------|
| `wpf-v3.9.0/` | 一次性基线（v3.9.0 在 Wine 下截图） | Avalonia 迁移时对照原版长啥样 | **冻结** —— v3.9.0 已成历史，不会再改 |
| `avalonia/mN-*/` | 每次重构完成时由 headless 视觉测试自动落盘 | 评审重构质量 / 跨版本对比 | **每次重构后重生** —— 测试套件跑完自动覆盖 |

两者**不再混在同一目录**。原版基线冻结，新版本随重构推进。

## 三件套配合使用

| 文档 | 回答的问题 |
|------|------|
| WPF 截图（`wpf-v3.9.0/*.png` + `index.html`） | 原版界面长什么样 |
| WPF 文档（`wpf-v3.9.0/操作步骤.md` + `源码映射.md`） | 原版怎么操作、代码在哪 |
| Avalonia 截图 + 摘要（`avalonia/mN-*/*.png` + `*.txt`） | 重构后长什么样、断言通过没 |

## Avalonia 视觉验证不只是 PNG

每个 `avalonia/mN-*/*.txt` 都记录了 headless 视觉测试的三层断言（**不是只 dump 一张图**）：

1. **ItemsSource 内容断言**：列表喂进的数据条数 = 实际条数
2. **可视化树颜色断言**：每个 ListBoxItem 内 Border 的 Background 颜色精确匹配 Converter 预期
3. **diff 语义断言**：选中 X → 弹窗内容 = X 的 diff（按 Added/Removed 着色）

这对应用户明确要求 "不能只截图，要看截图实现的功能对不对"。

## 关键发现（迁移前必读）

- **v3.9.0 核心改动**：AI 辅助界面（WebView2 承载的流式 Markdown 渲染）统一重构——这是 Avalonia 迁移中替换成本最高的区域。
- **最大利好**：Git 引擎 `biturbo`（libgit2 封装）已发布 **Windows / Linux / macOS 三平台原生库**，核心 Git 逻辑**无需重写**。
- **架构接缝已存在**：`ServiceLocator` 已抽象 7 个平台接口，各有一个 `Wpf*` 实现——Avalonia 端只需补 `Avalonia*` 实现。
- **最大替换成本**：WebView2 / AI 渲染、AvalonEdit、OxyPlot.Wpf、主题检测（UISettings + 注册表）、WindowsCredentialManager、Shell 集成、Git 路径硬编码（`git.exe`/`bash.exe`）。
- **UI 工作量**：245 个 WPF `.xaml` 需做语法转换。

## 使用方式

打开 `wpf-v3.9.0/index.html` 浏览原版 WPF 全部截图；重构某界面时，对照 `wpf-v3.9.0/源码映射.md` 第 3 节的代码文件定位，按需阅读对应 `.xaml/.cs` 源码。每完成一个 milestone，对应 `avalonia/mN-*/` 目录会被 headless 视觉测试自动更新。

## 环境

Ubuntu 22.04 沙箱（无图形界面）+ Wine 11 + Xvfb(:99) + 手工拼装的 .NET 10 WindowsDesktop 运行时。可复现步骤见 `wpf-v3.9.0/验证报告.md` 第 3 节。
