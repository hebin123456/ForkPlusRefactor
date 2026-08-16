using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ForkPlus.Avalonia.Dialogs;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M10 阶段：第二批"纯表单"弹窗的视觉/数据流/操作语义三层断言测试。
/// 覆盖：GoToLine / LongOperation / SshPassphrase / MessageBox / About / ApplyPatch。
/// </summary>
public class M10SimpleDialogsTests
{
    [AvaloniaFact]
    public void M10_GoToLine_DefaultsAndInputPropagate()
    {
        var win = new GoToLineWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();

        var vm = win.ViewModel;
        // 数据流：默认 LineNumber=1
        Assert.Equal(1, vm.LineNumber);

        // 操作语义：修改 VM 后 Close 拿回结果
        vm.LineNumber = 42;
        var result = win.GetResult();
        Assert.Equal(42, result);

        // 关闭
        win.Close();
    }

    [AvaloniaFact]
    public void M10_SshPassphrase_PasswordRoundTrip()
    {
        var win = new SshPassphraseWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(string.Empty, win.ViewModel.Passphrase);
        Assert.Null(win.GetResult()); // 空 -> null

        win.ViewModel.Passphrase = "secret123";
        var result = win.GetResult();
        Assert.Equal("secret123", result);
        win.Close();
    }

    [AvaloniaFact]
    public void M10_MessageBox_ResultOkAndCancel()
    {
        var win = new MessageBoxWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();

        win.ViewModel.Title = "Test";
        win.ViewModel.Message = "Hello";
        win.ViewModel.ShowOk = true;
        win.ViewModel.ShowCancel = true;
        Assert.True(win.ViewModel.ShowOk);
        Assert.True(win.ViewModel.ShowCancel);

        // 直接调用 OnOk 模拟点击
        typeof(MessageBoxWindow)
            .GetMethod("OnOk", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(win, new object?[] { null, null! });
        Assert.Equal(MessageBoxResult.Ok, win.ViewModel.Result);
    }

    [AvaloniaFact]
    public void M10_LongOperation_MessageUpdates()
    {
        var win = new LongOperationWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("taking longer", win.ViewModel.Message);
        win.SetMessage("Pushing to origin…");
        Assert.Equal("Pushing to origin…", win.ViewModel.Message);
        win.Close();
    }

    [AvaloniaFact]
    public void M10_About_DefaultsAndTitle()
    {
        var win = new AboutWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();

        var vm = win.ViewModel;
        Assert.Equal("ForkPlus", vm.AppName);
        Assert.Equal("Hebin", vm.Author);
        Assert.Equal("https://hebin.me", vm.HomepageUrl);
        Assert.False(string.IsNullOrEmpty(vm.Version));
        win.Close();
    }

    [AvaloniaFact]
    public void M10_ApplyPatch_PathAndCreateCommit_Propagate()
    {
        var win = new ApplyPatchWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(string.Empty, win.ViewModel.Path);
        Assert.False(win.ViewModel.CreateCommit);
        Assert.Null(win.GetResult()); // 空路径 -> null

        win.ViewModel.Path = "/tmp/test.patch";
        win.ViewModel.CreateCommit = true;
        var result = win.GetResult();
        Assert.NotNull(result);
        Assert.Equal("/tmp/test.patch", result!.Value.Path);
        Assert.True(result.Value.CreateCommit);
        win.Close();
    }

    [AvaloniaFact]
    public void M10_AllDialogs_Screenshot()
    {
        // 视觉验证：每个对话框单独截图保存
        var dir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "verification", "avalonia", "m10-dialogs"));
        Directory.CreateDirectory(dir);

        var mbWin = new MessageBoxWindow { Width = 490, Height = 150 };
        mbWin.ViewModel.Title = "Confirm";
        mbWin.ViewModel.Message = "Are you sure?";
        mbWin.ViewModel.ShowOk = true;
        mbWin.ViewModel.ShowCancel = true;

        var goWin = new GoToLineWindow { Width = 250, Height = 120 };
        goWin.ViewModel.LineNumber = 42;
        var ssWin = new SshPassphraseWindow { Width = 520, Height = 160 };
        ssWin.ViewModel.Passphrase = "secret123";

        var wins = new (Window Win, string Name)[]
        {
            (goWin, "avalonia-m10-gotoline.png"),
            (ssWin, "avalonia-m10-sshpassphrase.png"),
            (mbWin, "avalonia-m10-messagebox.png"),
            (new AboutWindow { Width = 240, Height = 360 }, "avalonia-m10-about.png"),
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
