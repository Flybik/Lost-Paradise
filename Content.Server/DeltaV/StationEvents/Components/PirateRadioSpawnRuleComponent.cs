/*
* Delta-V - This file is licensed under AGPLv3
* Copyright (c) 2024 Delta-V Contributors
* See AGPLv3.txt for details.
*/

using Content.Server.StationEvents.Events;

namespace Content.Server.StationEvents.Components;

[RegisterComponent, Access(typeof(PirateRadioSpawnRule))]
public sealed partial class PirateRadioSpawnRuleComponent : Component
{
    [DataField("PirateRadioShuttlePath")]
    public string PirateRadioShuttlePath = "Maps/Shuttles/DeltaV/DV-pirateradio.yml";

    [DataField("additionalRule")]
    public EntityUid? AdditionalRule;

    [DataField("debrisCount")]
    public int DebrisCount { get; set; }
}
