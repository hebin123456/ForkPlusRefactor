using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using ForkPlus.Services;

namespace ForkPlus.Services.AvaloniaImpl
{
	/// <summary>
	/// 跨平台凭据管理器实现（对标原 WPF 工程 ForkPlus/WindowsCredentialManager）。
	/// 按 OS 分派到对应存储后端：
	///   Windows → CredMan（advapi32 P/Invoke，与原实现完全一致）
	///   macOS   → Keychain（/usr/bin/security）
	///   Linux   → 文件存储（XDG 配置目录下的 JSON，带轻量混淆；生产建议替换为 libsecret/Secret Service）
	/// 业务层只依赖 <see cref="ICredentialManager"/>，零改动。
	/// </summary>
	public class AvaloniaCredentialManager : ICredentialManager
	{
		private const string SshKeyUsernameString = "SSH Key Passphrase";

		private readonly ICredentialStore _store = CreateStore();

		private static ICredentialStore CreateStore()
		{
			if (OperatingSystem.IsWindows())
				return new WindowsCredentialStore();
			if (OperatingSystem.IsMacOS())
				return new MacOsKeychainStore();
			return new LinuxFileCredentialStore();
		}

		public Credential? Read(string target) => _store.Read(target);

		public void Write(string target, string userName, string secret)
		{
			if (Encoding.Unicode.GetBytes(secret).Length > 512)
				throw new ArgumentOutOfRangeException(nameof(secret), "The secret message has exceeded 512 bytes.");
			_store.Write(target, userName ?? Environment.UserName, secret);
		}

		public bool Delete(string target) => _store.Delete(target);

		public IReadOnlyList<Credential> Enumerate() => _store.Enumerate();

		public string? QuerySshPassphrase(string sshKey)
		{
			var c = Read("fork:" + sshKey);
			return c != null && c.UserName == SshKeyUsernameString ? c.Secret : null;
		}

		public void StoreSshPassphrase(string sshKey, string passphrase)
			=> Write("fork:" + sshKey, SshKeyUsernameString, passphrase);

		public string? QuerySshUserPassword(Uri url, string username)
			=> Read("fork:ssh://" + url.Host + "." + username + ".password")?.Secret;

		public void StoreSshUserPassword(Uri url, string username, string password)
			=> Write("fork:ssh://" + url.Host + "." + username + ".password", username, password);

		// ===== 平台后端契约 =====
		private interface ICredentialStore
		{
			Credential? Read(string target);
			void Write(string target, string userName, string secret);
			bool Delete(string target);
			IReadOnlyList<Credential> Enumerate();
		}

		// ===== Windows：CredMan（advapi32 P/Invoke，与原 WindowsCredentialManager 完全一致）=====
		private sealed class WindowsCredentialStore : ICredentialStore
		{
			private enum CredentialType : uint
			{
				Generic = 1u,
				DomainPassword = 2u,
				DomainCertificate = 3u,
				DomainVisiblePassword = 4u,
				GenericCertificate = 5u,
				DomainExtended = 6u,
				Maximum = 7u,
				CredTypeMaximum = 0x10000u
			}

			[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
			private struct CREDENTIAL
			{
				public uint Flags;
				public CredentialType Type;
				public IntPtr TargetName;
				public IntPtr Comment;
				public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
				public uint CredentialBlobSize;
				public IntPtr CredentialBlob;
				public uint Persist;
				public uint AttributeCount;
				public IntPtr Attributes;
				public IntPtr TargetAlias;
				public IntPtr UserName;
			}

			private sealed class CriticalCredentialHandle : CriticalHandleZeroOrMinusOneIsInvalid
			{
				public CriticalCredentialHandle(IntPtr preexistingHandle) => SetHandle(preexistingHandle);

				public CREDENTIAL GetCredential()
				{
					if (IsInvalid)
						throw new InvalidOperationException("Invalid CriticalHandle!");
					return (CREDENTIAL)Marshal.PtrToStructure(handle, typeof(CREDENTIAL));
				}

				protected override bool ReleaseHandle()
				{
					if (!IsInvalid)
					{
						CredFree(handle);
						SetHandleAsInvalid();
						return true;
					}
					return false;
				}
			}

			public Credential? Read(string target)
			{
				if (CredRead(target, CredentialType.Generic, 0, out var ptr))
				{
					using (var handle = new CriticalCredentialHandle(ptr))
						return ToCredential(handle.GetCredential());
				}
				return null;
			}

			public void Write(string target, string userName, string secret)
			{
				var cred = new CREDENTIAL
				{
					AttributeCount = 0,
					Attributes = IntPtr.Zero,
					Comment = IntPtr.Zero,
					TargetAlias = IntPtr.Zero,
					Type = CredentialType.Generic,
					Persist = 2, // CredentialPersistence.LocalMachine
					CredentialBlobSize = (uint)Encoding.Unicode.GetBytes(secret).Length,
					TargetName = Marshal.StringToCoTaskMemUni(target),
					CredentialBlob = Marshal.StringToCoTaskMemUni(secret),
					UserName = Marshal.StringToCoTaskMemUni(userName)
				};
				bool ok = CredWrite(ref cred, 0);
				int err = Marshal.GetLastWin32Error();
				Marshal.FreeCoTaskMem(cred.TargetName);
				Marshal.FreeCoTaskMem(cred.CredentialBlob);
				Marshal.FreeCoTaskMem(cred.UserName);
				if (!ok)
					throw new Exception($"CredWrite failed with the error code {err}.");
			}

			public bool Delete(string target) => CredDelete(target, CredentialType.Generic, 0);

			public IReadOnlyList<Credential> Enumerate()
			{
				var list = new List<Credential>();
				if (CredEnumerate(null, 0, out var count, out var pCredentials))
				{
					int ptrSize = Marshal.SizeOf(typeof(IntPtr));
					for (int i = 0; i < count; i++)
					{
						IntPtr item = Marshal.ReadIntPtr(pCredentials, i * ptrSize);
						list.Add(ToCredential((CREDENTIAL)Marshal.PtrToStructure(item, typeof(CREDENTIAL))));
					}
				}
				return list;
			}

			private static Credential ToCredential(CREDENTIAL c)
			{
				string target = Marshal.PtrToStringUni(c.TargetName);
				string user = Marshal.PtrToStringUni(c.UserName);
				string secret = c.CredentialBlob != IntPtr.Zero
					? Marshal.PtrToStringUni(c.CredentialBlob, (int)c.CredentialBlobSize / 2)
					: null;
				return new Credential(target, user, secret, c.Type.ToString());
			}

			[DllImport("Advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredReadW", SetLastError = true)]
			private static extern bool CredRead(string target, CredentialType type, int reservedFlag, out IntPtr credentialPtr);

			[DllImport("Advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredWriteW", SetLastError = true)]
			private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

			[DllImport("Advapi32", CharSet = CharSet.Unicode, SetLastError = true)]
			private static extern bool CredEnumerate(string filter, int flag, out int count, out IntPtr pCredentials);

			[DllImport("Advapi32.dll", SetLastError = true)]
			private static extern bool CredFree([In] IntPtr cred);

			[DllImport("Advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredDeleteW", SetLastError = true)]
			private static extern bool CredDelete(string target, CredentialType type, int reservedFlag);
		}

		// ===== macOS：Keychain（/usr/bin/security）=====
		private sealed class MacOsKeychainStore : ICredentialStore
		{
			public Credential? Read(string target)
			{
				// security find-generic-password -s <service> -g
				// 密码打印到 stderr（password: "..."），账户 acct 在 stdout plist 中
				var (exit, stdout, stderr) = Run("/usr/bin/security", $"find-generic-password -s {Arg(target)} -g");
				if (exit != 0)
					return null;
				var user = Match(stdout, "acct");
				var secret = Match(stderr, "password");
				if (secret == null)
					return null;
				return new Credential(target, user ?? Environment.UserName, secret);
			}

			public void Write(string target, string userName, string secret)
			{
				// -U：已存在则更新；-a 账户 / -s 服务 / -w 密码
				Run("/usr/bin/security",
					$"add-generic-password -a {Arg(userName)} -s {Arg(target)} -w {Arg(secret)} -U");
			}

			public bool Delete(string target)
			{
				var (exit, _, _) = Run("/usr/bin/security", $"delete-generic-password -s {Arg(target)}");
				return exit == 0;
			}

			public IReadOnlyList<Credential> Enumerate()
			{
				// Keychain 枚举需解析 dump-keychain，跨平台 PoC 暂不实现（Windows/Linux 已支持）。
				return Array.Empty<Credential>();
			}

			private static string? Match(string text, string key)
			{
				// 在 plist / "key": "value" 文本中抓取 <key>acct</key><string>VALUE</string> 或 password: "VALUE"
				var m = System.Text.RegularExpressions.Regex.Match(
					text, $"<key>{key}</key>\\s*<string>(.*?)</string>");
				if (m.Success)
					return m.Groups[1].Value;
				m = System.Text.RegularExpressions.Regex.Match(text, $"{key}: \"(.*?)\"");
				return m.Success ? m.Groups[1].Value : null;
			}
		}

		// ===== Linux：文件存储（XDG 配置目录，轻量混淆；生产建议替换为 libsecret）=====
		private sealed class LinuxFileCredentialStore : ICredentialStore
		{
			private static string StoreFile
			{
				get
				{
					var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
					var baseDir = string.IsNullOrEmpty(xdg)
						? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
						: xdg;
					var dir = Path.Combine(baseDir, "forkplus");
					Directory.CreateDirectory(dir);
					return Path.Combine(dir, "credentials.json");
				}
			}

			private static List<Credential> LoadAll()
			{
				var file = StoreFile;
				if (!File.Exists(file))
					return new List<Credential>();
				try
				{
					var json = JsonSerializer.Deserialize<List<StoredCredential>>(File.ReadAllText(file));
					if (json == null)
						return new List<Credential>();
					var result = new List<Credential>();
					foreach (var s in json)
					{
						try { result.Add(new Credential(s.Target, s.User, Deobfuscate(s.Secret), s.Type)); }
						catch { /* 跳过损坏项 */ }
					}
					return result;
				}
				catch
				{
					return new List<Credential>();
				}
			}

			private static void SaveAll(List<Credential> all)
			{
				var list = new List<StoredCredential>();
				foreach (var c in all)
					list.Add(new StoredCredential { Target = c.TargetName, User = c.UserName, Secret = Obfuscate(c.Secret), Type = c.Type });
				File.WriteAllText(StoreFile, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
			}

			public Credential? Read(string target)
			{
				foreach (var c in LoadAll())
					if (c.TargetName == target)
						return c;
				return null;
			}

			public void Write(string target, string userName, string secret)
			{
				var all = LoadAll();
				all.RemoveAll(c => c.TargetName == target);
				all.Add(new Credential(target, userName, secret));
				SaveAll(all);
			}

			public bool Delete(string target)
			{
				var all = LoadAll();
				int removed = all.RemoveAll(c => c.TargetName == target);
				if (removed > 0)
				{
					SaveAll(all);
					return true;
				}
				return false;
			}

			public IReadOnlyList<Credential> Enumerate() => LoadAll();

			private sealed class StoredCredential
			{
				public string Target { get; set; } = "";
				public string User { get; set; } = "";
				public string Secret { get; set; } = "";
				public string Type { get; set; } = "Generic";
			}
		}

		// ===== 工具 =====
		private static (int ExitCode, string Stdout, string Stderr) Run(string fileName, string args)
		{
			try
			{
				var psi = new ProcessStartInfo(fileName, args)
				{
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};
				using var p = Process.Start(psi);
				if (p == null)
					return (-1, "", "");
				var stdout = p.StandardOutput.ReadToEnd();
				var stderr = p.StandardError.ReadToEnd();
				p.WaitForExit();
				return (p.ExitCode, stdout, stderr);
			}
			catch (Exception ex)
			{
				return (-1, "", ex.Message);
			}
		}

		// 参数转义：security 对含空格/特殊字符的值需要用双引号包裹（无法内嵌双引号，这里做基本转义）
		private static string Arg(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";

		// 轻量混淆（非加密，仅避免明文落盘）：XOR 固定密钥后 base64
		private static readonly byte[] ObfKey = Encoding.UTF8.GetBytes("ForkPlus.Credential.Obf");
		private static string Obfuscate(string s) => Convert.ToBase64String(Xor(Encoding.UTF8.GetBytes(s)));
		private static string Deobfuscate(string s)
		{
			try { return Encoding.UTF8.GetString(Xor(Convert.FromBase64String(s))); }
			catch { return ""; }
		}
		private static byte[] Xor(byte[] data)
		{
			for (int i = 0; i < data.Length; i++)
				data[i] = (byte)(data[i] ^ ObfKey[i % ObfKey.Length]);
			return data;
		}
	}
}
