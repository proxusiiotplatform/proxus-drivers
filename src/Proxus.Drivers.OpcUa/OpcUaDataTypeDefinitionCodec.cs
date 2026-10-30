namespace Proxus.Drivers.OpcUa;

internal static class OpcUaDataTypeDefinitionCodec
{
    public static OpcUaDataTypeDefinition Decode(DriverExtensionObject value)
    {
        if (value.Encoding != DriverExtensionObjectEncoding.Binary)
            throw new NotSupportedException(
                $"OPC UA DataTypeDefinition encoding {value.Encoding} is not supported; binary encoding is required.");
        var typeId = value.TypeId.NodeId;
        if (typeId.NamespaceIndex != 0 || typeId.Kind != IndustrialNodeIdKind.Numeric)
            throw new NotSupportedException($"Unsupported OPC UA DataTypeDefinition TypeId {value.TypeId}.");

        var reader = new UaBinaryReader(value.EncodedBody.Span);
        OpcUaDataTypeDefinition result = typeId.NumericIdentifier switch
        {
            OpcUaProtocol.StructureDefinitionBinary => ReadStructureDefinition(ref reader),
            OpcUaProtocol.EnumDefinitionBinary => ReadEnumDefinition(ref reader),
            _ => throw new NotSupportedException(
                $"Unsupported OPC UA DataTypeDefinition binary TypeId i={typeId.NumericIdentifier}.")
        };
        if (reader.Remaining != 0)
            throw new InvalidDataException($"OPC UA DataTypeDefinition payload has {reader.Remaining} trailing bytes.");
        return result;
    }

    private static OpcUaStructureDataTypeDefinition ReadStructureDefinition(ref UaBinaryReader reader)
    {
        var defaultEncodingId = reader.ReadNodeId();
        var baseDataType = reader.ReadNodeId();
        var structureTypeValue = reader.ReadInt32();
        var structureType = (OpcUaStructureType)structureTypeValue;
        if (structureType is not (OpcUaStructureType.Structure or OpcUaStructureType.StructureWithOptionalFields
            or OpcUaStructureType.Union or OpcUaStructureType.StructureWithSubtypedValues
            or OpcUaStructureType.UnionWithSubtypedValues))
            throw new InvalidDataException($"Unknown OPC UA StructureType {structureTypeValue}.");
        var count = ReadArrayLength(ref reader, "StructureDefinition.Fields", 20);
        var fields = new OpcUaStructureFieldDefinition[count];
        for (var i = 0; i < count; i++)
        {
            var name = reader.ReadString() ?? string.Empty;
            var description = reader.ReadLocalizedText();
            var dataType = reader.ReadNodeId();
            var valueRank = reader.ReadInt32();
            var dimensionsCount = ReadArrayLength(ref reader, "StructureField.ArrayDimensions", 4);
            var dimensions = new uint[dimensionsCount];
            for (var j = 0; j < dimensionsCount; j++) dimensions[j] = reader.ReadUInt32();
            var maxStringLength = reader.ReadUInt32();
            var isOptional = reader.ReadBoolean();
            fields[i] = new OpcUaStructureFieldDefinition(name, description, dataType, valueRank, dimensions,
                maxStringLength, isOptional);
        }

        return new OpcUaStructureDataTypeDefinition(defaultEncodingId, baseDataType, structureType, fields);
    }

    private static OpcUaEnumDataTypeDefinition ReadEnumDefinition(ref UaBinaryReader reader)
    {
        var count = ReadArrayLength(ref reader, "EnumDefinition.Fields", 14);
        var fields = new OpcUaEnumFieldDefinition[count];
        for (var i = 0; i < count; i++)
        {
            var value = reader.ReadInt64();
            var displayName = reader.ReadLocalizedText();
            var description = reader.ReadLocalizedText();
            var name = reader.ReadString() ?? string.Empty;
            fields[i] = new OpcUaEnumFieldDefinition(value, displayName, description, name);
        }

        return new OpcUaEnumDataTypeDefinition(fields);
    }

    private static int ReadArrayLength(ref UaBinaryReader reader, string field, int minimumElementBytes = 1)
    {
        var length = reader.ReadArrayLength(field, minimumElementBytes);
        return length < 0 ? 0 : length;
    }
}
