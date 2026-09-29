namespace SP621E.Core.Controllers;

/// <summary>
/// What a controller can actually do. The M4 "streaming vs parametric" decision is
/// hardware-blocked, so the seam deliberately supports either (or both) paths:
///
///  * FrameStreaming — the host renders arbitrary RGB frames and pushes them.
///  * Parametric — the host sends high-level commands (power/color/brightness/effect)
///    and the device renders internally.
///
/// A real SP621E may support only one. Code must check/use the flag and never assume.
/// </summary>
[Flags]
public enum ControllerCapability
{
    /// <summary>No control surface (placeholder only).</summary>
    None = 0,

    /// <summary>Accepts rendered RGB frames via <c>ILedController.SendFrameAsync</c>.</summary>
    FrameStreaming = 1 << 0,

    /// <summary>Accepts high-level commands via <c>IParametricController</c>.</summary>
    Parametric = 1 << 1,
}