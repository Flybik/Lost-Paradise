using Robust.Shared.Serialization;

namespace Content.Shared.Kitchen.Components
{

    [Serializable, NetSerializable]
    public enum KettleVisualState
    {
        Idle,
        Boiling
    }
}
