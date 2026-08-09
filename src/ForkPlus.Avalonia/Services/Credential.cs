using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 一条凭据的记录（对标原 WPF 工程 ForkPlus/WindowsCredentialManager.Credential）。
	/// <see cref="Secret"/> 对应原类型的 <c>Password</c> 字段。
	/// 跨平台凭据存储（Windows CredMan / macOS Keychain / Linux 文件）统一以该记录传递。
	/// </summary>
	public sealed record Credential(
		/// <summary>目标标识（原 CredMan 的 TargetName，如 "fork:ssh://github.com.user.password"）。</summary>
		string TargetName,
		/// <summary>关联用户名（SSH 密钥口令固定为 "SSH Key Passphrase"）。</summary>
		string UserName,
		/// <summary>密钥明文（对应原 Password）。</summary>
		string Secret,
		/// <summary>凭据类型，默认 "Generic"（对标 CREDENTIAL.Type）。</summary>
		string Type = "Generic");
}
