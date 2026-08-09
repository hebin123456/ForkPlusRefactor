using System;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using ForkPlus.Services;

namespace ForkPlus.Services.AvaloniaImpl
{
	/// <summary>
	/// 跨平台主题服务实现。
	/// 用 Avalonia 原生能力一次性替代原 WPF 工程的三套机制：
	///   WinRT UISettings.ColorValuesChanged（SystemThemeHelper）+ 注册表 AppsUseLightTheme（App.GetSystemTheme）
	///   + SystemEvents.UserPreferenceChanged（App.SubscribeToUserPreferences）。
	/// 具体映射：
	///   - 读取系统明暗 → Application.PlatformSettings.GetColorValues().ThemeVariant（跨平台，对标注册表探测）
	///   - 系统主题变化事件 → Application.ActualThemeVariantChanged（跟随系统时随 OS 变化）+ IPlatformSettings.ColorValuesChanged
	///   - 应用请求变体 → Application.RequestedThemeVariant（设为 ThemeVariant.Default 即「跟随系统」）
	/// （Avalonia 12 的 PlatformThemeVariantManager 已不存在，系统主题改由 IPlatformSettings 暴露。）
	/// </summary>
	public class AvaloniaThemeService : IThemeService
	{
		public event EventHandler<ThemeVariant>? SystemThemeVariantChanged;

		public ThemeVariant GetSystemThemeVariant()
		{
			// 读取 OS 系统主题变体（对标原 GetSystemTheme 读注册表 AppsUseLightTheme）
			var platform = Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant;
			return platform switch
			{
				PlatformThemeVariant.Light => ThemeVariant.Light,
				PlatformThemeVariant.Dark => ThemeVariant.Dark,
				_ => Application.Current?.ActualThemeVariant ?? ThemeVariant.Light
			};
		}

		public void SetRequestedThemeVariant(ThemeVariant variant)
		{
			if (Application.Current != null)
				Application.Current.RequestedThemeVariant = variant;
		}

		public void StartSystemTracking()
		{
			if (Application.Current is { } app)
			{
				app.ActualThemeVariantChanged += OnActualThemeVariantChanged;
				var settings = app.PlatformSettings;
				if (settings != null)
					settings.ColorValuesChanged += OnColorValuesChanged;
			}
		}

		private void OnActualThemeVariantChanged(object? sender, EventArgs e) => RaiseEvent();

		private void OnColorValuesChanged(object? sender, PlatformColorValues e) => RaiseEvent();

		private void RaiseEvent()
		{
			// 在 UI 线程回调，对标原 WPF：UiSettings_ColorValuesChanged -> Dispatcher.Invoke(Theme.Refresh)
			var variant = GetSystemThemeVariant();
			Dispatcher.UIThread.Post(() => SystemThemeVariantChanged?.Invoke(this, variant));
		}
	}
}
