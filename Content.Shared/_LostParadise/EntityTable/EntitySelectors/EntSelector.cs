using Content.Shared._LostParadise.EntityTable.ValueSelector;
using Robust.Shared.Prototypes;

namespace Content.Shared._LostParadise.EntityTable.EntitySelectors;

/// <summary>
/// Gets the spawn for the entity prototype specified at whatever count specified.
/// </summary>
public sealed partial class LPEntSelector : LPEntityTableSelector
{
    public const string IdDataFieldTag = "id";

    [DataField(IdDataFieldTag, required: true)]
    public EntProtoId Id;

    [DataField]
    public LPNumberSelector Amount = new LPConstantNumberSelector(1);

    protected override IEnumerable<EntProtoId> GetSpawnsImplementation(System.Random rand,
        IEntityManager entMan,
        IPrototypeManager proto)
    {
        var num = (int) Math.Round(Amount.Get(rand, entMan, proto));
        for (var i = 0; i < num; i++)
        {
            yield return Id;
        }
    }
}
