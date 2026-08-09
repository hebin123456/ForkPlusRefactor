using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ForkPlus.Services;

namespace ForkPlus.Services.AvaloniaImpl
{
	/// <summary>
	/// 跨平台窗口管理。对标 WpfWindowManagerService（使用 Application.Current.Windows）。
	/// TryActivateWindowByTitle 泛化为「按 Title 匹配任意窗口」，
	/// 比原 WPF 硬判断 AiCodeReviewWindow 类型更干净，且保留相同调用语义
	/// （见 ForkPlus/Accounts/NotificationManager.FindAiCodeReviewWindowAndActivate）。
	/// </summary>
	public class AvaloniaWindowManagerService : IWindowManagerService
	{
		public void ActivateAndShowNotifications()
		{
			// MVP：通知面板尚未迁移，预留。
			// 原 WPF 行为见 ForkPlus.UI.Dialogs.MainWindow.ShowNotificationManager()
		}

		public bool TryActivateWindowByTitle(string title)
		{
			if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				foreach (var window in desktop.Windows)
				{
					if (window.Title == title)
					{
						window.Activate();
						return true;
					}
				}
			}
			return false;
		}

		public void DispatchToUiThread(Action action) => Dispatcher.UIThread.Post(action);
	}
}
