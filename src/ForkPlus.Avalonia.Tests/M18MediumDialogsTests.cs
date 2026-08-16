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
/// M18 阶段：中等复杂度剩余弹窗。
/// 覆盖：ApplyStash / CherryPick / DeleteSubmodule / RebaseBranch / CreatePartialStash。
/// </summary>
public class M18MediumDialogsTests
{
    [AvaloniaFact]
    public void M18_ApplyStash_ResultWithStashAndRestoreIndex()
    {
        var win = new ApplyStashWindow();
        win.ViewModel.Stashes.Add(new StashItem { ReflogName = "stash@{0}", Subject = "WIP: feature" });
        win.ViewModel.Stashes.Add(new StashItem { ReflogName = "stash@{1}", Subject = "before merge" });
        win.ViewModel.SelectedStash = win.ViewModel.Stashes[0];
        win.ViewModel.RestoreIndex = true;
        win.Show();
        Dispatcher.UIThread.RunJobs();

        var r = win.GetResult();
        Assert.NotNull(r);
        Assert.Equal("stash@{0}", r!.Value.Stash!.ReflogName);
        Assert.True(r.Value.RestoreIndex);

        // 切到第二个 stash
        win.ViewModel.SelectedStash = win.ViewModel.Stashes[1];
        var r2 = win.GetResult();
        Assert.Equal("stash@{1}", r2!.Value.Stash!.ReflogName);
        win.Close();
    }

    [AvaloniaFact]
    public void M18_ApplyStash_NoStash_NullResult()
    {
        var win = new ApplyStashWindow();
        win.ViewModel.Stashes.Add(new StashItem { ReflogName = "stash@{0}" });
        win.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Null(win.GetResult());
        win.Close();
    }

    [AvaloniaFact]
    public void M18_CherryPick_AllOptionsPropagate()
    {
        var win = new CherryPickWindow();
        win.ViewModel.RevisionText = "abc1234";
        win.ViewModel.AvailableParents.Add("abc1234^");
        win.ViewModel.AvailableParents.Add("abc1234^^");
        win.ViewModel.ParentToSkipText = "abc1234^";
        win.ViewModel.CreateCommit = false;
        win.ViewModel.AppendOriginSha = true;
        win.ViewModel.SignOff = true;
        win.Show();
        Dispatcher.UIThread.RunJobs();

        var r = win.GetResult();
        Assert.False(r.CreateCommit);
        Assert.True(r.AppendOrigin);
        Assert.True(r.SignOff);
        Assert.Equal("abc1234^", r.ParentToSkip);
        win.Close();
    }

    [AvaloniaFact]
    public void M18_CherryPick_NoParent_NullParentInResult()
    {
        var win = new CherryPickWindow();
        win.ViewModel.CreateCommit = true;
        win.Show();
        Dispatcher.UIThread.RunJobs();

        var r = win.GetResult();
        Assert.True(r.CreateCommit);
        Assert.Null(r.ParentToSkip);
        win.Close();
    }

    [AvaloniaFact]
    public void M18_DeleteSubmodule_ValidationAndResult()
    {
        var win = new DeleteSubmoduleWindow();
        win.ViewModel.Name = "third-party/libgit2";
        win.ViewModel.Path = "third-party/libgit2";
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(win.ViewModel.IsValid);
        var r = win.GetResult();
        Assert.NotNull(r);
        Assert.Equal("third-party/libgit2", r!.Value.Name);
        Assert.False(r.Value.Force);

        win.ViewModel.ForceDelete = true;
        var r2 = win.GetResult();
        Assert.True(r2!.Value.Force);
        win.Close();
    }

    [AvaloniaFact]
    public void M18_DeleteSubmodule_EmptyName_NullResult()
    {
        var win = new DeleteSubmoduleWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.False(win.ViewModel.IsValid);
        Assert.Null(win.GetResult());
        win.Close();
    }

    [AvaloniaFact]
    public void M18_RebaseBranch_AllOptionsAndDependentBranches()
    {
        var win = new RebaseBranchWindow();
        win.ViewModel.SourceText = "feature/foo · 1111";
        win.ViewModel.DestinationText = "main · 2222";
        win.ViewModel.DependentBranches.Add("feature/sub1");
        win.ViewModel.DependentBranches.Add("feature/sub2");
        win.ViewModel.UpdateRefs = true;
        win.ViewModel.Autostash = true;
        win.Show();
        Dispatcher.UIThread.RunJobs();

        var r = win.GetResult();
        Assert.True(r.UpdateRefs);
        Assert.True(r.Autostash);
        Assert.Equal(2, r.DependentBranches.Count);
        Assert.Contains("feature/sub1", r.DependentBranches);
        win.Close();
    }

    [AvaloniaFact]
    public void M18_CreatePartialStash_MultiSelectFiles()
    {
        var win = new CreatePartialStashWindow();
        win.ViewModel.AvailableFiles.Add("src/foo.cs");
        win.ViewModel.AvailableFiles.Add("src/bar.cs");
        win.ViewModel.AvailableFiles.Add("README.md");
        win.ViewModel.SelectedFiles.Add("src/foo.cs");
        win.ViewModel.Message = "partial WIP";
        win.ViewModel.IncludeUntracked = true;
        win.Show();
        Dispatcher.UIThread.RunJobs();

        var r = win.GetResult();
        Assert.Equal("partial WIP", r.Message);
        Assert.True(r.IncludeUntracked);
        Assert.False(r.KeepIndex);
        Assert.Single(r.Files);
        Assert.Contains("src/foo.cs", r.Files);
        win.Close();
    }

    [AvaloniaFact]
    public void M18_AllDialogs_Screenshot()
    {
        var dir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "verification", "avalonia", "m18-medium-dialogs"));
        Directory.CreateDirectory(dir);

        var applyStash = new ApplyStashWindow { Width = 540, Height = 200 };
        applyStash.ViewModel.Stashes.Add(new StashItem { ReflogName = "stash@{0}", Subject = "WIP: feature" });
        applyStash.ViewModel.SelectedStash = applyStash.ViewModel.Stashes[0];
        applyStash.ViewModel.RestoreIndex = true;

        var cherryPick = new CherryPickWindow { Width = 620, Height = 320 };
        cherryPick.ViewModel.RevisionText = "abc1234 · commit subject";
        cherryPick.ViewModel.AvailableParents.Add("abc1234^");
        cherryPick.ViewModel.ParentToSkipText = "abc1234^";
        cherryPick.ViewModel.AppendOriginSha = true;

        var delSub = new DeleteSubmoduleWindow { Width = 500, Height = 220 };
        delSub.ViewModel.Name = "third-party/libgit2";
        delSub.ViewModel.Path = "third-party/libgit2";
        delSub.ViewModel.ForceDelete = true;

        var rebase = new RebaseBranchWindow { Width = 570, Height = 360 };
        rebase.ViewModel.SourceText = "feature/foo · 1111";
        rebase.ViewModel.DestinationText = "main · 2222";
        rebase.ViewModel.DependentBranches.Add("feature/sub1");
        rebase.ViewModel.DependentBranches.Add("feature/sub2");
        rebase.ViewModel.UpdateRefs = true;
        rebase.ViewModel.Autostash = true;

        var partialStash = new CreatePartialStashWindow { Width = 620, Height = 320 };
        partialStash.ViewModel.Message = "partial WIP";
        partialStash.ViewModel.AvailableFiles.Add("src/foo.cs");
        partialStash.ViewModel.AvailableFiles.Add("src/bar.cs");
        partialStash.ViewModel.AvailableFiles.Add("README.md");
        partialStash.ViewModel.SelectedFiles.Add("src/foo.cs");
        partialStash.ViewModel.IncludeUntracked = true;

        var wins = new (Window Win, string Name)[]
        {
            (applyStash, "avalonia-m18-applystash.png"),
            (cherryPick, "avalonia-m18-cherrypick.png"),
            (delSub, "avalonia-m18-deletesubmodule.png"),
            (rebase, "avalonia-m18-rebasebranch.png"),
            (partialStash, "avalonia-m18-createpartialstash.png"),
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
        Assert.True(total > 5000);
    }
}
