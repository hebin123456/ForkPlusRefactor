using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Avalonia.Controls;
using ForkPlus.Avalonia.Dialogs;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M13 阶段：含 ItemsControl 列表的对话框。
/// 覆盖：RemoveStash（单/多条切换）、RemoveLocalBranch（列表 + 远端/worktree 勾选）、
///       RemoveTag（列表 + 从远端删除勾选）、BindableGitPointView 增强（GitPoint 属性）。
/// </summary>
public class M13ListDialogsTests
{
    [AvaloniaFact]
    public void M13_RemoveStash_SingleMode_Defaults()
    {
        var win = new RemoveStashWindow();
        win.ViewModel.SinglePointText = "stash@{0}: WIP on main: abc1234";
        win.ViewModel.Stashes.Clear(); // 单条
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(win.ViewModel.IsSingleMode);
        Assert.Equal("Stash:", win.ViewModel.StartPointLabel);
        Assert.Equal("Delete", win.ViewModel.SubmitButtonTitle);
        win.Close();
    }

    [AvaloniaFact]
    public void M13_RemoveStash_MultipleMode_TitleAndList()
    {
        var win = new RemoveStashWindow();
        win.ViewModel.Stashes.Add(new StashItem { ReflogName = "stash@{0}", Subject = "WIP on main" });
        win.ViewModel.Stashes.Add(new StashItem { ReflogName = "stash@{1}", Subject = "WIP on feature" });
        win.ViewModel.StartPointLabel = "Stashes:";
        win.ViewModel.SubmitButtonTitle = "Delete 2 stashes";
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.False(win.ViewModel.IsSingleMode);
        Assert.Equal("Stashes:", win.ViewModel.StartPointLabel);
        Assert.Equal("Delete 2 stashes", win.ViewModel.SubmitButtonTitle);
        Assert.Equal(2, win.ViewModel.Stashes.Count);
        win.Close();
    }

    [AvaloniaFact]
    public void M13_RemoveLocalBranch_AllFieldsPropagate()
    {
        var win = new RemoveLocalBranchWindow();
        win.ViewModel.Branches.Add(new BranchItem { BranchName = "feature/x" });
        win.ViewModel.Branches.Add(new BranchItem { BranchName = "feature/y", UpstreamName = "origin/feature/y" });
        win.ViewModel.DeleteRemoteEnabled = true;
        win.ViewModel.DeleteRemoteChecked = true;
        win.ViewModel.DeleteWorktreeEnabled = true;
        win.ViewModel.DeleteWorktreeLabel = "Also delete worktree";
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, win.ViewModel.Branches.Count);
        Assert.True(win.ViewModel.Branches[1].HasUpstream);
        Assert.False(win.ViewModel.Branches[0].HasUpstream);
        Assert.True(win.ViewModel.DeleteRemoteChecked);
        Assert.True(win.ViewModel.DeleteWorktreeEnabled);

        var r = win.GetResult();
        Assert.NotNull(r);
        Assert.True(r!.Value.DeleteRemote);
        Assert.False(r.Value.DeleteWorktree); // 默认未勾
        win.Close();
    }

    [AvaloniaFact]
    public void M13_RemoveTag_CheckboxPropagates()
    {
        var win = new RemoveTagWindow();
        win.ViewModel.Tags.Add(new TagItem { Name = "v1.0.0", Sha = "abc1234" });
        win.ViewModel.DeleteFromRemotesLabel = "Also delete from remotes";
        win.ViewModel.DeleteFromRemotesChecked = true;
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Single(win.ViewModel.Tags);
        Assert.True(win.ViewModel.IsSingleMode);
        Assert.True(win.GetDeleteFromRemotes());

        win.ViewModel.Tags.Add(new TagItem { Name = "v2.0.0", Sha = "def5678" });
        Assert.False(win.ViewModel.IsSingleMode);
        win.Close();
    }

    [AvaloniaFact]
    public void M13_BindableGitPointView_GitPointProperty()
    {
        // 新增的 GitPoint 属性允许 ItemsControl 把数据项作为整体传入
        var gp = new BindableGitPointView();
        Assert.Null(gp.GitPoint);
        Assert.Null(gp.Subject);

        // ItemsControl 模式：传入任意对象（反射取 Name / BranchName）
        var item = new { Name = "v1.0.0", BranchName = "main" };
        gp.GitPoint = item;
        Assert.Equal("v1.0.0", gp.Subject);
        Assert.Equal("main", gp.Detail);

        // 切换到另一个对象，Subject/Detail 跟着变
        var item2 = new { Name = "v2.0.0", Sha = "def5678" };
        gp.GitPoint = item2;
        Assert.Equal("v2.0.0", gp.Subject);
        Assert.Equal("def5678", gp.Detail);
    }

    [AvaloniaFact]
    public void M13_AllDialogs_Screenshot()
    {
        var dir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "verification", "avalonia", "m13-list-dialogs"));
        Directory.CreateDirectory(dir);

        var single = new RemoveStashWindow { Width = 520, Height = 180 };
        single.ViewModel.SinglePointText = "stash@{0}: WIP on main: abc1234";

        var multi = new RemoveStashWindow { Width = 520, Height = 280 };
        multi.ViewModel.Stashes.Add(new StashItem { ReflogName = "stash@{0}", Subject = "WIP on main: abc1234" });
        multi.ViewModel.Stashes.Add(new StashItem { ReflogName = "stash@{1}", Subject = "WIP on feature: def5678" });
        multi.ViewModel.Stashes.Add(new StashItem { ReflogName = "stash@{2}", Subject = "WIP on bugfix: 9abcde0" });
        multi.ViewModel.StartPointLabel = "Stashes:";
        multi.ViewModel.SubmitButtonTitle = "Delete 3 stashes";

        var branch = new RemoveLocalBranchWindow { Width = 570, Height = 280 };
        branch.ViewModel.Branches.Add(new BranchItem { BranchName = "feature/login" });
        branch.ViewModel.Branches.Add(new BranchItem { BranchName = "feature/cart", UpstreamName = "origin/feature/cart" });
        branch.ViewModel.DeleteRemoteEnabled = true;
        branch.ViewModel.DeleteRemoteLabel = "Also delete remote branch origin/feature/cart";

        var tag = new RemoveTagWindow { Width = 520, Height = 280 };
        tag.ViewModel.Tags.Add(new TagItem { Name = "v1.0.0", Sha = "abc1234" });
        tag.ViewModel.Tags.Add(new TagItem { Name = "v1.0.1", Sha = "def5678" });
        tag.ViewModel.DeleteFromRemotesLabel = "Also delete from remotes";

        var wins = new (Window Win, string Name)[]
        {
            (single, "avalonia-m13-removestash-single.png"),
            (multi, "avalonia-m13-removestash-multi.png"),
            (branch, "avalonia-m13-removelocalbranch.png"),
            (tag, "avalonia-m13-removetag.png"),
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
        Assert.True(total > 4000);
    }
}
