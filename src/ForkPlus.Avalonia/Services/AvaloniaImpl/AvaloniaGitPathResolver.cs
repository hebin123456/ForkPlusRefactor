using System;
using System.IO;
using System.Runtime.InteropServices;
using ForkPlus.Services;

namespace ForkPlus.Services.AvaloniaImpl
{
	/// <summary>
	/// 跨平台 Git 工具路径解析实现。
	/// 核心策略：仅 Windows 才追加 .exe 扩展名，其他平台使用无扩展名的命令名（git / sh / bash / git-mm），
	/// 优先从 PATH 解析，回退到 git 同目录或程序基目录。
	/// </summary>
	public class AvaloniaGitPathResolver : IGitPathResolver
	{
		/// <summary>按平台返回可执行文件名：Windows 追加 .exe，其他保持原样。</summary>
		private static string ExeName(string baseName)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				return baseName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
					? baseName
					: baseName + ".exe";
			}
			// 非 Windows：去掉任何残留的 .exe（防御性），保持无扩展名
			return baseName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
				? baseName.Substring(0, baseName.Length - 4)
				: baseName;
		}

		public string GitPath => FindExecutableInPath("git")
			?? Path.Combine(AppContext.BaseDirectory, ExeName("git"));

		public string ShellPath => FindExecutableInPath("sh")
			?? Path.Combine(Path.GetDirectoryName(GitPath) ?? AppContext.BaseDirectory, ExeName("sh"));

		public string BashPath => FindExecutableInPath("bash")
			?? Path.Combine(Path.GetDirectoryName(GitPath) ?? AppContext.BaseDirectory, ExeName("bash"));

		public string GitMmPath => FindExecutableInPath("git-mm")
			?? Path.Combine(Path.GetDirectoryName(GitPath) ?? AppContext.BaseDirectory, ExeName("git-mm"));

		public string AskPassPath => Path.Combine(AppContext.BaseDirectory, ExeName("ForkPlus.AskPass"));

		public string FindExecutableInPath(string baseName)
		{
			var name = ExeName(baseName);
			// 1) 直接能用（已在 PATH 且当前目录）
			if (File.Exists(name))
				return Path.GetFullPath(name);

			// 2) 遍历 PATH 各段
			var pathEnv = Environment.GetEnvironmentVariable("PATH");
			if (!string.IsNullOrEmpty(pathEnv))
			{
				foreach (var dir in pathEnv.Split(Path.PathSeparator))
				{
					if (string.IsNullOrWhiteSpace(dir))
						continue;
					var candidate = Path.Combine(dir.Trim(), name);
					if (File.Exists(candidate))
						return candidate;
				}
			}
			return null;
		}
	}
}
