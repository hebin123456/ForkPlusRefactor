using Avalonia.Controls;

namespace ForkPlus.Avalonia.Diff;

/// <summary>
/// 承载 <see cref="DiffView"/> 的窗口（P2 PoC 演示用）。对标 ForkPlus 打开差异面板的窗口。
/// </summary>
public sealed class DiffWindow : Window
{
    public DiffWindow(DiffResult result)
    {
        Title = $"Diff · {result.OldPath} → {result.NewPath}";
        Width = 880;
        Height = 640;
        Content = new DiffView();
        ((DiffView)Content).Load(result);
    }
}
