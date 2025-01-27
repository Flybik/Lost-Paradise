using Robust.Shared.Prototypes;

namespace Content.Shared._LostParadise.EntityTable.EntitySelectors;

/// <summary>
/// Gets the spawns from the entity table prototype specified.
/// Can be used to reuse common tables.
/// </summary>
public sealed partial class LPNestedSelector : LPEntityTableSelector
{
    [DataField(required: true)]
    public ProtoId<LPEntityTablePrototype> TableId;

    protected override IEnumerable<EntProtoId> GetSpawnsImplementation(System.Random rand,
        IEntityManager entMan,
        IPrototypeManager proto)
    {
        return proto.Index(TableId).Table.GetSpawns(rand, entMan, proto);
    }
}
