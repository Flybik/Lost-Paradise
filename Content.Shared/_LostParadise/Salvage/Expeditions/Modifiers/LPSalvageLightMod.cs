using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Shared._LostParadise.Salvage.Expeditions.Modifiers;

[Prototype("LPsalvageLightMod")]
public sealed partial class LPSalvageLightMod : IPrototype, ILPBiomeSpecificMod
{
    [IdDataField] public string ID { get; } = default!;

    [DataField("desc")] public LocId Description { get; private set; } = string.Empty;

    /// <inheritdoc/>
    [DataField("cost")]
    public float Cost { get; private set; } = 0f;

    /// <inheritdoc/>
    [DataField("biomes", customTypeSerializer: typeof(PrototypeIdListSerializer<LPSalvageBiomeMod>))]
    public List<string>? Biomes { get; private set; } = null;

    [DataField("color", required: true)] public Color? Color;
}
