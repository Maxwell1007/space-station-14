using System.Linq;
using Content.Shared.Attributes.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Attributes
{
    public sealed class AttributeSystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly INetManager _netMan = default!;
        [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;

        private EntityQuery<AttributableComponent> _attributableQuery;
        private EntityQuery<MindContainerComponent> _mindContainerQuery;

        public override void Initialize()
        {
            SubscribeLocalEvent<AttributableComponent, ComponentInit>(AttributableInit);
            SubscribeLocalEvent<AttributableComponent, ComponentHandleState>(AttributableHandleState);
            _attributableQuery = GetEntityQuery<AttributableComponent>();
            _mindContainerQuery = GetEntityQuery<MindContainerComponent>();
        }

        /// <summary>
        ///     Initialize a damageable component
        /// </summary>
        private void AttributableInit(EntityUid uid, AttributableComponent component, ComponentInit _)
        {
            if (component.AttributeContainerID != null &&
                _prototypeManager.TryIndex<AttributeContainerPrototype>(component.AttributeContainerID,
                    out var damageContainerPrototype))
            {
                // Initialize damage dictionary, using the types and groups from the damage
                // container prototype
                foreach (var type in damageContainerPrototype.SupportedATypes)
                {
                    component.Attributes.AttributeDict.TryAdd(type, FixedPoint2.Zero);
                }
            }
            else
            {
                // No DamageContainerPrototype was given. So we will allow the container to support all damage types
                foreach (var type in _prototypeManager.EnumeratePrototypes<AttributeTypePrototype>())
                {
                    component.Attributes.AttributeDict.TryAdd(type.ID, FixedPoint2.Zero);
                }
            }
        }

        /// <summary>
        ///     Directly sets the damage specifier of a damageable component.
        /// </summary>
        /// <remarks>
        ///     Useful for some unfriendly folk. Also ensures that cached values are updated and that a damage changed
        ///     event is raised.
        /// </remarks>
        public void SetAttributes(EntityUid uid, AttributableComponent attributable, AttributesSpecifier attribute)
        {
            attributable.Attributes = attribute;
            AttributeChanged(uid, attributable);
        }

        /// <summary>
        ///     If the damage in a DamageableComponent was changed, this function should be called.
        /// </summary>
        /// <remarks>
        ///     This updates cached damage information, flags the component as dirty, and raises a damage changed event.
        ///     The damage changed event is used by other systems, such as damage thresholds.
        /// </remarks>
        public void AttributeChanged(EntityUid uid, AttributableComponent component, AttributesSpecifier? attributeDelta = null,
            bool interruptsDoAfters = false, EntityUid? origin = null)
        {
            Dirty(uid, component);
            RaiseLocalEvent(uid, new AttributeChangedEvent(component, attributeDelta, interruptsDoAfters, origin));
        }

        /// <summary>
        ///     Applies damage specified via a <see cref="DamageSpecifier"/>.
        /// </summary>
        /// <remarks>
        ///     <see cref="DamageSpecifier"/> is effectively just a dictionary of damage types and damage values. This
        ///     function just applies the container's resistances (unless otherwise specified) and then changes the
        ///     stored damage data. Division of group damage into types is managed by <see cref="DamageSpecifier"/>.
        /// </remarks>
        /// <returns>
        ///     Returns a <see cref="DamageSpecifier"/> with information about the actual damage changes. This will be
        ///     null if the user had no applicable components that can take damage.
        /// </returns>
        public AttributesSpecifier? TryChangeAttribute(EntityUid? uid, AttributesSpecifier Attribute, bool ignoreResistances = false,
            bool interruptsDoAfters = false, AttributableComponent? attributable = null, EntityUid? origin = null)
        {
            if (!uid.HasValue || !_attributableQuery.Resolve(uid.Value, ref attributable, false))
            {
                // TODO BODY SYSTEM pass damage onto body system
                return null;
            }

            if (Attribute.Empty)
            {
                return Attribute;
            }

            var before = new BeforeAttributeChangedEvent(Attribute, origin);
            RaiseLocalEvent(uid.Value, ref before);

            if (before.Cancelled)
                return null;

            // Apply resistances
            if (!ignoreResistances)
            {
                if (attributable.AttributeModifierSetId != null &&
                    _prototypeManager.TryIndex<DamageModifierSetPrototype>(attributable.AttributeModifierSetId, out var attributeModifierSet))
                {
                    // TODO DAMAGE PERFORMANCE
                    // use a local private field instead of creating a new dictionary here..
                    Attribute = AttributesSpecifier.ApplyModifierSet(Attribute, attributeModifierSet);
                }

                var ev = new AttributeModifyEvent(Attribute, origin);
                RaiseLocalEvent(uid.Value, ev);
                Attribute = ev.Attributes;

                if (Attribute.Empty)
                {
                    return Attribute;
                }
            }

            // TODO DAMAGE PERFORMANCE
            // Consider using a local private field instead of creating a new dictionary here.
            // Would need to check that nothing ever tries to cache the delta.
            var delta = new AttributesSpecifier();
            delta.AttributeDict.EnsureCapacity(Attribute.AttributeDict.Count);

            var dict = attributable.Attributes.AttributeDict;
            foreach (var (type, value) in Attribute.AttributeDict)
            {
                // CollectionsMarshal my beloved.
                if (!dict.TryGetValue(type, out var oldValue))
                    continue;

                var newValue = FixedPoint2.Max(FixedPoint2.Zero, oldValue + value);
                if (newValue == oldValue)
                    continue;

                dict[type] = newValue;
                delta.AttributeDict[type] = newValue - oldValue;
            }

            if (delta.AttributeDict.Count > 0)
                AttributeChanged(uid.Value, attributable, delta, interruptsDoAfters, origin);

            return delta;
        }

        /// <summary>
        ///     Sets all damage types supported by a <see cref="DamageableComponent"/> to the specified value.
        /// </summary>
        /// <remakrs>
        ///     Does nothing If the given damage value is negative.
        /// </remakrs>
        public void SetAllAttributes(EntityUid uid, AttributableComponent component, FixedPoint2 newValue)
        {
            if (newValue < 0)
            {
                // invalid value
                return;
            }

            foreach (var type in component.Attributes.AttributeDict.Keys)
            {
                component.Attributes.AttributeDict[type] = newValue;
            }

            // Setting damage does not count as 'dealing' damage, even if it is set to a larger value, so we pass an
            // empty damage delta.
            AttributeChanged(uid, component, new AttributesSpecifier());
        }

        public void SetAttributeModifierSetId(EntityUid uid, string damageModifierSetId, AttributableComponent? comp = null)
        {
            if (!_attributableQuery.Resolve(uid, ref comp))
                return;

            comp.AttributeModifierSetId = damageModifierSetId;
            Dirty(uid, comp);
        }

        private void AttributableHandleState(EntityUid uid, AttributableComponent component, ref ComponentHandleState args)
        {
            if (args.Current is not AttributableComponentState state)
            {
                return;
            }

            component.AttributeModifierSetId = state.AttrModifierSetId;

            // Has the damage actually changed?
            AttributesSpecifier newAttributes = new() { AttributeDict = new(state.AttributesDict) };
            var delta = component.Attributes - newAttributes;
            delta.TrimZeros();

            if (!delta.Empty)
            {
                component.Attributes = newAttributes;
                AttributeChanged(uid, component, delta);
            }
        }
    }

    /// <summary>
    ///     Raised before damage is done, so stuff can cancel it if necessary.
    /// </summary>
    [ByRefEvent]
    public record struct BeforeAttributeChangedEvent(AttributesSpecifier Damage, EntityUid? Origin = null, bool Cancelled = false);

    /// <summary>
    ///     Raised on an entity when damage is about to be dealt,
    ///     in case anything else needs to modify it other than the base
    ///     damageable component.
    ///
    ///     For example, armor.
    /// </summary>
    public sealed class AttributeModifyEvent : EntityEventArgs, IInventoryRelayEvent
    {
        // Whenever locational damage is a thing, this should just check only that bit of armour.
        public SlotFlags TargetSlots { get; } = ~SlotFlags.POCKET;

        public readonly AttributesSpecifier OriginalAttributes;
        public AttributesSpecifier Attributes;
        public EntityUid? Origin;

        public AttributeModifyEvent(AttributesSpecifier attributes, EntityUid? origin = null)
        {
            OriginalAttributes = attributes;
            Attributes = attributes;
            Origin = origin;
        }
    }

    public sealed class AttributeChangedEvent : EntityEventArgs
    {
        /// <summary>
        ///     This is the component whose damage was changed.
        /// </summary>
        /// <remarks>
        ///     Given that nearly every component that cares about a change in the damage, needs to know the
        ///     current damage values, directly passing this information prevents a lot of duplicate
        ///     Owner.TryGetComponent() calls.
        /// </remarks>
        public readonly AttributableComponent Attributable;

        /// <summary>
        ///     The amount by which the damage has changed. If the damage was set directly to some number, this will be
        ///     null.
        /// </summary>
        public readonly AttributesSpecifier? AttributesDelta;

        /// <summary>
        ///     Was any of the damage change dealing damage, or was it all healing?
        /// </summary>
        public readonly bool AttributeIncreased;

        /// <summary>
        ///     Does this event interrupt DoAfters?
        ///     Note: As provided in the constructor, this *does not* account for DamageIncreased.
        ///     As written into the event, this *does* account for DamageIncreased.
        /// </summary>
        public readonly bool InterruptsDoAfters;

        /// <summary>
        ///     Contains the entity which caused the change in damage, if any was responsible.
        /// </summary>
        public readonly EntityUid? Origin;

        public AttributeChangedEvent(AttributableComponent attributable, AttributesSpecifier? attributesDelta, bool interruptsDoAfters, EntityUid? origin)
        {
            Attributable = attributable;
            AttributesDelta = attributesDelta;
            Origin = origin;

            if (AttributesDelta == null)
                return;

            foreach (var damageChange in AttributesDelta.AttributeDict.Values)
            {
                if (damageChange > 0)
                {
                    AttributeIncreased = true;
                    break;
                }
            }
            InterruptsDoAfters = interruptsDoAfters && AttributeIncreased;
        }
    }
}
