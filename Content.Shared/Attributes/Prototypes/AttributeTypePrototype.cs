using Robust.Shared.Prototypes;

namespace Content.Shared.Attributes.Prototypes
{
    [Prototype("attributeType")]
    public sealed partial class AttributeTypePrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; private set; } = default!;

        [DataField(required: true)]
        private LocId Name { get; set; }

        [ViewVariables(VVAccess.ReadOnly)]
        public string LocalizedName => Loc.GetString(Name);



    }
}
