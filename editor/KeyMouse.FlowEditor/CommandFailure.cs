namespace KeyMouse;

/// <summary>
/// The console tool's abort type, repeated here because FlowModel.cs is linked into this assembly
/// and throws it. Same shape as the original (WindowFocus.cs) so the two can never mean different
/// things: a code and a message, and nothing was sent on the way out.
/// </summary>
internal sealed class CommandFailure : Exception
{
    public int Code { get; }

    public CommandFailure(int code, string message) : base(message) => Code = code;
}
