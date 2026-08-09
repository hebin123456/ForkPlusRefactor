using System;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using ForkPlus.Avalonia.Chart;
using ForkPlus.Services;

namespace ForkPlus.Avalonia;

/// <summary>
/// P3 平台适配演示窗口：把原 WPF 工程的 Windows 专属能力用跨平台服务重新实现并现场验证：
///   1) 主题检测（SystemThemeHelper）→ IThemeService
///   2) 凭据管理（WindowsCredentialManager）→ ICredentialManager
///   3) 文件对话框（OpenDialog / Windows API Code Pack）→ IFileDialogService
///   4) Git 工具路径（App.xaml.cs 写死 .exe）→ IGitPathResolver
///   5) Shell 集成（shell32 AppUserModelID / CodePack 跳转列表）→ IShellIntegrationService
///   6) 通知（WinRT Toast）→ IToastNotificationService（原生通道 + in-app 兜底）
///   7) 图表（OxyPlot.Wpf）→ 原生 Avalonia DrawingContext 渲染（OxyPlot 同构模型）
/// </summary>
public partial class PlatformServicesDemoWindow : Window
{
	private static readonly Color[] Palette =
	{
		Color.Parse("#FF9502"), Color.Parse("#64DA38"), Color.Parse("#1CADF8"),
		Color.Parse("#FF3B30"), Color.Parse("#A2845E"), Color.Parse("#CB73E1"),
		Color.Parse("#FFCC00"), Color.Parse("#8E8E91"), Color.Parse("#FF2968"),
		Color.Parse("#30D5C8"), Color.Parse("#5856D6"), Color.Parse("#B4D435")
	};

	public PlatformServicesDemoWindow()
	{
		InitializeComponent();

		RefreshThemeLabels();
		ServiceLocator.Theme.SystemThemeVariantChanged += (s, v) =>
		{
			AppendThemeLog("系统主题变化 → " + v);
			RefreshThemeLabels();
		};

		ThemeDarkBtn.Click += OnThemeDark;
		ThemeLightBtn.Click += OnThemeLight;
		ThemeSystemBtn.Click += OnThemeSystem;

		CredSaveBtn.Click += OnCredSave;
		CredLoadBtn.Click += OnCredLoad;
		CredDeleteBtn.Click += OnCredDelete;
		CredEnumBtn.Click += OnCredEnum;
		CredSshBtn.Click += OnCredSsh;

		FileDirBtn.Click += OnFileDir;
		FileFileBtn.Click += OnFileFile;
		FileExeBtn.Click += OnFileExe;
		FileSaveBtn.Click += OnFileSave;

		// Git 路径
		GitRefreshBtn.Click += (_, _) => RefreshGitPaths();
		GitTestBtn.Click += OnGitTest;
		RefreshGitPaths();

		// Shell
		ShellSetBtn.Click += OnShellSet;
		ShellRecentBtn.Click += OnShellRecent;

		// Toast
		ToastSendBtn.Click += OnToastSend;

		// 图表
		ChartType.SelectionChanged += (_, _) => UpdateChart();
		UpdateChart();
	}

	// Avalonia 12：InitializeComponent 必须由代码隐藏提供（XamlX 编译后补丁 AvaloniaXamlLoader.Load(this)）
	public void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	// ---- 主题 ----
	private void RefreshThemeLabels()
	{
		ThemeSystemText.Text = ServiceLocator.Theme.GetSystemThemeVariant().ToString();
		ThemeRequestedText.Text = (Application.Current?.RequestedThemeVariant ?? ThemeVariant.Default).ToString();
	}

	private void AppendThemeLog(string line) => ThemeLog.Text = ThemeLog.Text + line + "\n";

	private void OnThemeDark(object? sender, RoutedEventArgs e)
	{
		ServiceLocator.Theme.SetRequestedThemeVariant(ThemeVariant.Dark);
		RefreshThemeLabels();
		AppendThemeLog("请求变体 → Dark");
	}

	private void OnThemeLight(object? sender, RoutedEventArgs e)
	{
		ServiceLocator.Theme.SetRequestedThemeVariant(ThemeVariant.Light);
		RefreshThemeLabels();
		AppendThemeLog("请求变体 → Light");
	}

	private void OnThemeSystem(object? sender, RoutedEventArgs e)
	{
		ServiceLocator.Theme.SetRequestedThemeVariant(ThemeVariant.Default);
		RefreshThemeLabels();
		AppendThemeLog("请求变体 → Default（跟随系统）");
	}

	// ---- 凭据 ----
	private void OnCredSave(object? sender, RoutedEventArgs e)
	{
		try
		{
			ServiceLocator.Credentials.Write(CredTarget.Text, CredUser.Text, CredSecret.Text);
			CredResult.Text = "已写入凭据：\n  " + CredTarget.Text;
		}
		catch (Exception ex)
		{
			CredResult.Text = "写入失败：" + ex.Message;
		}
	}

	private void OnCredLoad(object? sender, RoutedEventArgs e)
	{
		var c = ServiceLocator.Credentials.Read(CredTarget.Text);
		CredResult.Text = c == null
			? "未找到：" + CredTarget.Text
			: $"目标={c.TargetName}\n用户={c.UserName}\n密钥={c.Secret}";
	}

	private void OnCredDelete(object? sender, RoutedEventArgs e)
	{
		var ok = ServiceLocator.Credentials.Delete(CredTarget.Text);
		CredResult.Text = ok ? "已删除：" + CredTarget.Text : "删除失败（不存在）：" + CredTarget.Text;
	}

	private void OnCredEnum(object? sender, RoutedEventArgs e)
	{
		var all = ServiceLocator.Credentials.Enumerate();
		CredResult.Text = $"枚举到 {all.Count} 条凭据：\n" +
			string.Join("\n", all.Select(c => $"  {c.TargetName} | {c.UserName}"));
	}

	private void OnCredSsh(object? sender, RoutedEventArgs e)
	{
		var key = "P3DemoKey_" + Guid.NewGuid().ToString("N").AsSpan(0, 8).ToString();
		const string secret = "s3cr3t";
		ServiceLocator.Credentials.StoreSshPassphrase(key, secret);
		var got = ServiceLocator.Credentials.QuerySshPassphrase(key);
		var ok = got == secret;
		ServiceLocator.Credentials.Delete("fork:" + key);
		CredResult.Text = $"SSH 口令往返（target=fork:{key}）：\n  存储='{secret}'\n  读取='{got}'\n  一致={ok}";
	}

	// ---- 文件对话框 ----
	private async void OnFileDir(object? sender, RoutedEventArgs e)
	{
		var path = await ServiceLocator.FileDialog.SelectDirectoryAsync(this, "选择目录", null);
		FileResult.Text = "目录：" + (path ?? "(已取消)");
	}

	private async void OnFileFile(object? sender, RoutedEventArgs e)
	{
		var path = await ServiceLocator.FileDialog.SelectFileAsync(this, "选择文件", null, "C# 源文件", "*.cs");
		FileResult.Text = "文件：" + (path ?? "(已取消)");
	}

	private async void OnFileExe(object? sender, RoutedEventArgs e)
	{
		var path = await ServiceLocator.FileDialog.SelectExecutableFileAsync(this, "选择可执行文件", null);
		FileResult.Text = "可执行文件：" + (path ?? "(已取消)");
	}

	private async void OnFileSave(object? sender, RoutedEventArgs e)
	{
		var path = await ServiceLocator.FileDialog.SelectFileSaveLocationAsync(this, "保存文件", null, "export.txt");
		FileResult.Text = "保存位置：" + (path ?? "(已取消)");
	}

	// ---- Git 路径 ----
	private void RefreshGitPaths()
	{
		var r = ServiceLocator.GitPathResolver;
		GitResult.Text =
			$"GitPath     = {r.GitPath}\n" +
			$"ShellPath   = {r.ShellPath}\n" +
			$"BashPath    = {r.BashPath}\n" +
			$"GitMmPath   = {r.GitMmPath ?? "(未找到)"}\n" +
			$"AskPassPath = {r.AskPassPath}\n" +
			$"\n运行平台 = {(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows(.exe)" : "非 Windows(无扩展名)")}";
	}

	private void OnGitTest(object? sender, RoutedEventArgs e)
	{
		var name = GitTestName.Text?.Trim();
		if (string.IsNullOrEmpty(name))
		{
			GitTestResult.Text = "请输入要查找的文件名（如 git / sh / bash）。";
			return;
		}
		var found = ServiceLocator.GitPathResolver.FindExecutableInPath(name);
		GitTestResult.Text = found == null ? $"PATH 中未找到：{name}" : $"找到：{found}";
	}

	// ---- Shell ----
	private void OnShellSet(object? sender, RoutedEventArgs e)
	{
		var id = ShellAppId.Text;
		ServiceLocator.Shell.SetAppUserModelId(id);
		ShellResult.Text =
			$"JumpListSupported = {ServiceLocator.Shell.JumpListSupported}\n" +
			$"已设置 AppUserModelID = {id}\n" +
			(ServiceLocator.Shell.JumpListSupported
				? "（Windows：已调用 shell32 SetCurrentProcessExplicitAppUserModelID）"
				: "（非 Windows：安全降级为 no-op）");
	}

	private void OnShellRecent(object? sender, RoutedEventArgs e)
	{
		var sample = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ForkPlusDemo.txt");
		try { System.IO.File.WriteAllText(sample, "demo"); } catch { }
		ServiceLocator.Shell.AddRecentFile(sample);
		ShellResult.Text =
			$"JumpListSupported = {ServiceLocator.Shell.JumpListSupported}\n" +
			$"已加入最近文件：{sample}\n" +
			(ServiceLocator.Shell.JumpListSupported
				? "（Windows：已调用 shell32 SHAddToRecentDocs）"
				: "（非 Windows：安全降级为 no-op）");
	}

	// ---- Toast ----
	private void OnToastSend(object? sender, RoutedEventArgs e)
	{
		var title = EscapeXml(ToastTitle.Text);
		var body = EscapeXml(ToastBody.Text);
		// 与原工程 NotificationManager.SendWindowsNotification 同构的 WinRT Toast XML
		var xml = "<?xml version=\"1.0\" encoding =\"utf-8\" ?>\n" +
			"<toast>\n<audio silent=\"true\"/>\n<visual>\n" +
			"    <binding template=\"ToastGeneric\">\n" +
			$"        <text hint-maxLines=\"1\">{title}</text>\n" +
			$"        <text>{body}</text>\n" +
			"    </binding>\n</visual>\n</toast>\n";
		ServiceLocator.Toast.Show(xml);

		var channel = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux → notify-send"
			: RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS → osascript"
			: "Windows / 其他 → in-app 浮层（保底）";
		ToastResult.Text = $"已发送 WinRT XML 通知。\n派发通道：{channel}\n（原生命令缺失时自动回退 in-app 浮层）";
	}

	private static string EscapeXml(string s) =>
		s?.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;") ?? "";

	// ---- 图表 ----
	private void UpdateChart()
	{
		var idx = ChartType.SelectedIndex;
		PlotModel model;
		if (idx == 1)
			model = BuildBarModel();
		else if (idx == 2)
			model = BuildPieModel();
		else
			model = BuildLineModel();
		Chart.Model = model;
	}

	private static PlotModel BuildLineModel()
	{
		var s = new LineSeries { Title = "每月提交", Color = Palette[2] };
		for (int i = 0; i < 12; i++)
			s.Points.Add(new DataPoint(i, new[] { 42, 55, 38, 61, 73, 49, 88, 95, 67, 81, 59, 102 }[i]));
		return new PlotModel { Title = "每月提交趋势", Series = { s } };
	}

	private static PlotModel BuildBarModel()
	{
		var s = new BarSeries { Title = "每周提交", Color = Palette[2] };
		var days = new[] { "一", "二", "三", "四", "五", "六", "日" };
		var vals = new[] { 120, 135, 128, 142, 160, 90, 70 };
		for (int i = 0; i < 7; i++)
		{
			s.Values.Add(vals[i]);
			s.Categories.Add(days[i]);
		}
		return new PlotModel { Title = "每周提交分布", Series = { s } };
	}

	private static PlotModel BuildPieModel()
	{
		var s = new PieSeries { Title = "语言占比" };
		var langs = new[] { ("C#", 8200.0), ("XAML", 4100.0), ("C++", 2600.0), ("Python", 1500.0), ("其他", 900.0) };
		for (int i = 0; i < langs.Length; i++)
			s.Slices.Add(new PieSlice(langs[i].Item1, langs[i].Item2, Palette[i % Palette.Length]));
		return new PlotModel { Title = "代码语言占比", Series = { s } };
	}
}
