using System.Numerics;
using Content.Shared.Movement.Components;
using Content.Shared.Physics;
using Content.Shared._LostParadise.Salvage;
using Robust.Shared.Map;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._LostParadise.Salvage;

public sealed class LPRestrictedRangeSystem : SharedLPRestrictedRangeSystem
{
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LPRestrictedRangeComponent, MapInitEvent>(OnRestrictedMapInit);
    }

    private void OnRestrictedMapInit(EntityUid uid, LPRestrictedRangeComponent component, MapInitEvent args)
    {
        component.BoundaryEntity = CreateBoundary(new EntityCoordinates(uid, component.Origin), component.Range);
    }

    public EntityUid CreateBoundary(EntityCoordinates coordinates, float range)
    {
        var boundaryUid = Spawn(null, coordinates);
        var boundaryPhysics = AddComp<PhysicsComponent>(boundaryUid);
        var cShape = new ChainShape();
        // Don't need it to be a perfect circle, just need it to be loosely accurate.
        cShape.CreateLoop(Vector2.Zero, range + 0.25f, false, count: 4);
        _fixtures.TryCreateFixture(
            boundaryUid,
            cShape,
            "boundary",
            collisionLayer: (int) (CollisionGroup.HighImpassable | CollisionGroup.Impassable | CollisionGroup.LowImpassable),
            body: boundaryPhysics);
        _physics.WakeBody(boundaryUid, body: boundaryPhysics);
        AddComp<BoundaryComponent>(boundaryUid);
        return boundaryUid;
    }
}
