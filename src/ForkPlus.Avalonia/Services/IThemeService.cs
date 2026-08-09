using Avalonia.Styling;

namespace ForkPlus.Services
{
	/// <summary>
	/// 跨平台系统主题检测与切换服务。
	/// 原 WPF 工程用三套机制拼出同样的能力，此处统一为一套 Avalonia 原生能力：
	///   - WinRT <c>UISettings.ColorValuesChanged</c>（ForkPlus/UI/SystemThemeHelper.SubscribeToSystemEvents）→ 系统明暗变化事件
	///   - 注册表 <c>HKCU\...\Themes\Personalize\AppsUseLightTheme</c>（ForkPlus/App.xaml.cs GetSystemTheme）→ 系统明暗探测
	///   - <c>Microsoft.Win32.SystemEvents.UserPreferenceChanged</c>（App.SubscribeToUserPreferences）→ 系统偏好变化
	/// 三者合并为：Avalonia 的 <see cref="Avalonia.Styling.PlatformThemeVariantManager"/>（事件 + 当前变体）
	/// 与 <c>Application.RequestedThemeVariant</c>（请求变体）。
	/// </summary>
	public interface IThemeService
	{
		/// <summary>
		/// 系统主题变体变化时触发（对标 WinRT UISettings.ColorValuesChanged / SystemEvents.UserPreferenceChanged
		/// 经 Dispatcher 调 Theme.Refresh 的回调点）。参数为当前系统 <see cref="ThemeVariant"/>。
		/// </summary>
		event EventHandler<ThemeVariant>? SystemThemeVariantChanged;

		/// <summary>
		/// 读取当前系统主题变体（Light / Dark）。
		/// 对标 GetSystemTheme() 读注册表 AppsUseLightTheme，此处由 PlatformThemeVariantManager 提供，跨平台可用。
		/// </summary>
		ThemeVariant GetSystemThemeVariant();

		/// <summary>
		/// 应用请求的主题变体。传 <see cref="ThemeVariant.Default"/> 即「跟随系统」（对标
		/// ForkPlusSettings.FollowSystemTheme 把主题映射为基底 Light/Dark 的逻辑）。
		/// </summary>
		void SetRequestedThemeVariant(ThemeVariant variant);

		/// <summary>
		/// 开始监听系统主题变化（对标 Theme.SubscribeToSystemEvents + App.SubscribeToUserPreferences）。
		/// 此后系统明暗切换会触发 <see cref="SystemThemeVariantChanged"/>。
		/// </summary>
		void StartSystemTracking();
	}
}
