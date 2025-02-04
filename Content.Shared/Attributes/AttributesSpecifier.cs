using System.Text.Json.Serialization;
using Content.Shared.Attributes.Prototypes;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

namespace Content.Shared.Attributes
{
    /// <summary>
    ///     This class represents a collection of damage types and damage values.
    /// </summary>
    /// <remarks>
    ///     The actual damage information is stored in <see cref="DamageDict"/>. This class provides
    ///     functions to apply resistance sets and supports basic math operations to modify this dictionary.
    /// </remarks>
    [DataDefinition, Serializable, NetSerializable]

    public sealed partial class AttributesSpecifier : IEquatable<AttributesSpecifier>
    {
        // These exist solely so the wiki works. Please do not touch them or use them.
        [JsonPropertyName("types")]
        [DataField("types", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<FixedPoint2, AttributeTypePrototype>))]
        [UsedImplicitly]
        private Dictionary<string,FixedPoint2>? _attributeTypeDictionary;

        /// <summary>
        ///     Main DamageSpecifier dictionary. Most DamageSpecifier functions exist to somehow modifying this.
        /// </summary>
        [JsonIgnore]
        [ViewVariables(VVAccess.ReadWrite)]
        [IncludeDataField(customTypeSerializer: typeof(AttributesSpecifierDictionarySerializer), readOnly: true)]
        public Dictionary<string, FixedPoint2> AttributeDict { get; set; } = new();

        /// <summary>
        ///     Returns a Strength value
        /// </summary>
        /// <remarks>
        ///     Note that this being zero does not mean this damage has no effect. Healing in one type may cancel damage
        ///     in another. Consider using <see cref="AnyPositive"/> or <see cref="Empty"/> instead.
        /// </remarks>
        public FixedPoint2 GetStrength()
        {
            if (AttributeDict.TryGetValue("Strength", out FixedPoint2 Strength))
                return Strength;
            else
                return FixedPoint2.Zero;
        }

        public FixedPoint2 GetDexterity()
        {
            if (AttributeDict.TryGetValue("Dexterity", out FixedPoint2 Dexterity))
                return Dexterity;
            else
                return FixedPoint2.Zero;
        }

        public FixedPoint2 GetNevpot()
        {
            if (AttributeDict.TryGetValue("Nevpot", out FixedPoint2 Nevpot))
                return Nevpot;
            else
                return FixedPoint2.Zero;
        }

        public FixedPoint2 GetBody()
        {
            if (AttributeDict.TryGetValue("Body", out FixedPoint2 Body))
                return Body;
            else
                return FixedPoint2.Zero;
        }

        public FixedPoint2 GetMastery()
        {
            if (AttributeDict.TryGetValue("Mastery", out FixedPoint2 Mastery))
                return Mastery;
            else
                return FixedPoint2.Zero;
        }

        public FixedPoint2 GetWisdom()
        {
            if (AttributeDict.TryGetValue("Wisdom", out FixedPoint2 Wisdom))
                return Wisdom;
            else
                return FixedPoint2.Zero;
        }
        public FixedPoint2 GetTotal()
        {
            var total = FixedPoint2.Zero;
            foreach (var value in AttributeDict.Values)
            {
                total += value;
            }
            return total;
        }

        /// <summary>
        /// Returns true if the specifier contains any positive damage values.
        /// Differs from <see cref="Empty"/> as a damage specifier might contain entries with zeroes.
        /// This also returns false if the specifier only contains negative values.
        /// </summary>
        public bool AnyPositive()
        {
            foreach (var value in AttributeDict.Values)
            {
                if (value > FixedPoint2.Zero)
                    return true;
            }

            return false;
        }

        /// <summary>
        ///     Whether this damage specifier has any entries.
        /// </summary>
        [JsonIgnore]
        public bool Empty => AttributeDict.Count == 0;

        #region constructors
        /// <summary>
        ///     Constructor that just results in an empty dictionary.
        /// </summary>
        public AttributesSpecifier() { }

        /// <summary>
        ///     Constructor that takes another DamageSpecifier instance and copies it.
        /// </summary>
        public AttributesSpecifier(AttributesSpecifier attributeSpec)
        {
            AttributeDict = new(attributeSpec.AttributeDict);
        }

        /// <summary>
        ///     Constructor that takes a single damage type prototype and a damage value.
        /// </summary>
        public AttributesSpecifier(AttributeTypePrototype type, FixedPoint2 value)
        {
            AttributeDict = new() { { type.ID, value } };
        }

        #endregion constructors

        /// <summary>
        ///     Reduce (or increase) damages by applying a damage modifier set.
        /// </summary>
        /// <remarks>
        ///     Only applies resistance to a damage type if it is dealing damage, not healing.
        ///     This will never convert damage into healing.
        /// </remarks>
        public static AttributesSpecifier ApplyModifierSet(AttributesSpecifier attributeSpec, AttributeModifierSet attributemodifierSet)
        {
            // Make a copy of the given data. Don't modify the one passed to this function. I did this before, and weapons became
            // duller as you hit walls. Neat, but not FixedPoint2ended. And confusing, when you realize your fists don't work no
            // more cause they're just bloody stumps.
            AttributesSpecifier newAttribute = new();
            newAttribute.AttributeDict.EnsureCapacity(attributeSpec.AttributeDict.Count);

            foreach (var (key, value) in attributeSpec.AttributeDict)
            {
                if (value == 0)
                    continue;

                if (value < 0)
                {
                    newAttribute.AttributeDict[key] = value;
                    continue;
                }

                float newValue = value.Float();

                if (attributemodifierSet.AFlatReduction.TryGetValue(key, out var Areduction))
                    newValue = Math.Max(0f, newValue - Areduction); // flat reductions can't heal you

                if (attributemodifierSet.ACoefficients.TryGetValue(key, out var Acoefficient))
                    newValue *= Acoefficient; // coefficients can heal you, e.g. cauterizing bleeding

                if(newValue != 0)
                    newAttribute.AttributeDict[key] = FixedPoint2.New(newValue);
            }

            return newAttribute;
        }

        /// <summary>
        ///     Reduce (or increase) damages by applying multiple modifier sets.
        /// </summary>
        /// <param name="attributeSpec"></param>
        /// <param name="modifierSets"></param>
        /// <returns></returns>
        public static AttributesSpecifier ApplyModifierSets(AttributesSpecifier attributeSpec, IEnumerable<AttributeModifierSet> modifierSets)
        {
            bool any = false;
            AttributesSpecifier newAttribute = attributeSpec;
            foreach (var set in modifierSets)
            {
                // This creates a new damageSpec for each modifier when we really onlt need to create one.
                // This is quite inefficient, but hopefully this shouldn't ever be called frequently.
                newAttribute = ApplyModifierSet(newAttribute, set);
                any = true;
            }

            if (!any)
                newAttribute = new AttributesSpecifier(attributeSpec);

            return newAttribute;
        }

        /// <summary>
        ///     Remove any damage entries with zero damage.
        /// </summary>
        public void TrimZeros()
        {
            foreach (var (key, value) in AttributeDict)
            {
                if (value == 0)
                {
                    AttributeDict.Remove(key);
                }
            }
        }

        /// <summary>
        ///     Clamps each damage value to be within the given range.
        /// </summary>
        public void Clamp(FixedPoint2 minValue, FixedPoint2 maxValue)
        {
            DebugTools.Assert(minValue < maxValue);
            ClampMax(maxValue);
            ClampMin(minValue);
        }

        /// <summary>
        ///     Sets all damage values to be at least as large as the given number.
        /// </summary>
        /// <remarks>
        ///     Note that this only acts on damage types present in the dictionary. It will not add new damage types.
        /// </remarks>
        public void ClampMin(FixedPoint2 minValue)
        {
            foreach (var (key, value) in AttributeDict)
            {
                if (value < minValue)
                {
                    AttributeDict[key] = minValue;
                }
            }
        }

        /// <summary>
        ///     Sets all damage values to be at most some number. Note that if a damage type is not present in the
        ///     dictionary, these will not be added.
        /// </summary>
        public void ClampMax(FixedPoint2 maxValue)
        {
            foreach (var (key, value) in AttributeDict)
            {
                if (value > maxValue)
                {
                    AttributeDict[key] = maxValue;
                }
            }
        }

        /// <summary>
        ///     This adds the damage values of some other <see cref="DamageSpecifier"/> to the current one without
        ///     adding any new damage types.
        /// </summary>
        /// <remarks>
        ///     This is used for <see cref="AttributableComponent"/>s, such that only "supported" damage types are
        ///     actually added to the component. In most other instances, you can just use the addition operator.
        /// </remarks>
        public void ExclusiveAdd(AttributesSpecifier other)
        {
            foreach (var (type, value) in other.AttributeDict)
            {
                // CollectionsMarshal my beloved.
                if (AttributeDict.TryGetValue(type, out var existing))
                {
                    AttributeDict[type] = existing + value;
                }
            }
        }



        /// <summary>
        ///     Returns a dictionary using <see cref="DamageGroupPrototype.ID"/> keys, with values calculated by adding
        ///     up the values for each damage type in that group
        /// </summary>
        /// <remarks>
        ///     If a damage type is associated with more than one supported damage group, it will contribute to the
        ///     total of each group. If no members of a group are present in this <see cref="DamageSpecifier"/>, the
        ///     group is not included in the resulting dictionary.
        /// </remarks>

        #region Operators
        public static AttributesSpecifier operator *(AttributesSpecifier attributeSpec, FixedPoint2 factor)
        {
            AttributesSpecifier newAttribute = new();
            foreach (var entry in attributeSpec.AttributeDict)
            {
                newAttribute.AttributeDict.Add(entry.Key, entry.Value * factor);
            }
            return newAttribute;
        }

        public static AttributesSpecifier operator *(AttributesSpecifier attributeSpec, float factor)
        {
            AttributesSpecifier newAttribute = new();
            foreach (var entry in attributeSpec.AttributeDict)
            {
                newAttribute.AttributeDict.Add(entry.Key, entry.Value * factor);
            }
            return newAttribute;
        }

        public static AttributesSpecifier operator /(AttributesSpecifier AttributeSpec, FixedPoint2 factor)
        {
            AttributesSpecifier newDamage = new();
            foreach (var entry in AttributeSpec.AttributeDict)
            {
                newDamage.AttributeDict.Add(entry.Key, entry.Value / factor);
            }
            return newDamage;
        }

        public static AttributesSpecifier operator /(AttributesSpecifier AttributeSpec, float factor)
        {
            AttributesSpecifier newDamage = new();

            foreach (var entry in AttributeSpec.AttributeDict)
            {
                newDamage.AttributeDict.Add(entry.Key, entry.Value / factor);
            }
            return newDamage;
        }

        public static AttributesSpecifier operator +(AttributesSpecifier damageSpecA, AttributesSpecifier damageSpecB)
        {
            // Copy existing dictionary from dataA
            AttributesSpecifier newDamage = new(damageSpecA);

            // Then just add types in B
            foreach (var entry in damageSpecB.AttributeDict)
            {
                if (!newDamage.AttributeDict.TryAdd(entry.Key, entry.Value))
                {
                    // Key already exists, add values
                    newDamage.AttributeDict[entry.Key] += entry.Value;
                }
            }
            return newDamage;
        }

        // Here we define the subtraction operator explicitly, rather than implicitly via something like X + (-1 * Y).
        // This is faster because FixedPoint2 multiplication is somewhat involved.
        public static AttributesSpecifier operator -(AttributesSpecifier damageSpecA, AttributesSpecifier damageSpecB)
        {
            AttributesSpecifier newDamage = new(damageSpecA);

            foreach (var entry in damageSpecB.AttributeDict)
            {
                if (!newDamage.AttributeDict.TryAdd(entry.Key, -entry.Value))
                {
                    newDamage.AttributeDict[entry.Key] -= entry.Value;
                }
            }
            return newDamage;
        }

        public static AttributesSpecifier operator +(AttributesSpecifier damageSpec) => damageSpec;

        public static AttributesSpecifier operator -(AttributesSpecifier damageSpec) => damageSpec * -1;

        public static AttributesSpecifier operator *(float factor, AttributesSpecifier damageSpec) => damageSpec * factor;

        public static AttributesSpecifier operator *(FixedPoint2 factor, AttributesSpecifier damageSpec) => damageSpec * factor;

        public bool Equals(AttributesSpecifier? other)
        {
            if (other == null || AttributeDict.Count != other.AttributeDict.Count)
                return false;

            foreach (var (key, value) in AttributeDict)
            {
                if (!other.AttributeDict.TryGetValue(key, out var otherValue) || value != otherValue)
                    return false;
            }

            return true;
        }

        public FixedPoint2 this[string key] => AttributeDict[key];
    }
    #endregion
}
