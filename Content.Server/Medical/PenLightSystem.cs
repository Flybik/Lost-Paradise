using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Server.PowerCell;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Drugs;
using Content.Shared.Drunk;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Interaction;
using Content.Shared.Medical;
using Content.Shared.Mobs.Systems;
using Content.Shared.Traits.Assorted.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Medical;
/// <summary>
/// This stores the eye exam system for <see cref="PenLightComponent"/>
/// </summary>
public sealed class PenLightSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PenLightComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<PenLightComponent, PenLightDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(EntityUid uid, PenLightComponent component, AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;
        args.Handled = TryStartExam(uid, target, args.User, component);
    }

    private void OnDoAfter(Entity<PenLightComponent> uid, ref PenLightDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target == null || !_powerCell.HasDrawCharge(uid, user: args.User))
            return;

        OpenUserInterface(args.User, uid);
        Diagnose(uid, args.Target.Value);
        args.Handled = true;
    }


    /// <summary>
    /// Actually handles the exam interaction.
    /// </summary>
    public bool TryStartExam(EntityUid uid, EntityUid target, EntityUid user, PenLightComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, component.DoAfterDuration, new PenLightDoAfterEvent(),
            uid, target, uid)
        {
            BlockDuplicate = true,
            BreakOnUserMove = true,
            BreakOnTargetMove = true,
            BreakOnHandChange = true,
            NeedHand = true
        });
    }
    private void OpenUserInterface(EntityUid user, EntityUid penlight)
    {
        if (!TryComp<ActorComponent>(user, out var actor) || !_uiSystem.TryGetUi(penlight, PenLightUiKey.Key, out var ui))
            return;

        _uiSystem.OpenUi(ui, actor.PlayerSession);
    }

    /// <summary>
    /// Runs the checks for the different types of eye damage
    /// </summary>
    private void Diagnose(EntityUid penlight, EntityUid target)
    {
        if (!_uiSystem.TryGetUi(penlight, PenLightUiKey.Key, out var ui))
            return;

        if (!HasComp<DamageableComponent>(target))
            return;
        // Blind
        var blind = _entityManager.HasComponent<PermanentBlindnessComponent>(target);

        // Drunk
        var drunk = !_entityManager.HasComponent<DrunkComponent>(target);

        // EyeDamage

        var eyeDamage = false;
        if (TryComp<BlindableComponent>(target, out var eyeDam))
        {
            eyeDamage = eyeDam.EyeDamage > 0;
        }

        // seeingRainbows
        var seeingRainbows = _entityManager.HasComponent<SeeingRainbowsComponent>(target);

        _uiSystem.SendUiMessage(ui, new PenLightUserMessage(GetNetEntity(target),
        blind,
        drunk,
        eyeDamage,
        seeingRainbows
        ));
    }
}
