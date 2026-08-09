using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 跨平台 Git 辅助工具路径解析。
	/// 对标原 WPF 工程 App.xaml.cs 中写死 .exe 的 GitPath / ShellPath / BashPath / GitMmPath
	/// 以及 Consts.ForkPlus.AskPassFilename（"ForkPlus.AskPass.exe"）。
	/// 原实现在非 Windows 平台会因找不到 git.exe / bash.exe 直接报错
	/// （见验证截图 55-git-error-dialog.png）。
	/// </summary>
	public interface IGitPathResolver
	{
		/// <summary>git 可执行文件完整路径（Windows=git.exe，其他=git）。</summary>
		string GitPath { get; }

		/// <summary>sh 可执行文件（Windows 通常 git 同目录的 sh.exe，其他=sh）。</summary>
		string ShellPath { get; }

		/// <summary>bash 可执行文件（Windows=bash.exe，其他=bash）。</summary>
		string BashPath { get; }

		/// <summary>git-mm 可执行文件（Windows=git-mm.exe，其他=git-mm）。</summary>
		string GitMmPath { get; }

		/// <summary>ForkPlus.AskPass 凭据助手完整路径（按平台加 .exe）。</summary>
		string AskPassPath { get; }

		/// <summary>
		/// 在 PATH 环境变量中查找指定可执行文件名，返回第一个匹配的完整路径；未找到返回 null。
		/// 入参不含扩展名时，非 Windows 平台按传入名查找，Windows 平台追加 .exe。
		/// </summary>
		string FindExecutableInPath(string baseName);
	}
}
