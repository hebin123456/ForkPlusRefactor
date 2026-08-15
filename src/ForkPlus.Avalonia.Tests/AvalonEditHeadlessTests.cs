using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using AvaloniaEdit;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M0 验收：AvalonEdit 集成。原 WPF 工程用 ICSharpCode.AvalonEdit.dll 做差异视图，
/// 跨平台迁移用 Avalonia.AvaloniaEdit（同 API 的 Avalonia 移植）。本测试在 headless 上下文里
/// 构造 TextEditor 并验证 Text 属性可写入/读回，证明该控件能在 net10.0 + Avalonia 12 栈上成功
/// 实例化——即差异视图的底层控件可用（M0 的 AvalonEdit 接线完成）。
/// </summary>
[Collection(nameof(AvaloniaHeadlessCollection))]
public class AvalonEditHeadlessTests
{
	[AvaloniaFact]
	public void TextEditor_CanBeConstructed_And_TextRoundTrips()
	{
		// [AvaloniaFact] 已拉起 headless Application；直接构造 AvaloniaEdit 的 TextEditor
		// （对应 WPF 的 ICSharpCode.AvalonEdit.TextEditor），证明该控件在 Avalonia 12 下可实例化。
		var editor = new AvaloniaEdit.TextEditor();

		const string src = "diff --git a/file.cs b/file.cs\n- old line\n+ new line\n";
		editor.Text = src;

		// Text 属性写入/读回一致，说明底层 TextDocument 已建立（差异渲染的数据层就绪）
		Assert.Equal(src, editor.Text);
		Assert.IsAssignableFrom<Control>(editor);
	}
}
