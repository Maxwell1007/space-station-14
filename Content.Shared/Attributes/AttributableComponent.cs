using Content.Shared.Attributes.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Attributes
{
    [RegisterComponent]
    [NetworkedComponent]
    [Access(typeof(AttributeSystem), Other = AccessPermissions.ReadExecute)]
    public sealed partial class AttributableComponent : Component
    {

        [DataField("attributeContainer")]
        public ProtoId<AttributeContainerPrototype>? AttributeContainerID;


        [DataField("AttributesModifierSet")]
        public ProtoId<DamageModifierSetPrototype>? AttributeModifierSetId;

        [DataField(readOnly: true)]
        public AttributesSpecifier Attributes = new();
    }

    [Serializable, NetSerializable]
    public sealed class AttributableComponentState : ComponentState
    {
        public readonly Dictionary<string, FixedPoint2> AttributesDict;
        public readonly string? AttrModifierSetId;

        public AttributableComponentState(
            Dictionary<string, FixedPoint2> attributesDict,
            string? attrModifierSetId)
        {
            AttributesDict = attributesDict;
            this.AttrModifierSetId = attrModifierSetId;
        }
    }
}
