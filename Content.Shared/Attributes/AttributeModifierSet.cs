﻿using Content.Shared.Attributes.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Shared.Attributes
{
    /// <summary>
    ///     A set of coefficients or flat modifiers to damage types. Can be applied to <see cref="DamageSpecifier"/> using <see
    ///     cref="DamageSpecifier.ApplyModifierSet(DamageSpecifier, DamageModifierSet)"/>. This can be done several times as the
    ///     <see cref="DamageSpecifier"/> is passed to it's final target. By default the receiving <see cref="DamageableComponent"/>, will
    ///     also apply it's own <see cref="DamageModifierSet"/>.
    /// </summary>
    /// <remarks>
    /// The modifier will only ever be applied to damage that is being dealt. Healing is unmodified.
    /// </remarks>
    [DataDefinition]
    [Serializable, NetSerializable]
    [Virtual]
    public partial class AttributeModifierSet
    {
        [DataField("Acoefficients", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<float, AttributeTypePrototype>))]
        public Dictionary<string, float> ACoefficients = new();

        [DataField("AflatReductions", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<float, AttributeTypePrototype>))]
        public Dictionary<string, float> AFlatReduction = new();
    }
}
