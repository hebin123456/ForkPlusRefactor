using Avalonia.Controls;

namespace ForkPlus.Avalonia.Diff;

/// <summary>
/// 承载 <see cref="DiffView"/> 的窗口（P2 PoC + M3 演示用）。
/// 对标 ForkPlus 打开差异面板的窗口。原 WPF 在主窗口下方编辑区显示；Avalonia 端
/// 简化成独立窗口（对标 DiffTab 标签页），便于 headless 测试直接断言新窗口的 DiffView。
/// </summary>
public sealed class DiffWindow : Window
{
    public DiffWindow(DiffResult result)
    {
        Title = $"Diff · {result.OldPath} → {result.NewPath}";
        Width = 880;
        Height = 640;
        var view = new DiffView();
        Content = view;
        view.Load(result);
        DiffView = view;
    }

    /// <summary>承载的 <see cref="DiffView"/>，供测试断言已加载的 diff。</summary>
    public DiffView DiffView { get; }
}
