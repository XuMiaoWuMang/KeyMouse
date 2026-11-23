namespace KeyMouse.Runtime;

internal static class FlowCommandExecutor
{
    internal static int Execute(FlowStep step, bool allowRestore)
    {
        try
        {
            if (ClientMouse.Handles(step)) return ClientMouse.Execute(step);
            var g = new CommandLine.GlobalOptions
            {
                ClassName = NonBlank(step.Target?.Class),
                ProcessName = NonBlank(step.Target?.Process),
                AllowRestore = allowRestore,
            };
            (int X, int Y)? Point(FlowPoint? p, bool end = false)
            {
                if (p == null) return null;
                if (p.Space == FlowSpace.Screen) return (p.X, p.Y);
                if (p.Space != FlowSpace.Client) throw new CommandFailure(2, $"不认识的坐标空间 {p.Space}");
                if (end) (g.Wx2, g.Wy2) = (p.X, p.Y); else (g.Wx, g.Wy) = (p.X, p.Y);
                return null;
            }
            string button = step.Button ?? "left", key = step.Combo ?? step.Text ?? "";
            return step.Type switch
            {
                "keyhold" or "mousehold" or "hold" => Hold(g, step, Point(step.At)),
                "focus" => CommandTarget.Focus(g),
                "type" => KeyboardCommands.Type(g, step.Text ?? "", step.IntervalMs ?? 15),
                "key" => KeyboardCommands.Combo(g, key, 30),
                "keydown" or "keyup" => KeyboardCommands.KeyState(g, key, step.Type == "keyup"),
                "mousedown" or "mouseup" => MouseCommands.Button(g, button, step.Type == "mouseup"),
                "move" => MouseCommands.Move(g, Point(step.At)),
                "click" => MouseCommands.Click(g, Point(step.At), button),
                "wheel" when step.Axis == null || step.Axis.Equals(FlowAxis.Vertical, StringComparison.OrdinalIgnoreCase) || step.Axis.Equals(FlowAxis.Horizontal, StringComparison.OrdinalIgnoreCase) =>
                    MouseCommands.Wheel(g, Point(step.At), step.Delta ?? 120, string.Equals(step.Axis, FlowAxis.Horizontal, StringComparison.OrdinalIgnoreCase)),
                "drag" when step.From != null && step.To != null && step.From.Space == step.To.Space =>
                    MouseCommands.Drag(g, Point(step.From), Point(step.To, true), button, 20, step.DurationMs ?? 400),
                _ => CommandLine.Fail(2, $"无效的 {step.Type} 步骤"),
            };
        }
        catch (CommandFailure ex) { return CommandLine.Fail(ex.Code, ex.Message); }
        catch (ArgumentException ex) { return CommandLine.Fail(2, ex.Message); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return CommandLine.Fail(1, ex.Message); }
    }

    private static int Hold(CommandLine.GlobalOptions g, FlowStep step, (int X, int Y)? absolute)
    {
        int ms = step.Ms ?? 0;
        if (FlowSchema.TryGetNumberRule(step.Type, "ms", out var rule)) ms = rule.Clamp(ms);
        bool mouse = step.Type == "mousehold" || (step.Type == "hold" && !string.IsNullOrEmpty(step.Button));
        var target = CommandTarget.PrepareTarget(g);
        var at = CommandTarget.RelativePoint(g, target, step.Type) ?? absolute;
        if (mouse)
        {
            string button = step.Button ?? "left";
            NativeInput.ValidateButton(button);
            if (!ExecutionMode.DryRun)
            {
                if (at is { } p) NativeInput.MoveTo(p.X, p.Y);
                NativeInput.ButtonDown(button);
                try { Thread.Sleep(ms); } finally { NativeInput.ButtonUp(button); }
            }
        }
        else
        {
            var (vk, ext) = KeyMap.Resolve(step.Combo ?? step.Text ?? "");
            if (!ExecutionMode.DryRun)
            {
                NativeInput.Key(vk, ext, false);
                try
                {
                    int delay = Math.Min(ms, 500);
                    Thread.Sleep(delay);
                    for (int elapsed = delay; elapsed < ms; elapsed += 33)
                    { NativeInput.Key(vk, ext, false); Thread.Sleep(Math.Min(33, ms - elapsed)); }
                }
                finally { NativeInput.Key(vk, ext, true); }
            }
        }
        CommandOutput.Out.WriteLine($"长按 {step.Button ?? step.Combo ?? step.Text} {ms} ms" + (ExecutionMode.DryRun ? "（试运行未真的按）" : ""));
        return 0;
    }

    private static string? NonBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
