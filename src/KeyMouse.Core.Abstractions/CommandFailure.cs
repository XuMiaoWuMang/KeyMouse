namespace KeyMouse.Windowing;

internal sealed class CommandFailure : Exception
{
    public int Code { get; }

    public CommandFailure(int code, string message) : base(message) => Code = code;
}
