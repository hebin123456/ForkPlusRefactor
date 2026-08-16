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
/// M14 阶段：文本输入 + 勾选的对话框（无业务逻辑，纯表单 + CheckBox）。
/// 覆盖：RenameLocalBranch（ReferenceTextBox + 远端重命名勾选）、
///       SaveSnapshot（PlaceholderTextBox + Stage new files 勾选）、
///       SaveAsPatch（ItemsControl 列表）、
///       RevertRevision（单/合并模式 + parent ComboBox + commit 勾选）。
/// </summary>
public class M14TextInputDialogsTests
{
    [AvaloniaFact]
    public void M14_RenameLocalBranch_AllFieldsPropagate()
    {
        var win = new RenameLocalBranchWindow();
        win.ViewModel.CurrentBranchText = "feature/old · abc1234";
        win.ViewModel.RenameRemoteCheckboxLabel = "Also rename origin/feature/old";
        win.Show();
        Dispatcher.UIThread.RunJobs();

        // 空名 → null
        Assert.Null(win.GetResult());

        // 设置新名
        win.ViewModel.NewName = "feature/new";
        win.ViewModel.RenameRemoteChecked = true;
        var r = win.GetResult();
        Assert.NotNull(r);
        Assert.Equal("feature/new", r!.Value.NewName);
        Assert.True(r.Value.RenameRemote);

        // 关闭
        win.Close();
    }

    [AvaloniaFact]
    public void M14_SaveSnapshot_MessageAndStagePropagate()
    {
        var win = new SaveSnapshotWindow();
        win.ViewModel.CurrentHeadText = "main · abc1234 Initial commit";
        win.Show();
        Dispatcher.UIThread.RunJobs();

        // 默认 stage new files = false
        Assert.False(win.ViewModel.StageNewFilesChecked);
        var r = win.GetResult();
        Assert.Equal(string.Empty, r.Message);
        Assert.False(r.StageNewFiles);

        // 填写
        win.ViewModel.Message = "WIP on feature";
        win.ViewModel.StageNewFilesChecked = true;
        var r2 = win.GetResult();
        Assert.Equal("WIP on feature", r2.Message);
        Assert.True(r2.StageNewFiles);

        win.Close();
    }

    [AvaloniaFact]
    public void M14_SaveAsPatch_RevisionListPropagates()
    {
        var win = new SaveAsPatchWindow();
        win.ViewModel.RevisionsLabel = "3 revisions selected";
        win.ViewModel.Revisions.Add(new SaveAsPatchRevisionItem { Subject = "feat: x", Sha = "abc1234" });
        win.ViewModel.Revisions.Add(new SaveAsPatchRevisionItem { Subject = "fix: y", Sha = "def5678" });
        win.ViewModel.Revisions.Add(new SaveAsPatchRevisionItem { Subject = "docs: z", Sha = "9abcde0" });
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("3 revisions selected", win.ViewModel.RevisionsLabel);
        Assert.Equal(3, win.ViewModel.Revisions.Count);
        win.Close();
    }

    [AvaloniaFact]
    public void M14_RevertRevision_SingleMode_Defaults()
    {
        var win = new RevertRevisionWindow();
        win.ViewModel.RevisionText = "abc1234 feat: add feature";
        win.Show();
        Dispatcher.UIThread.RunJobs();

        // 单 commit 模式：IsMergeRevision = false
        Assert.False(win.ViewModel.IsMergeRevision);
        Assert.True(win.ViewModel.CommitChecked); // 默认勾选
        var r = win.GetResult();
        Assert.True(r.Commit);
        Assert.Null(r.ParentIndex); // 单模式不传 parent
        win.Close();
    }

    [AvaloniaFact]
    public void M14_RevertRevision_MergeMode_ParentSelection()
    {
        var win = new RevertRevisionWindow();
        win.ViewModel.RevisionText = "9abcde0 Merge branch 'feature'";
        win.ViewModel.Parents.Add(new RevisionItem { Subject = "Initial commit", Sha = "1111111" });
        win.ViewModel.Parents.Add(new RevisionItem { Subject = "Merge feature", Sha = "2222222" });
        win.ViewModel.Parents.Add(new RevisionItem { Subject = "feat: x", Sha = "3333333" });
        win.Show();
        Dispatcher.UIThread.RunJobs();

        // 合并模式：IsMergeRevision = true
        Assert.True(win.ViewModel.IsMergeRevision);
        Assert.Equal(3, win.ViewModel.Parents.Count);

        // 选中第二个 parent
        win.ViewModel.SelectedParent = win.ViewModel.Parents[1];
        var r = win.GetResult();
        Assert.True(r.Commit);
        Assert.Equal(2, r.ParentIndex); // 1-based

        // 关闭
        win.Close();
    }

    [AvaloniaFact]
    public void M14_AllDialogs_Screenshot()
    {
        var dir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "verification", "avalonia", "m14-textinput-dialogs"));
        Directory.CreateDirectory(dir);

        var rename = new RenameLocalBranchWindow { Width = 500, Height = 200 };
        rename.ViewModel.CurrentBranchText = "feature/old · abc1234";
        rename.ViewModel.NewName = "feature/new";
        rename.ViewModel.RenameRemoteCheckboxLabel = "Also rename origin/feature/old";

        var snap = new SaveSnapshotWindow { Width = 590, Height = 230 };
        snap.ViewModel.CurrentHeadText = "main · abc1234 Initial commit";
        snap.ViewModel.Message = "WIP on feature";
        snap.ViewModel.StageNewFilesChecked = true;

        var patch = new SaveAsPatchWindow { Width = 590, Height = 320 };
        patch.ViewModel.RevisionsLabel = "3 revisions selected";
        patch.ViewModel.Revisions.Add(new SaveAsPatchRevisionItem { Subject = "feat: x", Sha = "abc1234" });
        patch.ViewModel.Revisions.Add(new SaveAsPatchRevisionItem { Subject = "fix: y", Sha = "def5678" });
        patch.ViewModel.Revisions.Add(new SaveAsPatchRevisionItem { Subject = "docs: z", Sha = "9abcde0" });

        var revertSingle = new RevertRevisionWindow { Width = 590, Height = 200 };
        revertSingle.ViewModel.RevisionText = "abc1234 feat: add feature";

        var revertMerge = new RevertRevisionWindow { Width = 590, Height = 230 };
        revertMerge.ViewModel.RevisionText = "9abcde0 Merge branch 'feature'";
        revertMerge.ViewModel.Parents.Add(new RevisionItem { Subject = "Initial commit", Sha = "1111111" });
        revertMerge.ViewModel.Parents.Add(new RevisionItem { Subject = "Merge feature", Sha = "2222222" });
        revertMerge.ViewModel.SelectedParent = revertMerge.ViewModel.Parents[1];

        var wins = new (Window Win, string Name)[]
        {
            (rename, "avalonia-m14-renamelocalbranch.png"),
            (snap, "avalonia-m14-savesnapshot.png"),
            (patch, "avalonia-m14-saveaspatch.png"),
            (revertSingle, "avalonia-m14-revertsingle.png"),
            (revertMerge, "avalonia-m14-revertmerge.png"),
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
