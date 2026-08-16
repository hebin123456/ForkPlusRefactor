using System.Collections.Generic;

namespace ForkPlus.Avalonia.Dialogs;

public sealed class CherryPickViewModel
{
    public string DialogTitle { get; set; } = "Cherry-Pick";
    public string DialogDescription { get; set; } = "Apply the change introduced by an existing commit";
    public string SubmitButtonTitle { get; set; } = "Cherry-Pick";

    public string RevisionLabel { get; set; } = "Revision:";
    public string ParentToSkipLabel { get; set; } = "Parent to skip:";
    public string CommitLabel { get; set; } = "Commit the changes";
    public string CommitToolTip { get; set; } = "--no-commit (when unchecked)";
    public string AppendOriginLabel { get; set; } = "Append origin to commit message";
    public string AppendOriginToolTip { get; set; } = "-x";
    public string SignOffLabel { get; set; } = "Sign off the commit message";
    public string SignOffToolTip { get; set; } = "--signoff";

    public string RevisionText { get; set; } = string.Empty;
    public string ParentToSkipText { get; set; } = string.Empty;

    public List<string> AvailableParents { get; set; } = new();

    public bool CreateCommit { get; set; } = true;
    public bool AppendOriginSha { get; set; }
    public bool SignOff { get; set; }
}
