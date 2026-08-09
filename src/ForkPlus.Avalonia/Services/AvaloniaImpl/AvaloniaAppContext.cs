using System;
using ForkPlus.Services;

namespace ForkPlus.Services.AvaloniaImpl
{
	/// <summary>
	/// 跨平台应用上下文实现。
	/// 行为严格对标原 WPF 工程 ForkPlus/App.xaml.cs 的静态路径约定，
	/// 以保证迁移后用户数据目录（LocalApplicationData/ForkPlus、ForkPlusData）保持不变：
	///   ForkDirectoryPath      = LocalApplicationData/ForkPlus
	///   ForkDataDirectoryPath  = LocalApplicationData/ForkPlusData
	///   RepositoriesFilePath   = ForkDataDirectoryPath/repositories.toml
	///   OSVersion              = Environment.OSVersion.Version
	/// </summary>
	public class AvaloniaAppContext : IAppContext
	{
		private static string LocalAppData =>
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

		public string AppDataDirectory => System.IO.Path.Combine(LocalAppData, "ForkPlus");

		public string ForkDataDirectoryPath => System.IO.Path.Combine(LocalAppData, "ForkPlusData");

		public string RepositoriesFilePath =>
			System.IO.Path.Combine(ForkDataDirectoryPath, "repositories.toml");

		public Version OSVersion => Environment.OSVersion.Version;

		public void Shutdown()
		{
			if (Avalonia.Application.Current?.ApplicationLifetime
				is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lifetime)
			{
				lifetime.Shutdown();
			}
			else
			{
				Environment.Exit(0);
			}
		}
	}
}
