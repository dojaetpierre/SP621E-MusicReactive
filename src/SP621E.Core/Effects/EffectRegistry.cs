namespace SP621E.Core.Effects;

using System.Collections.ObjectModel;
using SP621E.Core.Effects.EffectLibrary;

/// <summary>
/// Registry of all effect instances available to the pipeline.
/// </summary>
public sealed class EffectRegistry
{
    public static EffectRegistry CreateDefault()
    {
        var registry = new EffectRegistry();
        registry.Effects.Add(new SolidEffect());
        registry.Effects.Add(new BassPulseEffect());
        registry.Effects.Add(new StrobeEffect());
        registry.Effects.Add(new SpectrumEffect());
        registry.Effects.Add(new RainbowWaveEffect());
        registry.Effects.Add(new BreatheEffect());
        registry.Effects.Add(new BeatSnapEffect());
        registry.Effects.Add(new RunnerEffect());
        return registry;
    }

    public ObservableCollection<IEffect> Effects { get; } = [];

    public IEffect? ActiveEffect { get; set; }
}