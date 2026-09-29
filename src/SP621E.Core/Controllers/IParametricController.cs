using SP621E.Core.LedFrame;

namespace SP621E.Core.Controllers;

/// <summary>
/// High-level, device-rendered control surface: power, color, brightness, built-in
/// effects. Only valid on controllers exposing <see cref="ControllerCapability.Parametric"/>.
///
/// These are deliberately NOT frame-oriented: a parametric device has no pixel buffer
/// the host can address, so the host sends intent and the device renders internally.
/// The wire encoding of each command is controller-specific (learned in Milestone 3
/// for the SP621E; never invented).
/// </summary>
public interface IParametricController
{
    /// <summary>Names of built-in effects this device reports, if enumerable; otherwise empty.</summary>
    IReadOnlyList<string> EffectNames { get; }

    Task SetPowerAsync(bool on, CancellationToken ct = default);

    Task SetColorAsync(Rgb color, CancellationToken ct = default);

    /// <summary><paramref name="brightness"/> in [0, 1] interpreted by the device's own range.</summary>
    Task SetBrightnessAsync(double brightness, CancellationToken ct = default);

    /// <summary>Select a built-in effect by name. Valid names come from <see cref="EffectNames"/>.</summary>
    Task SetEffectAsync(string effectName, CancellationToken ct = default);
}