using System;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using ForkPlus.Services;

namespace ForkPlus.Services.AvaloniaImpl
{
	/// <summary>
	/// 跨平台主题服务实现。
	/// 用 Avalonia 原生 <see cref="Avalonia.Styling.PlatformThemeVariantManager"/> 一次性替代原 WPF 工程的三套机制：
	///   WinRT UISettings.ColorValuesChanged（SystemThemeHelper）+ 注册表 AppsUseLightTheme（App.GetSystemTheme）
	///   + SystemEvents.UserPreferenceChanged（App.SubscribeToUserPreferences）。
	/// 事件回调统一在 UI 线程派发（对标原 UiSettings_ColorValuesChanged 经 Dispatcher.Invoke 调 Theme.Refresh）。
	/// </summary>
	public class AvaloniaThemeService : IThemeService
	{
		public event EventHandler<ThemeVariant>? SystemThemeVariantChanged;

		public ThemeVariant GetSystemThemeVariant()
		{
			var v = PlatformThemeVariantManager.Current?.ThemeVariant;
			if (v == null)
				v = Application.Current?.ActualThemeVariant ?? ThemeVariant.Light;
			return v;
		}

		public void SetRequestedThemeVariant(ThemeVariant variant)
		{
			if (Application.Current != null)
				Application.Current.RequestedThemeVariant = variant;
		}

		public void StartSystemTracking()
		{
			PlatformThemeVariantManager.ThemeVariantChanged += OnPlatformThemeVariantChanged;
		}

		private void OnPlatformThemeVariantChanged(object? sender, EventArgs e)
		{
			var variant = GetSystemThemeVariant();
			// 在 UI 线程回调，对标原 WPF：UiSettings_ColorValuesChanged -> Dispatcher.Invoke(Theme.Refresh)
			Dispatcher.UIThread.Post(() => SystemThemeVariantChanged?.Invoke(this, variant));
		}
	}
}
