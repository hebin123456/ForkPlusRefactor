using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
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
}
