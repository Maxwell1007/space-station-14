using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Shared.Attributes.Prototypes
{
    /// <summary>
    ///     A damage container which can be used to specify support for various damage types.
    /// </summary>
    /// <remarks>
    ///     This is effectively just a list of damage types that can be specified in YAML files using both damage types
    ///     and damage groups. Currently this is only used to specify what damage types a <see
    ///     cref="AttributableComponent"/> should support.
    /// </remarks>
    [Prototype("AttributeContainer")]
    [Serializable, NetSerializable]
    public sealed partial class AttributeContainerPrototype : IPrototype
    {
        [ViewVariables]
        [IdDataField]
        public string ID { get; private set; } = default!;

        [DataField("supportedATypes", customTypeSerializer: typeof(PrototypeIdListSerializer<AttributeTypePrototype>))]
        public List<string> SupportedATypes = new();
    }
}
