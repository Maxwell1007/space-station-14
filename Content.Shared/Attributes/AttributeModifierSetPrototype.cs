using Robust.Shared.Prototypes;

namespace Content.Shared.Attributes.Prototypes
{
    /// <summary>
    ///     A version of DamageModifierSet that can be serialized as a prototype, but is functionally identical.
    /// </summary>
    /// <remarks>
    ///     Done to avoid removing the 'required' tag on the ID and passing around a 'prototype' when we really
    ///     just want normal data to be deserialized.
    /// </remarks>
    [Prototype("AttributeModifierSet")]
    public sealed partial class DamageModifierSetPrototype : AttributeModifierSet, IPrototype
    {
        [ViewVariables]
        [IdDataField]
        public string ID { get; private set; } = default!;
    }
}
