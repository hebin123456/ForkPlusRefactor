using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ForkPlus.Services;
using ForkPlus.Services.AvaloniaImpl;

namespace ForkPlus.Avalonia;

public partial class App : Application
{
	public override void Initialize()
	{
		InitializeComponent();
	}

	public override void OnFrameworkInitializationCompleted()
	{
		// 初始化跨平台服务定位器（对标原 WPF 工程 ServiceLocator.Initialize）。
		// 业务层（未来从 ForkPlus 搬运的 ViewModel / Commands）通过 ServiceLocator 取服务，零改动。
		if (!ServiceLocator.IsInitialized)
		{
			ServiceLocator.Initialize(
				dispatcher: new AvaloniaDispatcher(),
				designMode: new AvaloniaDesignModeService(),
				appContext: new AvaloniaAppContext(),
				clipboard: new AvaloniaClipboardService(),
				timer: new AvaloniaTimerService(),
				toast: new AvaloniaToastNotificationService(),
				windowManager: new AvaloniaWindowManagerService());
		}

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = new MainWindow();
		}

		base.OnFrameworkInitializationCompleted();
	}
}
