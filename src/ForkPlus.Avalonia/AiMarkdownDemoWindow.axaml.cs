using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using ForkPlus.Avalonia.Markdown;

namespace ForkPlus.Avalonia;

/// <summary>
/// P4 演示窗口：用自研零依赖的 <see cref="AvaloniaMarkdownViewer"/> 替代原 WPF 工程的 WebView2 渲染层。
/// 演示：① 流式 AI 文本（StartStreaming/AppendChunk/RenderFinal，对标 AiStreamingWebView 的节流流式渲染）；
/// ② Git mm 参考长文档（含标题/列表/代码块/表格/引用，对标 GitMmReferenceWindow）；
/// ③ 错误兜底面板（对标 AiStreamingWebView.ShowError）；
/// ④ 明暗主题跟随（Application.RequestedThemeVariant，对标 WebView2 的 PreferredColorScheme）。
/// OpenAiService 的纯 HTTP 流式调用（onChunk 回调）平台无关，无需替换——只替换「Markdown→渲染」这一层。
/// </summary>
public partial class AiMarkdownDemoWindow : Window
{
	private const string SampleDoc = @"# git mm 参考

`git mm` 是 ForkPlus 的多仓库管理命令。

## 起步

1. 初始化工作区
2. 添加子仓库
3. 同步上游

## 常用命令

- `git mm start`：开始一个管理会话
- `git mm sync`：同步所有子仓库
- `git mm upload`：上传本地变更

## 示例

```csharp
git mm start --name demo
```

> 提示：执行上传前请先确认已配置凭据。

## 支持矩阵

| 命令   | Windows | macOS | Linux |
|--------|---------|-------|-------|
| start  | 支持    | 支持  | 支持  |
| sync   | 支持    | 支持  | 支持  |
| upload | 支持    | 支持  | 支持  |
";

	public AiMarkdownDemoWindow()
	{
		InitializeComponent();

		StreamButton.Click += OnStream;
		DocButton.Click += (_, _) =>
		{
			Viewer.Markdown = SampleDoc;
			Viewer.ShowContent();
			StatusText.Text = "已渲染 Git mm 参考文档（含标题/列表/代码块/表格/引用）。";
		};
		ErrorButton.Click += (_, _) =>
			Viewer.ShowError("示例错误：无法连接到 AI 服务（演示错误兜底面板，替代 WebView2 的 HTML 错误页）。");
		ThemeButton.Click += OnTheme;
		ClearButton.Click += (_, _) =>
		{
			Viewer.Markdown = string.Empty;
			StatusText.Text = "已清空。";
		};
		Viewer.StreamingProgress += len => StatusText.Text = $"流式接收中：{len} 字符…";
		Viewer.LinkClicked += url => StatusText.Text = $"点击链接：{url}";
	}

	// Avalonia 12：InitializeComponent 必须由代码隐藏提供（内部调用 AvaloniaXamlLoader.Load(this)），
	// 编译期由 XamlX（CompileAvaloniaXaml）把该 Load(this) 调用补丁为编译后的 XAML IL。
	public void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	private async void OnStream(object? sender, RoutedEventArgs e)
	{
		Viewer.StartStreaming();
		Viewer.ShowContent();
		StatusText.Text = "开始模拟流式 AI 输出…";
		string[] chunks =
		{
			"# 提交摘要\n\n",
			"本次变更涉及 **3** 个文件，共 *42* 行。\n\n",
			"## 主要改动\n\n",
			"- 修复登录超时\n- 优化缓存命中率\n- 升级依赖版本\n\n",
			"```csharp\nvar client = new HttpClient();\nawait client.GetAsync(url);\n```\n\n",
			"> 建议：合并前请先做一次完整 review。\n\n",
			"详见 [官方文档](https://example.com)。\n"
		};
		foreach (string chunk in chunks)
		{
			Viewer.AppendChunk(chunk);
			await Task.Delay(350);
		}
		Viewer.RenderFinal();
		Viewer.ShowContent();
		StatusText.Text = "流式渲染完成（渲染由 Avalonia 原生控件树完成，无 WebView2）。";
	}

	private void OnTheme(object? sender, RoutedEventArgs e)
	{
		Application? app = Application.Current;
		if (app == null)
		{
			return;
		}
		app.RequestedThemeVariant = app.RequestedThemeVariant == ThemeVariant.Dark
			? ThemeVariant.Light
			: ThemeVariant.Dark;
		Viewer.UpdateTheme();
	}
}
