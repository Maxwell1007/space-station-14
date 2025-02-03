using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared.Attributes;

//todo writing
public sealed class AttributesSpecifierDictionarySerializer : ITypeReader<Dictionary<string, FixedPoint2>, MappingDataNode>
{
    private ITypeValidator<Dictionary<string, FixedPoint2>, MappingDataNode> AttributeTypeSerializer = new PrototypeIdDictionarySerializer<FixedPoint2, AttributeTypePrototype>();

    public ValidationNode Validate(ISerializationManager serializationManager, MappingDataNode node,
        IDependencyCollection dependencies, ISerializationContext? context = null)
    {
        var vals = new Dictionary<ValidationNode, ValidationNode>();
        if (node.TryGet<MappingDataNode>("types", out var typesNode))
        {
            vals.Add(new ValidatedValueNode(new ValueDataNode("types")), AttributeTypeSerializer.Validate(serializationManager, typesNode, dependencies, context));
        }

        return new ValidatedMappingNode(vals);
    }

    public Dictionary<string, FixedPoint2> Read(ISerializationManager serializationManager, MappingDataNode node, IDependencyCollection dependencies,
        SerializationHookContext hookCtx, ISerializationContext? context = null, ISerializationManager.InstantiationDelegate<Dictionary<string, FixedPoint2>>? instanceProvider = null)
    {
        var dict = instanceProvider != null ? instanceProvider() : new();
        // Add all the damage types by just copying the type dictionary (if it is not null).
        if (node.TryGet<MappingDataNode>("types", out var typesNode))
        {
            serializationManager.Read(typesNode, instanceProvider: () => dict, notNullableOverride: true);
        }

        return dict;
    }
}

