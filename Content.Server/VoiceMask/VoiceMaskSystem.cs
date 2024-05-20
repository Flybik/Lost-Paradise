using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared.Clothing;
using Content.Shared.Database;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.VoiceMask;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.VoiceMask;

public sealed partial class VoiceMaskSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VoiceMaskComponent, TransformSpeakerNameEvent>(OnSpeakerNameTransform);
        SubscribeLocalEvent<VoiceMaskComponent, VoiceMaskChangeNameMessage>(OnChangeName);
        SubscribeLocalEvent<VoiceMaskComponent, WearerMaskToggledEvent>(OnMaskToggled);
        SubscribeLocalEvent<VoiceMaskerComponent, GotEquippedEvent>(OnEquip);
        SubscribeLocalEvent<VoiceMaskerComponent, GotUnequippedEvent>(OnUnequip);
        SubscribeLocalEvent<VoiceMaskSetNameEvent>(OnSetName);
        // SubscribeLocalEvent<VoiceMaskerComponent, GetVerbsEvent<AlternativeVerb>>(GetVerbs);
    }

    private void OnSetName(VoiceMaskSetNameEvent ev)
    {
        OpenUI(ev.Performer);
    }

    private void OnChangeName(EntityUid uid, VoiceMaskComponent component, VoiceMaskChangeNameMessage message)
    {
        if (message.Name.Length > HumanoidCharacterProfile.MaxNameLength || message.Name.Length <= 0)
        {
            _popupSystem.PopupEntity(Loc.GetString("voice-mask-popup-failure"), uid, message.Actor, PopupType.SmallCaution);
            return;
        }

        component.VoiceName = message.Name;
        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(message.Actor):player} set voice of {ToPrettyString(uid):mask}: {component.VoiceName}");

        _popupSystem.PopupEntity(Loc.GetString("voice-mask-popup-success"), uid, message.Actor);

        TrySetLastKnownName(uid, message.Name);

        UpdateUI(uid, component);
    }

    private void OnSpeakerNameTransform(EntityUid uid, VoiceMaskComponent component, TransformSpeakerNameEvent args)
    {
        if (component.Enabled)
        {
            /*
            args.Name = _idCard.TryGetIdCard(uid, out var card) && !string.IsNullOrEmpty(card.FullName)
                ? card.FullName
                : Loc.GetString("voice-mask-unknown");
                */

            args.Name = component.VoiceName;
            if (component.SpeechVerb != null)
                args.SpeechVerb = component.SpeechVerb;
        }
    }

    private void OnMaskToggled(Entity<VoiceMaskComponent> ent, ref WearerMaskToggledEvent args)
    {
        ent.Comp.Enabled = !args.IsToggled;
    }

    private void OpenUI(EntityUid player, ActorComponent? actor = null)
    {
        // Delta-V: `logMissing: false` because of syrinx.
        if (!Resolve(player, ref actor, logMissing: false))
            return;
        if (!_uiSystem.HasUi(player, VoiceMaskUIKey.Key))
            return;

        _uiSystem.OpenUi(player, VoiceMaskUIKey.Key, actor.PlayerSession);
        UpdateUI(player);
    }

    private void UpdateUI(EntityUid owner, VoiceMaskComponent? component = null)
    {
        // Delta-V: `logMissing: false` because of syrinx
        if (!Resolve(owner, ref component, logMissing: false))
        {
            return;
        }

        if (_uiSystem.HasUi(owner, VoiceMaskUIKey.Key))
            _uiSystem.SetUiState(owner, VoiceMaskUIKey.Key, new VoiceMaskBuiState(component.VoiceName));
    }
}
