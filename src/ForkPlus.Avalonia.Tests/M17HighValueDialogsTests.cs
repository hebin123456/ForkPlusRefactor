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
/// M17 阶段：简单高价值弹窗。
/// 覆盖：AddGitIgnorePattern（双多行 TextBox + IsValid）、
///       AddCustomRefspec（单 ReferenceTextBox）、
///       TrackRemoteBranch（GitPointView + ReferenceTextBox + 3 选 RadioButton）、
///       CheckoutBranch（双 GitPointView + 3 选 RadioButton）。
/// </summary>
public class M17HighValueDialogsTests
{
    [AvaloniaFact]
    public void M17_AddGitIgnorePattern_ValidationAndResult()
    {
        var win = new AddGitIgnorePatternWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.False(win.ViewModel.IsValid);
        Assert.Null(win.GetResult());

        win.ViewModel.Pattern = "*.bak\nbuild/\n";
        win.ViewModel.Preview = "**/foo.bak\nbuild/";
        Assert.True(win.ViewModel.IsValid);
        Assert.Equal("*.bak\nbuild/\n", win.GetResult());
        win.Close();
    }

    [AvaloniaFact]
    public void M17_AddCustomRefspec_ValidationAndResult()
    {
        var win = new AddCustomRefspecWindow { Width = 500, Height = 160 };
        win.ViewModel.RemoteName = "origin";
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.False(win.ViewModel.IsValid);
        Assert.Null(win.GetResult());

        win.ViewModel.BranchName = "refs/heads/feature/custom";
        Assert.True(win.ViewModel.IsValid);
        Assert.Equal("refs/heads/feature/custom", win.GetResult());
        win.Close();
    }

    [AvaloniaFact]
    public void M17_TrackRemoteBranch_ValidationAndRadioEnum()
    {
        var win = new TrackRemoteBranchWindow();
        win.ViewModel.RemoteBranchText = "origin/feature/foo · abc1234";
        win.ViewModel.LocalChangesOption = LeanBranchingLocalChanges.StashAndReapply;
        win.Show();
        Dispatcher.UIThread.RunJobs();

        // 默认 local name 为空 → invalid
        Assert.False(win.ViewModel.IsValid);
        Assert.Null(win.GetResult());

        win.ViewModel.LocalBranchName = "feature/foo";
        Assert.True(win.ViewModel.IsValid);

        var r = win.GetResult();
        Assert.NotNull(r);
        Assert.Equal("feature/foo", r!.Value.LocalBranchName);
        Assert.Equal(LeanBranchingLocalChanges.StashAndReapply, r.Value.Option);
        win.Close();
    }

    [AvaloniaFact]
    public void M17_CheckoutBranch_GetResultEcho()
    {
        var win = new CheckoutBranchWindow();
        win.ViewModel.SwitchToText = "develop · def5678";
        win.ViewModel.FastForwardText = "origin/develop · def5678";
        win.ViewModel.LocalChangesOption = LeanBranchingLocalChanges.Discard;
        win.Show();
        Dispatcher.UIThread.RunJobs();

        var r = win.GetResult();
        Assert.Equal("develop · def5678", r.SwitchTo);
        Assert.Equal(LeanBranchingLocalChanges.Discard, r.Option);
        win.Close();
    }

    [AvaloniaFact]
    public void M17_AllDialogs_Screenshot()
    {
        var dir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "verification", "avalonia", "m17-high-value-dialogs"));
        Directory.CreateDirectory(dir);

        var gitignore = new AddGitIgnorePatternWindow { Width = 670, Height = 320 };
        gitignore.ViewModel.Pattern = "*.bak\nbuild/\n*.tmp";
        gitignore.ViewModel.Preview = "**/foo.bak\nbuild/\nobj/x.tmp";

        var refspec = new AddCustomRefspecWindow { Width = 500, Height = 160 };
        refspec.ViewModel.RemoteName = "origin:";
        refspec.ViewModel.BranchName = "refs/heads/feature/custom";

        var track = new TrackRemoteBranchWindow { Width = 540, Height = 260 };
        track.ViewModel.RemoteBranchText = "origin/feature/foo · abc1234";
        track.ViewModel.LocalBranchName = "feature/foo";
        track.ViewModel.LocalChangesOption = LeanBranchingLocalChanges.StashAndReapply;

        var checkout = new CheckoutBranchWindow { Width = 580, Height = 320 };
        checkout.ViewModel.SwitchToText = "develop · def5678";
        checkout.ViewModel.FastForwardText = "origin/develop · def5678";
        checkout.ViewModel.LocalChangesOption = LeanBranchingLocalChanges.Discard;

        var wins = new (Window Win, string Name)[]
        {
            (gitignore, "avalonia-m17-addgitignorepattern.png"),
            (refspec, "avalonia-m17-addcustomrefspec.png"),
            (track, "avalonia-m17-trackremotebranch.png"),
            (checkout, "avalonia-m17-checkoutbranch.png"),
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
