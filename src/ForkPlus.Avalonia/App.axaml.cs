using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ForkPlus.Services;
using ForkPlus.Services.AvaloniaImpl;

namespace ForkPlus.Avalonia;

public partial class App : Application
{
	public override void Initialize()
	{
		InitializeComponent();
	}

	// Avalonia 12：InitializeComponent 必须由代码隐藏提供（内部调用 AvaloniaXamlLoader.Load(this)），
	// 编译期由 XamlX（CompileAvaloniaXaml）把该 Load(this) 调用补丁为编译后的 XAML IL。
	public void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		// 初始化跨平台服务定位器（对标原 WPF 工程 ServiceLocator.Initialize）。
		// 业务层（未来从 ForkPlus 搬运的 ViewModel / Commands）通过 ServiceLocator 取服务，零改动。
		if (!ServiceLocator.IsInitialized)
		{
			var theme = new AvaloniaThemeService();
			ServiceLocator.Initialize(
				dispatcher: new AvaloniaDispatcher(),
				designMode: new AvaloniaDesignModeService(),
				appContext: new AvaloniaAppContext(),
				clipboard: new AvaloniaClipboardService(),
				timer: new AvaloniaTimerService(),
				toast: new AvaloniaToastNotificationService(),
				windowManager: new AvaloniaWindowManagerService(),
				theme: theme,
				credentials: new AvaloniaCredentialManager(),
				fileDialog: new AvaloniaFileDialogService());
			// 启动系统主题跟踪（对标原 WPF Theme.SubscribeToSystemEvents + App.SubscribeToUserPreferences）
			theme.StartSystemTracking();
		}

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = new MainWindow();
		}

		base.OnFrameworkInitializationCompleted();
	}
}
