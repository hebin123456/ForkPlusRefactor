using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 平台无关的 Toast 通知服务接口。
	/// WPF 实现使用 WinRT ToastNotifications，Avalonia 实现使用各平台原生通知或 in-app 浮层。
	/// 入参 xmlPayload 为原工程构造的 WinRT Toast XML（见 ForkPlus/Accounts/NotificationManager.SendWindowsNotification）。
	/// </summary>
	public interface IToastNotificationService
	{
		void Show(string xmlPayload);
	}
}
