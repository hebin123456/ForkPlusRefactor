using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ForkPlus.Avalonia.Markdown;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// P5 核心：用 Avalonia Headless（替代原 WPF 工程的 FlaUI）在真实 Avalonia 上下文里构造
/// <see cref="AvaloniaMarkdownViewer"/>（P4 交付物），断言其可视化树，验证该自研渲染控件在
/// 无头环境下可用。[AvaloniaFact] 自动拉起 headless Application，无需启动完整 App。
/// </summary>
public class MarkdownViewerHeadlessTests
{
	[AvaloniaFact]
	public void HeadlessApp_IsInitialized()
	{
		// [AvaloniaFact] 应已拉起 headless Application，使 Rebuild() 中的 Application.Current 可用
		Assert.NotNull(Application.Current);
	}

	[AvaloniaFact]
	public void Viewer_Renders_Blocks_Into_InnerStackPanel()
	{
		var viewer = new AvaloniaMarkdownViewer();
		viewer.Markdown = "# Title\n\nHello world\n\n```csharp\nvar x = 1;\n```";
		var stack = GetInnerStack(viewer);
		// 标题 + 段落 + 代码块 = 3 个块级控件
		Assert.Equal(3, stack.Children.Count);
	}

	[AvaloniaFact]
	public void Viewer_Renders_CodeBlock_As_Border()
	{
		var viewer = new AvaloniaMarkdownViewer();
		viewer.Markdown = "```js\nconsole.log(1);\n```";
		var stack = GetInnerStack(viewer);
		Assert.Contains(stack.Children, c => c is Border);
	}

	[AvaloniaFact]
	public void Viewer_Renders_Table_As_Grid()
	{
		var viewer = new AvaloniaMarkdownViewer();
		viewer.Markdown = "| A | B |\n| --- | --- |\n| 1 | 2 |";
		var stack = GetInnerStack(viewer);
		Assert.Contains(stack.Children, c => c is Grid);
	}

	[AvaloniaFact]
	public void Viewer_ShowError_Displays_Message_And_Hides_Content()
	{
		var viewer = new AvaloniaMarkdownViewer();
		viewer.Markdown = "# ok";
		viewer.ShowError("boom");
		var grid = (Grid)viewer.Content!;
		var fallback = (TextBlock)grid.Children[2];
		var scroll = (ScrollViewer)grid.Children[0];
		Assert.Equal("boom", fallback.Text);
		Assert.True(fallback.IsVisible);
		Assert.False(scroll.IsVisible);
	}

	// Content(Grid) -> [ScrollViewer(_scroll), ProgressBar(_busy), TextBlock(_fallback)]
	// -> ScrollViewer.Content = StackPanel(_stack，块级控件容器)
	private static StackPanel GetInnerStack(AvaloniaMarkdownViewer viewer)
	{
		var grid = (Grid)viewer.Content!;
		var scroll = (ScrollViewer)grid.Children[0];
		return (StackPanel)scroll.Content!;
	}
}
