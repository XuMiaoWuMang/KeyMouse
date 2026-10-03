namespace KeyMouse;

/// <summary>
/// Which field groups a step type actually uses.
///
/// This lives in the format's own code, not in the editor, because it is a property of the format: the
/// loader already knows that a `click-text` needs a region and a match mode, and the inspector must not
/// be able to drift away from that. The editor asks this table; a unit test walks <see cref="FlowDocument.KnownTypes"/>
/// and refuses to let a type exist without an entry here.
/// </summary>
[Flags]
internal enum StepFields
{
    None = 0,
    Target = 1 << 0,
    Point = 1 << 1,
    Region = 1 << 2,
    Text = 1 << 3,
    Match = 1 << 4,
    Timeout = 1 << 5,
    Button = 1 << 6,
    Combo = 1 << 7,
    Ms = 1 << 8,
    Delta = 1 << 9,
    Duration = 1 << 10,
    DragEnds = 1 << 11,
    Note = 1 << 12,
    Into = 1 << 13,
    Group = 1 << 14,
    Call = 1 << 15,
    When = 1 << 16,
}

internal static class FlowStepSchema
{
    /// <summary>
    /// The fields each type uses. Keep this beside the loader: when a type gains a field, the entry
    /// here changes in the same commit, and the editor picks it up without being told twice.
    /// </summary>
    internal static StepFields For(string type) => type switch
    {
        "focus" => StepFields.Target | StepFields.When | StepFields.Note,
        "click" => StepFields.Target | StepFields.Point | StepFields.Button | StepFields.When | StepFields.Note,
        "drag" => StepFields.Target | StepFields.Point | StepFields.Button | StepFields.Duration |
                  StepFields.DragEnds | StepFields.When | StepFields.Note,
        "move" => StepFields.Target | StepFields.Point | StepFields.When | StepFields.Note,
        "wheel" => StepFields.Target | StepFields.Point | StepFields.Delta | StepFields.When | StepFields.Note,
        "type" => StepFields.Target | StepFields.Text | StepFields.When | StepFields.Note,
        "key" => StepFields.Target | StepFields.Combo | StepFields.Text | StepFields.When | StepFields.Note,
        "sleep" => StepFields.Ms | StepFields.When,
        "wait-window" => StepFields.Target | StepFields.Timeout | StepFields.When | StepFields.Note,
        "wait-text" => StepFields.Target | StepFields.Region | StepFields.Text | StepFields.Match |
                       StepFields.Timeout | StepFields.When | StepFields.Note,
        "click-text" => StepFields.Target | StepFields.Region | StepFields.Text | StepFields.Match |
                        StepFields.Timeout | StepFields.Button | StepFields.When | StepFields.Note,
        "read-text" => StepFields.Target | StepFields.Region | StepFields.Text | StepFields.Match |
                       StepFields.Into | StepFields.When | StepFields.Note,
        "repeat" => StepFields.Group | StepFields.Note,
        "foreach" => StepFields.Group | StepFields.Note,
        "call" => StepFields.Call | StepFields.Note,
        _ => StepFields.Target | StepFields.When | StepFields.Note,
    };
}
