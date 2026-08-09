using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using ForkPlus.Services;

namespace ForkPlus.Avalonia;

/// <summary>
/// P3 平台适配演示窗口：把原 WPF 工程的三处 Windows 专属能力用跨平台服务重新实现并现场验证：
///   1) 主题检测（SystemThemeHelper）→ IThemeService
///   2) 凭据管理（WindowsCredentialManager）→ ICredentialManager
///   3) 文件对话框（OpenDialog / Windows API Code Pack）→ IFileDialogService
/// </summary>
public partial class PlatformServicesDemoWindow : Window
{
	public PlatformServicesDemoWindow()
	{
		InitializeComponent();

		RefreshThemeLabels();
		// 订阅系统主题变化（对标原 WPF Theme.SubscribeToSystemEvents 的回调链）
		ServiceLocator.Theme.SystemThemeVariantChanged += (s, v) =>
		{
			AppendThemeLog("系统主题变化 → " + v);
			RefreshThemeLabels();
		};

		ThemeDarkBtn.Click += OnThemeDark;
		ThemeLightBtn.Click += OnThemeLight;
		ThemeSystemBtn.Click += OnThemeSystem;

		CredSaveBtn.Click += OnCredSave;
		CredLoadBtn.Click += OnCredLoad;
		CredDeleteBtn.Click += OnCredDelete;
		CredEnumBtn.Click += OnCredEnum;
		CredSshBtn.Click += OnCredSsh;

		FileDirBtn.Click += OnFileDir;
		FileFileBtn.Click += OnFileFile;
		FileExeBtn.Click += OnFileExe;
		FileSaveBtn.Click += OnFileSave;
	}

	// Avalonia 12：InitializeComponent 必须由代码隐藏提供（XamlX 编译后补丁 AvaloniaXamlLoader.Load(this)）
	public void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	// ---- 主题 ----
	private void RefreshThemeLabels()
	{
		ThemeSystemText.Text = ServiceLocator.Theme.GetSystemThemeVariant().ToString();
		ThemeRequestedText.Text = (Application.Current?.RequestedThemeVariant ?? ThemeVariant.Default).ToString();
	}

	private void AppendThemeLog(string line) => ThemeLog.Text = ThemeLog.Text + line + "\n";

	private void OnThemeDark(object? sender, RoutedEventArgs e)
	{
		ServiceLocator.Theme.SetRequestedThemeVariant(ThemeVariant.Dark);
		RefreshThemeLabels();
		AppendThemeLog("请求变体 → Dark");
	}

	private void OnThemeLight(object? sender, RoutedEventArgs e)
	{
		ServiceLocator.Theme.SetRequestedThemeVariant(ThemeVariant.Light);
		RefreshThemeLabels();
		AppendThemeLog("请求变体 → Light");
	}

	private void OnThemeSystem(object? sender, RoutedEventArgs e)
	{
		ServiceLocator.Theme.SetRequestedThemeVariant(ThemeVariant.Default);
		RefreshThemeLabels();
		AppendThemeLog("请求变体 → Default（跟随系统）");
	}

	// ---- 凭据 ----
	private void OnCredSave(object? sender, RoutedEventArgs e)
	{
		try
		{
			ServiceLocator.Credentials.Write(CredTarget.Text, CredUser.Text, CredSecret.Text);
			CredResult.Text = "已写入凭据：\n  " + CredTarget.Text;
		}
		catch (Exception ex)
		{
			CredResult.Text = "写入失败：" + ex.Message;
		}
	}

	private void OnCredLoad(object? sender, RoutedEventArgs e)
	{
		var c = ServiceLocator.Credentials.Read(CredTarget.Text);
		CredResult.Text = c == null
			? "未找到：" + CredTarget.Text
			: $"目标={c.TargetName}\n用户={c.UserName}\n密钥={c.Secret}";
	}

	private void OnCredDelete(object? sender, RoutedEventArgs e)
	{
		var ok = ServiceLocator.Credentials.Delete(CredTarget.Text);
		CredResult.Text = ok ? "已删除：" + CredTarget.Text : "删除失败（不存在）：" + CredTarget.Text;
	}

	private void OnCredEnum(object? sender, RoutedEventArgs e)
	{
		var all = ServiceLocator.Credentials.Enumerate();
		CredResult.Text = $"枚举到 {all.Count} 条凭据：\n" +
			string.Join("\n", all.Select(c => $"  {c.TargetName} | {c.UserName}"));
	}

	private void OnCredSsh(object? sender, RoutedEventArgs e)
	{
		var key = "P3DemoKey_" + Guid.NewGuid().ToString("N").AsSpan(0, 8).ToString();
		const string secret = "s3cr3t";
		ServiceLocator.Credentials.StoreSshPassphrase(key, secret);
		var got = ServiceLocator.Credentials.QuerySshPassphrase(key);
		var ok = got == secret;
		ServiceLocator.Credentials.Delete("fork:" + key);
		CredResult.Text = $"SSH 口令往返（target=fork:{key}）：\n  存储='{secret}'\n  读取='{got}'\n  一致={ok}";
	}

	// ---- 文件对话框 ----
	private async void OnFileDir(object? sender, RoutedEventArgs e)
	{
		var path = await ServiceLocator.FileDialog.SelectDirectoryAsync(this, "选择目录", null);
		FileResult.Text = "目录：" + (path ?? "(已取消)");
	}

	private async void OnFileFile(object? sender, RoutedEventArgs e)
	{
		var path = await ServiceLocator.FileDialog.SelectFileAsync(this, "选择文件", null, "C# 源文件", "*.cs");
		FileResult.Text = "文件：" + (path ?? "(已取消)");
	}

	private async void OnFileExe(object? sender, RoutedEventArgs e)
	{
		var path = await ServiceLocator.FileDialog.SelectExecutableFileAsync(this, "选择可执行文件", null);
		FileResult.Text = "可执行文件：" + (path ?? "(已取消)");
	}

	private async void OnFileSave(object? sender, RoutedEventArgs e)
	{
		var path = await ServiceLocator.FileDialog.SelectFileSaveLocationAsync(this, "保存文件", null, "export.txt");
		FileResult.Text = "保存位置：" + (path ?? "(已取消)");
	}
}
