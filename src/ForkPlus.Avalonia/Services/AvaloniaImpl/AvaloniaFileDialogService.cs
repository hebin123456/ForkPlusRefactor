using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using ForkPlus.Services;

namespace ForkPlus.Services.AvaloniaImpl
{
	/// <summary>
	/// 跨平台文件/目录对话框实现（对标原 WPF 工程 ForkPlus/UI/OpenDialog）。
	/// 用 Avalonia 原生 OpenFolderDialog / OpenFileDialog / SaveFileDialog 替代 Windows API Code Pack，
	/// 方法签名与行为一一对应，并模态于传入的父窗口（ShowAsync(parent)）。
	/// </summary>
	public class AvaloniaFileDialogService : IFileDialogService
	{
		public async Task<string?> SelectDirectoryAsync(Window? parent, string title, string? initialDirectory)
		{
			var dlg = new OpenFolderDialog
			{
				Title = title,
				Directory = initialDirectory
			};
			return await dlg.ShowAsync(parent);
		}

		public async Task<string?> SelectExecutableFileAsync(Window? parent, string title, string? initialDirectory)
			=> await SelectFileAsync(parent, title, initialDirectory, "Applications", "*.exe");

		public async Task<string?> SelectFileAsync(Window? parent, string title, string? initialDirectory, string fileTypeName, string extensionPattern)
		{
			var dlg = new OpenFileDialog
			{
				Title = title,
				Directory = initialDirectory,
				AllowMultiple = false,
				Filters = new List<FileDialogFilter>
				{
					new FileDialogFilter { Name = fileTypeName, Extensions = new List<string> { NormalizeExt(extensionPattern) } }
				}
			};
			var result = await dlg.ShowAsync(parent);
			return result?.FirstOrDefault();
		}

		public async Task<string?> SelectPatchSaveLocationAsync(Window? parent, string title, string? initialDirectory, string defaultFileName)
		{
			var dlg = new SaveFileDialog
			{
				Title = title,
				Directory = initialDirectory,
				InitialFileName = defaultFileName,
				DefaultExtension = "patch",
				Filters = new List<FileDialogFilter>
				{
					new FileDialogFilter { Name = "Patches", Extensions = new List<string> { "patch" } }
				}
			};
			var path = await dlg.ShowAsync(parent);
			if (path != null && !path.EndsWith(".patch", StringComparison.OrdinalIgnoreCase))
				path += ".patch";
			return path;
		}

		public async Task<string?> SelectFileSaveLocationAsync(Window? parent, string title, string? initialDirectory, string defaultFileName)
		{
			var ext = Path.GetExtension(defaultFileName).TrimStart('.');
			var dlg = new SaveFileDialog
			{
				Title = title,
				Directory = initialDirectory,
				InitialFileName = defaultFileName,
				DefaultExtension = ext,
				Filters = new List<FileDialogFilter>
				{
					new FileDialogFilter { Name = $"*{ext} files", Extensions = new List<string> { ext } }
				}
			};
			return await dlg.ShowAsync(parent);
		}

		private static string NormalizeExt(string pattern)
		{
			var s = pattern.Trim();
			if (s.StartsWith("*."))
				s = s.Substring(2);
			else if (s.StartsWith("."))
				s = s.Substring(1);
			return s;
		}
	}
}
