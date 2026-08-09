using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 轻量级服务定位器，用于解耦业务层对 WPF 的直接依赖。
	/// 迁移完成后可替换为正式 DI 容器。
	/// 原工程（ForkPlus/Services/ServiceLocator.cs）直接依赖 WPF 类型；
	/// 此处为跨平台版本，仅依赖本文件定义的接口，由各平台（Avalonia/WPF）注入实现。
	/// </summary>
	public static class ServiceLocator
	{
		public static IDispatcher Dispatcher { get; private set; }
		public static IDesignModeService DesignMode { get; private set; }
		public static IAppContext AppContext { get; private set; }
		public static IClipboardService Clipboard { get; private set; }
		public static ITimerService Timer { get; private set; }
		public static IToastNotificationService Toast { get; private set; }
		public static IWindowManagerService WindowManager { get; private set; }
		public static IThemeService Theme { get; private set; }
		public static ICredentialManager Credentials { get; private set; }
		public static IFileDialogService FileDialog { get; private set; }
		public static IGitPathResolver GitPathResolver { get; private set; }
		public static IShellIntegrationService Shell { get; private set; }

		public static bool IsInitialized { get; private set; }

		public static void Initialize(
			IDispatcher dispatcher,
			IDesignModeService designMode,
			IAppContext appContext,
			IClipboardService clipboard,
			ITimerService timer = null,
			IToastNotificationService toast = null,
			IWindowManagerService windowManager = null,
			IThemeService theme = null,
			ICredentialManager credentials = null,
			IFileDialogService fileDialog = null,
			IGitPathResolver gitPathResolver = null,
			IShellIntegrationService shell = null)
		{
			Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
			DesignMode = designMode ?? throw new ArgumentNullException(nameof(designMode));
			AppContext = appContext ?? throw new ArgumentNullException(nameof(appContext));
			Clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
			Timer = timer;
			Toast = toast;
			WindowManager = windowManager;
			Theme = theme;
			Credentials = credentials;
			FileDialog = fileDialog;
			GitPathResolver = gitPathResolver;
			Shell = shell;
			IsInitialized = true;
		}

		public static void Reset()
		{
			Dispatcher = null;
			DesignMode = null;
			AppContext = null;
			Clipboard = null;
			WindowManager = null;
			Theme = null;
			Credentials = null;
			FileDialog = null;
			GitPathResolver = null;
			Shell = null;
			IsInitialized = false;
		}
	}
}
