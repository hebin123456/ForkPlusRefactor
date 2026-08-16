using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ForkPlus.Avalonia.Controls;
using ForkPlus.Avalonia.Dialogs;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M11 阶段：第三批"含 GitPointView / 自定义控件"对话框的测试。
/// 覆盖：CreateTag / CreateBranch / TrackRemoteBranch / OpenRepositoryAlert，
/// 以及 5 个 WPF 自定义控件 stub (GitPointView / ReferenceTextBox / PlaceholderTextBox /
/// BindableGitPointView / AutoTooltipTextBlock)。
/// </summary>
public class M11FormDialogsTests
{
    [AvaloniaFact]
    public void M11_CreateTag_AllFieldsPropagate()
    {
        var win = new CreateTagWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(string.Empty, win.ViewModel.Name);
        Assert.Equal(string.Empty, win.ViewModel.Message);
        Assert.False(win.ViewModel.Push);
        Assert.Null(win.GetResult()); // 空 name -> null

        win.ViewModel.Name = "v1.0.0";
        win.ViewModel.Message = "Release 1.0";
        win.ViewModel.Push = true;
        var r = win.GetResult();
        Assert.NotNull(r);
        Assert.Equal("v1.0.0", r!.Value.Name);
        Assert.Equal("Release 1.0", r.Value.Message);
        Assert.True(r.Value.Push);
        win.Close();
    }

    [AvaloniaFact]
    public void M11_CreateBranch_AllFieldsPropagate()
    {
        var win = new CreateBranchWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(win.ViewModel.CheckoutAfterCreate); // 默认
        Assert.Equal(CreateBranchViewModel.LocalChangesOption.DoNotChange, win.ViewModel.LocalChanges);

        win.ViewModel.Name = "feature/x";
        win.ViewModel.CheckoutAfterCreate = false;
        win.ViewModel.LocalChanges = CreateBranchViewModel.LocalChangesOption.StashAndReapply;
        var r = win.GetResult();
        Assert.NotNull(r);
        Assert.Equal("feature/x", r!.Value.Name);
        Assert.False(r.Value.Checkout);
        Assert.Equal(CreateBranchViewModel.LocalChangesOption.StashAndReapply, r.Value.Option);
        win.Close();
    }

    [AvaloniaFact]
    public void M11_TrackRemoteBranch_FieldsPropagate()
    {
        var win = new TrackRemoteBranchWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();

        win.ViewModel.LocalBranchName = "main";
        var r = win.GetResult();
        Assert.NotNull(r);
        Assert.Equal("main", r!.Value.LocalBranchName);
        win.Close();
    }

    [AvaloniaFact]
    public void M11_OpenRepositoryAlert_MessageAndButton()
    {
        var win = new OpenRepositoryAlertWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Open Repository", win.ViewModel.Title);
        win.ViewModel.Message = "Repo not found";
        win.ViewModel.FirstButtonText = "Browse...";
        Assert.Equal("Repo not found", win.ViewModel.Message);
        Assert.Equal("Browse...", win.ViewModel.FirstButtonText);
        win.Close();
    }

    [AvaloniaFact]
    public void M11_CustomControls_RenderAndPropagate()
    {
        // 验证 5 个 WPF 自定义控件 stub 在 Avalonia 中可用
        var gpv = new GitPointView { Text = "main · abc1234 Initial commit" };
        Assert.Equal("main · abc1234 Initial commit", gpv.Text);

        var rtb = new ReferenceTextBox();
        rtb.Placeholder = "Branch name";
        Assert.Equal("Branch name", rtb.Placeholder);
        rtb.Text = "feature";
        Assert.Equal("feature", rtb.Text);

        var ptb = new PlaceholderTextBox();
        ptb.Placeholder = "Type here";
        ptb.Text = "Hello";
        ptb.AcceptsReturn = true;
        ptb.TextWrapping = global::Avalonia.Media.TextWrapping.Wrap;
        Assert.Equal("Type here", ptb.Placeholder);
        Assert.Equal("Hello", ptb.Text);
        Assert.True(ptb.AcceptsReturn);
        Assert.Equal(global::Avalonia.Media.TextWrapping.Wrap, ptb.TextWrapping);

        var bgpv = new BindableGitPointView { Subject = "feat: x", Detail = "by Hebin" };
        Assert.Equal("feat: x", bgpv.Subject);
        Assert.Equal("by Hebin", bgpv.Detail);

        var attb = new AutoTooltipTextBlock { Text = "very long commit message…" };
        Assert.Equal("very long commit message…", attb.Text);
    }

    [AvaloniaFact]
    public void M11_AllDialogs_Screenshot()
    {
        var dir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "verification", "avalonia", "m11-dialogs"));
        Directory.CreateDirectory(dir);

        var createTag = new CreateTagWindow { Width = 590, Height = 320 };
        createTag.ViewModel.Name = "v1.0.0";
        createTag.ViewModel.Message = "First release";

        var createBranch = new CreateBranchWindow { Width = 600, Height = 280 };
        createBranch.ViewModel.Name = "feature/login";

        var wins = new (Window Win, string Name)[]
        {
            (createTag, "avalonia-m11-createtag.png"),
            (createBranch, "avalonia-m11-createbranch.png"),
            (new TrackRemoteBranchWindow { Width = 540, Height = 220 }, "avalonia-m11-trackremote.png"),
            (new OpenRepositoryAlertWindow { Width = 620, Height = 150 }, "avalonia-m11-openalert.png"),
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
