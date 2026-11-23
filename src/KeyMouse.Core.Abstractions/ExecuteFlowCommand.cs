namespace KeyMouse.Flow;

/// <summary>Execute a resolved leaf in the caller's current dry-run and cancellation scope.</summary>
internal delegate int ExecuteFlowCommand(FlowStep step, bool allowRestore);
