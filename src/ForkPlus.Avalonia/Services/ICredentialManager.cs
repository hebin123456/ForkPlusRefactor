using System;
using System.Collections.Generic;

namespace ForkPlus.Services
{
	/// <summary>
	/// 跨平台凭据管理接口（对标原 WPF 工程 ForkPlus/WindowsCredentialManager）。
	/// 原实现通过 advapi32 的 CredRead/CredWrite/CredDelete（Windows CredMan，DPAPI）存取，
	/// 仅 Windows 可用；此处抽象为平台无关接口，由各平台实现：
	///   Windows → 沿用 CredMan P/Invoke；macOS → Keychain（security）；Linux → Secret Service / 文件存储。
	/// 业务层（账户、SSH 凭据）只依赖本接口，零改动。
	/// </summary>
	public interface ICredentialManager
	{
		/// <summary>读取凭据；不存在返回 null（对标 ReadCredential）。</summary>
		Credential? Read(string target);

		/// <summary>
		/// 写入凭据（对标 WriteCredential）。secret 上限 512 字节（UTF-16），超出抛 <see cref="ArgumentOutOfRangeException"/>。
		/// </summary>
		void Write(string target, string userName, string secret);

		/// <summary>删除凭据，成功返回 true（对标 RemoveCredential）。</summary>
		bool Delete(string target);

		/// <summary>枚举全部凭据（对标 EnumerateCrendentials）。</summary>
		IReadOnlyList<Credential> Enumerate();

		// ---- 便捷封装：对标 ForkPlus 的 SSH 凭据约定（target 固定前缀 "fork:"）----

		/// <summary>查询 SSH 私钥口令（对标 QuerySshPassphrase）。</summary>
		string? QuerySshPassphrase(string sshKey);

		/// <summary>存储 SSH 私钥口令（对标 StoreSshPassphrase）。</summary>
		void StoreSshPassphrase(string sshKey, string passphrase);

		/// <summary>查询 SSH 远程用户密码（对标 QuerySshUserPassword）。</summary>
		string? QuerySshUserPassword(Uri url, string username);

		/// <summary>存储 SSH 远程用户密码（对标 StoreSshUserPassword）。</summary>
		void StoreSshUserPassword(Uri url, string username, string password);
	}
}
