using Avalonia;
using Avalonia.Headless;
using Avalonia.Skia;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// 自定义 Avalonia Headless 测试启动器：禁用 <c>UseHeadlessDrawing</c>（默认是 stub renderer），
/// 切到真实的 Skia 渲染后端，这样 <see cref="Avalonia.Media.Imaging.RenderTargetBitmap"/> 才能
/// 拿到真实像素并落盘 PNG。
///
/// <para>
/// 选 Skia 而不是 <c>Avalonia.Skia</c> 自带的 GPU 加速分支：CI 沙箱（无显卡）下走 Skia CPU 渲染
/// 足够生成 1100×700 单帧快照，单帧成本 &lt; 200ms。
/// </para>
///
/// <para>
/// 仅影响 <see cref="MainWindowVisualScreenshotTests"/> 一个测试类下的 <c>[AvaloniaFact]</c>，
/// 其它无头行为测试（<c>MainWindowCommitFlowTests</c>、<c>MarkdownViewerHeadlessTests</c> 等）
/// 仍走默认 stub renderer，因为它们只需要断言可视化树 / 控件状态，不需要像素。
/// </para>
/// </summary>
public sealed class SkiaHeadlessTestApp : Application
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<SkiaHeadlessTestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
                ShouldRenderOnUIThread = true,
            });
}
