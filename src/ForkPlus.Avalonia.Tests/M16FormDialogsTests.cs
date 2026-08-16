using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ForkPlus.Avalonia.Dialogs;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M16 阶段：继续机械转其他表单弹窗。
/// 覆盖：GitFlowFinishFeature（分支选择 + 3 选 CheckBox）、
///       GitFlowFinishHotfix（分支 + 多行 Message + Delete CheckBox）、
///       GitFlowFinishRelease（分支 + Message + Delete + BackMerge）、
///       GitFlowInit（6 个 TextBox 表单 + IsValid）、
///       LeanBranchingFinish（双只读展示）、
///       RenameStash（单 TextBox + Placeholder）、
///       CreateWorktree（分支 + 名称 + 路径 + IsValid）。
/// </summary>
public class M16FormDialogsTests
{
    [AvaloniaFact]
    public void M16_GitFlowFinishFeature_ResultWithOptions()
    {
        var win = new GitFlowFinishFeatureWindow();
        win.ViewModel.Branches.Add(new BranchItem { BranchName = "feature/foo" });
        win.ViewModel.Branches.Add(new BranchItem { BranchName = "feature/bar" });
        win.ViewModel.SelectedBranch = win.ViewModel.Branches[0];
        win.ViewModel.DeleteBranches = true;
        win.ViewModel.RebaseInsteadOfMerge = true;
        win.ViewModel.NoFastForward = false;
        win.Show();
        Dispatcher.UIThread.RunJobs();

        var r = win.GetResult();
        Assert.NotNull(r);
        Assert.Equal("feature/foo", r!.Value.Branch!.BranchName);
        Assert.True(r.Value.DeleteBranches);
        Assert.True(r.Value.RebaseInsteadOfMerge);
        Assert.False(r.Value.NoFastForward);
        win.Close();
    }

    [AvaloniaFact]
    public void M16_GitFlowFinishFeature_NoBranch_NullResult()
    {
        var win = new GitFlowFinishFeatureWindow();
        win.ViewModel.Branches.Add(new BranchItem { BranchName = "feature/foo" });
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Null(win.GetResult());
        win.Close();
    }

    [AvaloniaFact]
    public void M16_GitFlowFinishHotfix_DefaultDeleteBranchesTrue()
    {
        var win = new GitFlowFinishHotfixWindow();
        win.ViewModel.Branches.Add(new BranchItem { BranchName = "hotfix/fix-bug" });
        win.ViewModel.SelectedBranch = win.ViewModel.Branches[0];
        win.ViewModel.Message = "fix critical bug";
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(win.ViewModel.DeleteBranches);
        var r = win.GetResult();
        Assert.NotNull(r);
        Assert.Equal("hotfix/fix-bug", r!.Value.Branch!.BranchName);
        Assert.Equal("fix critical bug", r.Value.Message);
        Assert.True(r.Value.DeleteBranches);
        win.Close();
    }

    [AvaloniaFact]
    public void M16_GitFlowFinishRelease_BackMergeDefault()
    {
        var win = new GitFlowFinishReleaseWindow();
        win.ViewModel.Branches.Add(new BranchItem { BranchName = "release/1.2.0" });
        win.ViewModel.SelectedBranch = win.ViewModel.Branches[0];
        win.ViewModel.Message = "Release 1.2.0";
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(win.ViewModel.DeleteBranches);
        Assert.True(win.ViewModel.BackMergeMaster);

        var r = win.GetResult();
        Assert.NotNull(r);
        Assert.Equal("release/1.2.0", r!.Value.Branch!.BranchName);
        Assert.Equal("Release 1.2.0", r.Value.Message);
        Assert.True(r.Value.BackMergeMaster);
        win.Close();
    }

    [AvaloniaFact]
    public void M16_GitFlowInit_ValidationAndResult()
    {
        var win = new GitFlowInitWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();

        // 默认值即可 valid
        Assert.True(win.ViewModel.IsValid);
        var r = win.GetResult();
        Assert.NotNull(r);
        Assert.Equal("master", r!.ProductionBranch);
        Assert.Equal("develop", r.DevelopmentBranch);
        Assert.Equal("feature/", r.FeaturePrefix);
        Assert.Equal("release/", r.ReleasePrefix);
        Assert.Equal("hotfix/", r.HotfixPrefix);

        // 清空 production → invalid
        win.ViewModel.ProductionBranch = "";
        Assert.False(win.ViewModel.IsValid);
        Assert.Null(win.GetResult());
        win.Close();
    }

    [AvaloniaFact]
    public void M16_LeanBranchingFinish_ResultEcho()
    {
        var win = new LeanBranchingFinishWindow();
        win.ViewModel.CurrentBranchText = "feature/foo · abc1234";
        win.ViewModel.MainBranchText = "main · def5678";
        win.Show();
        Dispatcher.UIThread.RunJobs();

        var r = win.GetResult();
        Assert.NotNull(r);
        Assert.Equal("feature/foo · abc1234", r!.Value.Current);
        Assert.Equal("main · def5678", r.Value.Main);
        win.Close();
    }

    [AvaloniaFact]
    public void M16_RenameStash_EmptyMessage_NullResult()
    {
        var win = new RenameStashWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Null(win.GetResult());
        win.ViewModel.Message = "new stash message";
        Assert.Equal("new stash message", win.GetResult());
        win.Close();
    }

    [AvaloniaFact]
    public void M16_CreateWorktree_ValidationAndResult()
    {
        var win = new CreateWorktreeWindow();
        win.ViewModel.Branches.Add(new BranchItem { BranchName = "develop" });
        win.ViewModel.SelectedBranch = win.ViewModel.Branches[0];
        win.Show();
        Dispatcher.UIThread.RunJobs();

        // 空 branch name → invalid
        Assert.False(win.ViewModel.IsValid);
        Assert.Null(win.GetResult());

        win.ViewModel.BranchName = "new-feature";
        win.ViewModel.Path = "/tmp/wt-new-feature";
        Assert.True(win.ViewModel.IsValid);
        var r = win.GetResult();
        Assert.NotNull(r);
        Assert.Equal("new-feature", r!.Value.BranchName);
        Assert.Equal("/tmp/wt-new-feature", r.Value.Path);
        Assert.Equal("develop", r.Value.Branch!.BranchName);
        win.Close();
    }

    [AvaloniaFact]
    public void M16_AllDialogs_Screenshot()
    {
        var dir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "verification", "avalonia", "m16-form-dialogs"));
        Directory.CreateDirectory(dir);

        var finishFeature = new GitFlowFinishFeatureWindow { Width = 490, Height = 240 };
        finishFeature.ViewModel.Branches.Add(new BranchItem { BranchName = "feature/foo" });
        finishFeature.ViewModel.SelectedBranch = finishFeature.ViewModel.Branches[0];
        finishFeature.ViewModel.DeleteBranches = true;
        finishFeature.ViewModel.RebaseInsteadOfMerge = true;

        var finishHotfix = new GitFlowFinishHotfixWindow { Width = 530, Height = 280 };
        finishHotfix.ViewModel.Branches.Add(new BranchItem { BranchName = "hotfix/fix-bug" });
        finishHotfix.ViewModel.SelectedBranch = finishHotfix.ViewModel.Branches[0];
        finishHotfix.ViewModel.Message = "fix critical bug";

        var finishRelease = new GitFlowFinishReleaseWindow { Width = 530, Height = 320 };
        finishRelease.ViewModel.Branches.Add(new BranchItem { BranchName = "release/1.2.0" });
        finishRelease.ViewModel.SelectedBranch = finishRelease.ViewModel.Branches[0];
        finishRelease.ViewModel.Message = "Release 1.2.0";

        var init = new GitFlowInitWindow { Width = 530, Height = 320 };

        var leanFinish = new LeanBranchingFinishWindow { Width = 610, Height = 180 };
        leanFinish.ViewModel.CurrentBranchText = "feature/foo · abc1234";
        leanFinish.ViewModel.MainBranchText = "main · def5678";

        var renameStash = new RenameStashWindow { Width = 500, Height = 160 };
        renameStash.ViewModel.Message = "new stash message";

        var createWorktree = new CreateWorktreeWindow { Width = 590, Height = 260 };
        createWorktree.ViewModel.Branches.Add(new BranchItem { BranchName = "develop" });
        createWorktree.ViewModel.SelectedBranch = createWorktree.ViewModel.Branches[0];
        createWorktree.ViewModel.BranchName = "new-feature";
        createWorktree.ViewModel.Path = "/tmp/wt-new-feature";

        var wins = new (Window Win, string Name)[]
        {
            (finishFeature, "avalonia-m16-gitflowfinishfeature.png"),
            (finishHotfix, "avalonia-m16-gitflowfinishhotfix.png"),
            (finishRelease, "avalonia-m16-gitflowfinishrelease.png"),
            (init, "avalonia-m16-gitflowinit.png"),
            (leanFinish, "avalonia-m16-leanbranchingfinish.png"),
            (renameStash, "avalonia-m16-renamestash.png"),
            (createWorktree, "avalonia-m16-createworktree.png"),
        };

        long total = 0;
        foreach (var (win, name) in wins)
        {
            win.Show();
            Dispatcher.UIThread.RunJobs();
            win.Measure(new Size(win.Width, win.Height));
            win.Arrange(new Rect(0, 0, win.Width, win.Height));
            Dispatcher.UIThread.RunJobs();
            var rtb = new RenderTargetBitmap(new PixelSize((int)win.Width, (int)win.Height), new Vector(96, 96));
            rtb.Render(win);
            var file = Path.Combine(dir, name);
            rtb.Save(file);
            Assert.True(File.Exists(file));
            var len = new FileInfo(file).Length;
            Assert.True(len > 1000, $"file too small: {file} = {len}");
            total += len;
            win.Close();
        }
        Assert.True(total > 7000);
    }
}
