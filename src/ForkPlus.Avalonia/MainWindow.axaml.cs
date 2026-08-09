using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ForkPlus.Avalonia.Diff;
using ForkPlus.Services;

namespace ForkPlus.Avalonia;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		var ac = ServiceLocator.AppContext;
		ServicesText.Text =
			"已接入的跨平台服务（ServiceLocator）：\n" +
			$"  IAppContext.AppDataDirectory     = {ac.AppDataDirectory}\n" +
			$"  IAppContext.ForkDataDirectoryPath = {ac.ForkDataDirectoryPath}\n" +
			$"  IAppContext.RepositoriesFilePath  = {ac.RepositoriesFilePath}\n" +
			$"  IAppContext.OSVersion            = {ac.OSVersion}\n" +
			"  IDispatcher / IClipboardService / ITimerService /\n" +
			"  IToastNotificationService / IWindowManagerService / IDesignModeService 均已注册";

		CopyButton.Click += OnCopyClicked;
		ToastButton.Click += OnToastClicked;
		DiffButton.Click += OnDiffClicked;
		PlatformButton.Click += OnPlatformClicked;
	}

	// Avalonia 12：InitializeComponent 必须由代码隐藏提供（内部调用 AvaloniaXamlLoader.Load(this)），
	// 编译期由 XamlX（CompileAvaloniaXaml）把该 Load(this) 调用补丁为编译后的 XAML IL。
	public void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	private void OnCopyClicked(object? sender, RoutedEventArgs e)
	{
		ServiceLocator.Clipboard.SetText("Hello from Avalonia ForkPlus!");
		StatusText.Text = "已通过 IClipboardService 写入剪贴板。";
	}

	private void OnToastClicked(object? sender, RoutedEventArgs e)
	{
		// 与 ForkPlus/Accounts/NotificationManager.SendWindowsNotification 构造的 WinRT Toast XML 完全一致
		ServiceLocator.Toast?.Show(
			"<toast><visual><binding template=\"ToastGeneric\">" +
			"<text>ForkPlus</text>" +
			"<text>Avalonia 通知服务已接入（WinRT Toast XML 被解析显示）</text>" +
			"</binding></visual></toast>");
		StatusText.Text = "已通过 IToastNotificationService 显示 in-app 浮层。";
	}

	// P2 PoC：用跨平台 LCS 算法（对标 ForkPlus 经 biturbo 计算的 BtPatchToken）生成
	// DiffResult，再交给 AvaloniaEdit 视图渲染带色差异。biturbo 为原生 Windows 库，
	// 此处用同构的纯托管实现以便跨平台运行。
	private void OnDiffClicked(object? sender, RoutedEventArgs e)
	{
		const string oldText =
@"public int Add(int a, int b)
{
    return a + b;
}

public void Log(string message)
{
    Console.WriteLine(message);
}";
		const string newText =
@"public int Add(int a, int b)
{
    // 支持更大的数值范围
    return checked(a + b);
}

public void Log(string message)
{
    Logger.Info(message);
}

public void Reset()
{
    Logger.Clear();
}";
		var result = LineDiff.Compute(oldText, newText, "a/Calculator.cs", "b/Calculator.cs");
		new DiffWindow(result).Show();
		StatusText.Text = $"已打开差异对比：{result.Lines.Count} 行（绿=新增，红=删除）。";
	}

	// P3：打开平台服务演示窗口，验证主题/凭据/文件对话框三个跨平台服务
	// （分别替代原 WPF 的 SystemThemeHelper / WindowsCredentialManager / OpenDialog(CodePack)）。
	private void OnPlatformClicked(object? sender, RoutedEventArgs e)
	{
		new PlatformServicesDemoWindow().Show();
		StatusText.Text = "已打开平台服务 Demo (P3)：主题检测 / 凭据管理 / 文件对话框。";
	}
}
