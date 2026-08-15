using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using ForkPlus.Avalonia.Diff;
using ForkPlus.Avalonia.Git;
using ForkPlus.Services;
using ForkPlus.Services.AvaloniaImpl;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M3 验收 1：<see cref="UnifiedDiffParser"/> 解析标准 unified diff 文本。
/// 不依赖 biturbo / git，纯算法。
/// </summary>
public class UnifiedDiffParserTests
{
    [Fact]
    public void Parse_SingleFile_AddRemoveContext_LinesClassifiedAndNumbered()
    {
        const string unified =
@"diff --git a/Calculator.cs b/Calculator.cs
index 0123..4567 100644
--- a/Calculator.cs
+++ b/Calculator.cs
@@ -1,3 +1,4 @@
 public int Add(int a, int b)
 {
+    // support bigger values
     return a + b;
 }
";
        DiffResult r = UnifiedDiffParser.Parse(unified);

        Assert.Equal("Calculator.cs", r.OldPath);
        Assert.Equal("Calculator.cs", r.NewPath);
        // 期望 5 行：unchanged / unchanged / added / unchanged / unchanged
        Assert.Equal(5, r.Lines.Count);
        Assert.Equal(DiffLineKind.Unchanged, r.Lines[0].Kind);
        Assert.Equal("public int Add(int a, int b)", r.Lines[0].Text);
        Assert.Equal(1, r.Lines[0].OldLineNumber);
        Assert.Equal(1, r.Lines[0].NewLineNumber);

        Assert.Equal(DiffLineKind.Added, r.Lines[2].Kind);
        Assert.Equal("    // support bigger values", r.Lines[2].Text);
        Assert.Equal(-1, r.Lines[2].OldLineNumber);
        Assert.Equal(3, r.Lines[2].NewLineNumber);

        Assert.Equal(DiffLineKind.Unchanged, r.Lines[3].Kind);
        Assert.Equal("    return a + b;", r.Lines[3].Text);
        Assert.Equal(3, r.Lines[3].OldLineNumber);
        Assert.Equal(4, r.Lines[3].NewLineNumber);
    }

    [Fact]
    public void Parse_RemovedLine_HasOldNoMinusOneNewNo()
    {
        const string unified =
@"diff --git a/x.txt b/x.txt
--- a/x.txt
+++ b/x.txt
@@ -1,2 +1,1 @@
 keep
-drop
";
        DiffResult r = UnifiedDiffParser.Parse(unified);
        Assert.Equal(2, r.Lines.Count);
        Assert.Equal(DiffLineKind.Unchanged, r.Lines[0].Kind);
        Assert.Equal(DiffLineKind.Removed, r.Lines[1].Kind);
        Assert.Equal(2, r.Lines[1].OldLineNumber);
        Assert.Equal(-1, r.Lines[1].NewLineNumber);
    }

    [Fact]
    public void Parse_TwoFiles_InsertsFileSeparator()
    {
        const string unified =
@"diff --git a/a.txt b/a.txt
--- a/a.txt
+++ b/a.txt
@@ -0,0 +1,1 @@
+hello
diff --git a/b.txt b/b.txt
--- a/b.txt
+++ b/b.txt
@@ -0,0 +1,1 @@
+world
";
        DiffResult r = UnifiedDiffParser.Parse(unified);
        // 1 added("hello") + 1 separator("── b.txt ──") + 1 added("world")
        Assert.Equal(3, r.Lines.Count);
        Assert.Equal(DiffLineKind.Added, r.Lines[0].Kind);
        Assert.Equal("hello", r.Lines[0].Text);
        Assert.Equal(DiffLineKind.Unchanged, r.Lines[1].Kind);
        Assert.Contains("b.txt", r.Lines[1].Text);
        Assert.Equal(DiffLineKind.Added, r.Lines[2].Kind);
        Assert.Equal("world", r.Lines[2].Text);
    }
}

/// <summary>
/// M3 验收 2：<see cref="GitRepository.GetCommitDiff"/> 经 git CLI（IGitPathResolver 解析）
/// 拿到真实 commit 的 unified diff，喂给 <see cref="UnifiedDiffParser"/> 产出可用
/// <see cref="DiffResult"/>。需要 git 可执行文件已在 PATH（CI 镜像里就装好了）。
/// </summary>
public class GitRepositoryGetCommitDiffTests
{
    private static bool _servicesInitialized;
    private static readonly object _initLock = new();

    private static void EnsureServicesInitialized()
    {
        if (_servicesInitialized) return;
        lock (_initLock)
        {
            if (_servicesInitialized) return;
            ServiceLocator.Initialize(
                dispatcher: new AvaloniaDispatcher(),
                designMode: new AvaloniaDesignModeService(),
                appContext: new AvaloniaAppContext(),
                clipboard: new AvaloniaClipboardService(),
                timer: new AvaloniaTimerService(),
                toast: new AvaloniaToastNotificationService(),
                windowManager: new AvaloniaWindowManagerService(),
                credentials: new AvaloniaCredentialManager(),
                fileDialog: new AvaloniaFileDialogService(),
                gitPathResolver: new AvaloniaGitPathResolver(),
                shell: new AvaloniaShellIntegrationService());
            _servicesInitialized = true;
        }
    }

    [Fact]
    public void GetCommitDiff_SecondCommit_ContainsAddedBLine()
    {
        EnsureServicesInitialized();
        string repoDir = CreateMultiCommitRepo();
        try
        {
            using var repo = new GitRepository(repoDir);
            // 第二个 commit 增加了 b.txt（含一行 "b"）
            GitCommit[] commits = repo.GetCommits("refs/heads/main", 10);
            Assert.True(commits.Length >= 2);
            // GetCommits 按 date_order 返回：commits[0] 是最新（third commit），倒着取第二个提交
            // 第二个 commit (subject "second commit") 应该是新增了 b.txt
            GitCommit? second = commits.FirstOrDefault(c => c.Subject == "second commit");
            Assert.NotNull(second);

            DiffResult diff = repo.GetCommitDiff(second!.Sha);
            Assert.Contains(diff.Lines, l => l.Kind == DiffLineKind.Added && l.Text == "b");
            // 至少有一处 Removed 或 Added，证明 parser 真的解析了 hunk
            Assert.Contains(diff.Lines, l => l.Kind != DiffLineKind.Unchanged);
        }
        finally
        {
            TryDelete(repoDir);
        }
    }

    [Fact]
    public void GetCommitDiff_FirstCommit_NoParent_AllLinesAdded()
    {
        EnsureServicesInitialized();
        string repoDir = CreateMultiCommitRepo();
        try
        {
            using var repo = new GitRepository(repoDir);
            GitCommit[] commits = repo.GetCommits("refs/heads/main", 10);
            GitCommit? first = commits.FirstOrDefault(c => c.Subject == "first commit");
            Assert.NotNull(first);

            DiffResult diff = repo.GetCommitDiff(first!.Sha);
            // 首个 commit 整文件都是新增
            Assert.NotEmpty(diff.Lines);
            Assert.True(diff.Lines.All(l => l.Kind == DiffLineKind.Added),
                "首个 commit 的每行都应该是 Added，但实际看到：" +
                string.Join(",", diff.Lines.Select(l => l.Kind.ToString()).Distinct()));
        }
        finally
        {
            TryDelete(repoDir);
        }
    }

    [Fact]
    public void GetCommitDiff_EmptySha_ThrowsArgumentException()
    {
        EnsureServicesInitialized();
        using var repo = new GitRepository(Path.Combine(Path.GetTempPath(), "fp_dummy_" + Guid.NewGuid().ToString("N")));
        Assert.Throws<ArgumentException>(() => repo.GetCommitDiff(""));
        Assert.Throws<ArgumentException>(() => repo.GetCommitDiff("   "));
    }

    private static string CreateMultiCommitRepo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "forkplus_m3_repo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        RunGit(dir, "init", "-b", "main");
        RunGit(dir, "config", "user.email", "ci@example.com");
        RunGit(dir, "config", "user.name", "ForkPlus CI");

        File.WriteAllText(Path.Combine(dir, "a.txt"), "a");
        RunGit(dir, "add", "a.txt");
        RunGit(dir, "commit", "-m", "first commit");
        Thread.Sleep(1100);

        File.WriteAllText(Path.Combine(dir, "b.txt"), "b");
        RunGit(dir, "add", "b.txt");
        RunGit(dir, "commit", "-m", "second commit");
        return dir;
    }

    private static void RunGit(string workingDir, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string a in args) psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动 git 进程");
        proc.WaitForExit();
        string stderr = proc.StandardError.ReadToEnd();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(" ", args)} 失败 (exit {proc.ExitCode}): {stderr}");
    }

    private static void TryDelete(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch { }
    }
}
