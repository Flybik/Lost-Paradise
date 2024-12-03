using Content.Server.Spawners.EntitySystems;
using Content.Shared._LostParadise.EntityTable.EntitySelectors;
using Robust.Shared.Prototypes;

namespace Content.Server._LostParadise.Spawners.Components;

[RegisterComponent, EntityCategory("Spawner"), Access(typeof(ConditionalSpawnerSystem))]
public sealed partial class LPEntityTableSpawnerComponent : Component
{
    /// <summary>
    /// Table that determines what gets spawned.
    /// </summary>
    [DataField(required: true)]
    public LPEntityTableSelector Table = default!;

    /// <summary>
    /// Scatter of entity spawn coordinates
    /// </summary>
    [DataField]
    public float Offset = 0.2f;

    /// <summary>
    /// A variable meaning whether the spawn will
    /// be able to be used again or whether
    /// it will be destroyed after the first use
    /// </summary>
    [DataField]
    public bool DeleteSpawnerAfterSpawn = true;
}

