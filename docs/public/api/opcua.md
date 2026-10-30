---
id: public-api-opcua
title: Proxus.Drivers.OpcUa
audience: developer
status: generated
since: 0.1.0
last_verified: 2026-08-13
---

# Proxus.Drivers.OpcUa

Public API for `Proxus.Drivers.OpcUa`. This page is generated from the compiled assembly.

Use the [OPC UA recipes](../examples/opc-ua.md) for complete workflows, security guidance, and warnings.

## OpcUaApplicationDescription

```csharp
public sealed class OpcUaApplicationDescription
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Application Description in the public driver contract.

### Members

#### `OpcUaApplicationDescription`

```csharp
public OpcUaApplicationDescription(string ApplicationUri, string ProductUri, IndustrialLocalizedText ApplicationName, OpcUaApplicationType ApplicationType, string GatewayServerUri, string DiscoveryProfileUri, string[] DiscoveryUrls)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaApplicationDescription` instance from the supplied values.

#### `ApplicationName`

```csharp
public IndustrialLocalizedText ApplicationName { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets application Name.

#### `ApplicationType`

```csharp
public OpcUaApplicationType ApplicationType { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets application Type.

#### `ApplicationUri`

```csharp
public string ApplicationUri { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets application Uri.

#### `DiscoveryProfileUri`

```csharp
public string DiscoveryProfileUri { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets discovery Profile Uri.

#### `DiscoveryUrls`

```csharp
public string[] DiscoveryUrls { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets discovery Urls.

#### `GatewayServerUri`

```csharp
public string GatewayServerUri { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets gateway Server Uri.

#### `ProductUri`

```csharp
public string ProductUri { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets product Uri.

## OpcUaApplicationIdentity

```csharp
public sealed class OpcUaApplicationIdentity
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Application Identity in the public driver contract.

### Members

#### `OpcUaApplicationIdentity`

```csharp
public OpcUaApplicationIdentity(X509Certificate2 Certificate)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaApplicationIdentity` instance from the supplied values.

#### `Certificate`

```csharp
public X509Certificate2 Certificate { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets certificate.

## OpcUaApplicationType

```csharp
public enum OpcUaApplicationType
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported opc Ua Application Type choices. Select only values documented by the target protocol or device profile.

### Members

#### `Server`

```csharp
Server = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the server option.

#### `Client`

```csharp
Client = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the client option.

#### `ClientAndServer`

```csharp
ClientAndServer = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the client And Server option.

#### `DiscoveryServer`

```csharp
DiscoveryServer = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the discovery Server option.

## OpcUaAttributeId

```csharp
public enum OpcUaAttributeId
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported opc Ua Attribute Id choices. Select only values documented by the target protocol or device profile.

### Members

#### `NodeId`

```csharp
NodeId = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the node Id option.

#### `NodeClass`

```csharp
NodeClass = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the node Class option.

#### `BrowseName`

```csharp
BrowseName = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the browse Name option.

#### `DisplayName`

```csharp
DisplayName = 4
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the display Name option.

#### `Description`

```csharp
Description = 5
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the description option.

#### `WriteMask`

```csharp
WriteMask = 6
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the write Mask option.

#### `UserWriteMask`

```csharp
UserWriteMask = 7
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the user Write Mask option.

#### `IsAbstract`

```csharp
IsAbstract = 8
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the is Abstract option.

#### `Symmetric`

```csharp
Symmetric = 9
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the symmetric option.

#### `InverseName`

```csharp
InverseName = 10
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the inverse Name option.

#### `ContainsNoLoops`

```csharp
ContainsNoLoops = 11
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the contains No Loops option.

#### `EventNotifier`

```csharp
EventNotifier = 12
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the event Notifier option.

#### `Value`

```csharp
Value = 13
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the value option.

#### `DataType`

```csharp
DataType = 14
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the data Type option.

#### `ValueRank`

```csharp
ValueRank = 15
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the value Rank option.

#### `ArrayDimensions`

```csharp
ArrayDimensions = 16
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the array Dimensions option.

#### `AccessLevel`

```csharp
AccessLevel = 17
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the access Level option.

#### `UserAccessLevel`

```csharp
UserAccessLevel = 18
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the user Access Level option.

#### `MinimumSamplingInterval`

```csharp
MinimumSamplingInterval = 19
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the minimum Sampling Interval option.

#### `Historizing`

```csharp
Historizing = 20
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the historizing option.

#### `Executable`

```csharp
Executable = 21
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the executable option.

#### `UserExecutable`

```csharp
UserExecutable = 22
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the user Executable option.

#### `DataTypeDefinition`

```csharp
DataTypeDefinition = 23
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the data Type Definition option.

#### `RolePermissions`

```csharp
RolePermissions = 24
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the role Permissions option.

#### `UserRolePermissions`

```csharp
UserRolePermissions = 25
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the user Role Permissions option.

#### `AccessRestrictions`

```csharp
AccessRestrictions = 26
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the access Restrictions option.

#### `AccessLevelEx`

```csharp
AccessLevelEx = 27
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the access Level Ex option.

## OpcUaBrowseDescription

```csharp
public struct OpcUaBrowseDescription
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Browse Description in the public driver contract.

### Members

#### `OpcUaBrowseDescription`

```csharp
public OpcUaBrowseDescription(IndustrialNodeId NodeId, OpcUaBrowseDirection Direction = Forward, IndustrialNodeId ReferenceTypeId = null, bool IncludeSubtypes = true, OpcUaNodeClass NodeClassMask = Unspecified, OpcUaBrowseResultMask ResultMask = All)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaBrowseDescription` instance from the supplied values.

#### `Direction`

```csharp
public OpcUaBrowseDirection Direction { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets direction.

#### `IncludeSubtypes`

```csharp
public bool IncludeSubtypes { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets include Subtypes.

#### `NodeClassMask`

```csharp
public OpcUaNodeClass NodeClassMask { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets node Class Mask.

#### `NodeId`

```csharp
public IndustrialNodeId NodeId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets node Id.

#### `ReferenceTypeId`

```csharp
public IndustrialNodeId ReferenceTypeId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets reference Type Id.

#### `ResultMask`

```csharp
public OpcUaBrowseResultMask ResultMask { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets result Mask.

## OpcUaBrowseDirection

```csharp
public enum OpcUaBrowseDirection
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported opc Ua Browse Direction choices. Select only values documented by the target protocol or device profile.

### Members

#### `Forward`

```csharp
Forward = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the forward option.

#### `Inverse`

```csharp
Inverse = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the inverse option.

#### `Both`

```csharp
Both = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the both option.

## OpcUaBrowsePath

```csharp
public sealed class OpcUaBrowsePath
```

A starting node and the RelativePath to resolve from it with TranslateBrowsePathsToNodeIds.

### Members

#### `OpcUaBrowsePath`

```csharp
public OpcUaBrowsePath(IndustrialNodeId startingNode, OpcUaRelativePathElement[] elements)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaBrowsePath` instance from the supplied values.

#### `FromBrowseNames`

```csharp
public static OpcUaBrowsePath FromBrowseNames(IndustrialNodeId startingNode, IndustrialQualifiedName[] browseNames)
```

Builds a path that follows hierarchical references through the given BrowseNames, for example `OpcUaBrowsePath.FromBrowseNames(objectsFolder, new(0, "Server"), new(0, "ServerStatus"))`.

#### `Elements`

```csharp
public IReadOnlyList<OpcUaRelativePathElement> Elements { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets elements.

#### `StartingNode`

```csharp
public IndustrialNodeId StartingNode { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets starting Node.

## OpcUaBrowsePathResult

```csharp
public sealed class OpcUaBrowsePathResult
```

Per-path TranslateBrowsePathsToNodeIds result. A Bad status (for example Bad_NoMatch) has no targets.

### Members

#### `OpcUaBrowsePathResult`

```csharp
public OpcUaBrowsePathResult(uint statusCode, OpcUaBrowsePathTarget[] targets)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaBrowsePathResult` instance from the supplied values.

#### `Quality`

```csharp
public DriverQuality Quality { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets quality.

#### `StatusCode`

```csharp
public uint StatusCode { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets status Code.

#### `Targets`

```csharp
public IReadOnlyList<OpcUaBrowsePathTarget> Targets { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets targets.

## OpcUaBrowsePathTarget

```csharp
public struct OpcUaBrowsePathTarget
```

A node matched by a browse path. `OpcUaBrowsePathTarget.RemainingPathIndex` is `UInt32.MaxValue` when the whole path was resolved; otherwise the target is in another server and resolution stopped at that element.

### Members

#### `OpcUaBrowsePathTarget`

```csharp
public OpcUaBrowsePathTarget(IndustrialExpandedNodeId TargetId, uint RemainingPathIndex)
```

A node matched by a browse path. `OpcUaBrowsePathTarget.RemainingPathIndex` is `UInt32.MaxValue` when the whole path was resolved; otherwise the target is in another server and resolution stopped at that element.

#### `IsComplete`

```csharp
public bool IsComplete { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets is Complete.

#### `RemainingPathIndex`

```csharp
public uint RemainingPathIndex { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets remaining Path Index.

#### `TargetId`

```csharp
public IndustrialExpandedNodeId TargetId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets target Id.

## OpcUaBrowseResult

```csharp
public sealed class OpcUaBrowseResult
```

*Undocumented: no XML summary; this text is generated from the name.* Carries the structured result of opc Ua Browse, including protocol status where applicable.

### Members

#### `OpcUaBrowseResult`

```csharp
public OpcUaBrowseResult(uint statusCode, byte[] continuationPoint, OpcUaReferenceDescription[] references)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaBrowseResult` instance from the supplied values.

#### `ContinuationPoint`

```csharp
public byte[] ContinuationPoint { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets continuation Point.

#### `HasContinuationPoint`

```csharp
public bool HasContinuationPoint { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets has Continuation Point.

#### `Quality`

```csharp
public DriverQuality Quality { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets quality.

#### `References`

```csharp
public IReadOnlyList<OpcUaReferenceDescription> References { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets references.

#### `StatusCode`

```csharp
public uint StatusCode { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets status Code.

## OpcUaBrowseResultMask

```csharp
public enum OpcUaBrowseResultMask
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported opc Ua Browse Result Mask choices. Select only values documented by the target protocol or device profile.

### Members

#### `None`

```csharp
None = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the none option.

#### `ReferenceType`

```csharp
ReferenceType = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the reference Type option.

#### `IsForward`

```csharp
IsForward = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the is Forward option.

#### `NodeClass`

```csharp
NodeClass = 4
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the node Class option.

#### `BrowseName`

```csharp
BrowseName = 8
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the browse Name option.

#### `DisplayName`

```csharp
DisplayName = 16
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the display Name option.

#### `TypeDefinition`

```csharp
TypeDefinition = 32
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the type Definition option.

#### `All`

```csharp
All = 63
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the all option.

## OpcUaCertificateTrustOptions

```csharp
public sealed class OpcUaCertificateTrustOptions
```

*Undocumented: no XML summary; this text is generated from the name.* Configures opc Ua Certificate Trust. Create and validate it once for each endpoint.

### Members

#### `OpcUaCertificateTrustOptions`

```csharp
public OpcUaCertificateTrustOptions()
```

Creates an instance with default member values.

#### `ExpectedApplicationUri`

```csharp
public string ExpectedApplicationUri { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets expected Application Uri.

#### `RequireHostName`

```csharp
public bool RequireHostName { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets require Host Name.

#### `RevocationMode`

```csharp
public X509RevocationMode RevocationMode { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets revocation Mode.

#### `TrustedPeerCertificates`

```csharp
public X509Certificate2[] TrustedPeerCertificates { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets trusted Peer Certificates.

#### `TrustedRootCertificates`

```csharp
public X509Certificate2[] TrustedRootCertificates { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets trusted Root Certificates.

## OpcUaConnectionLimits

```csharp
public struct OpcUaConnectionLimits
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Connection Limits in the public driver contract.

### Members

#### `OpcUaConnectionLimits`

```csharp
public OpcUaConnectionLimits(uint ReceiveBufferSize, uint SendBufferSize, uint MaxMessageSize, uint MaxChunkCount)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaConnectionLimits` instance from the supplied values.

#### `MaxChunkCount`

```csharp
public uint MaxChunkCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets max Chunk Count.

#### `MaxMessageSize`

```csharp
public uint MaxMessageSize { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets max Message Size.

#### `ReceiveBufferSize`

```csharp
public uint ReceiveBufferSize { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets receive Buffer Size.

#### `SendBufferSize`

```csharp
public uint SendBufferSize { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets send Buffer Size.

## OpcUaDataTypeDefinition

```csharp
public abstract class OpcUaDataTypeDefinition
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Data Type Definition in the public driver contract.

This type has no separately declared public members.

## OpcUaDiscoveredDataType

```csharp
public struct OpcUaDiscoveredDataType
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Discovered Data Type in the public driver contract.

### Members

#### `OpcUaDiscoveredDataType`

```csharp
public OpcUaDiscoveredDataType(IndustrialNodeId NodeId, IndustrialQualifiedName BrowseName, OpcUaDataTypeDefinition Definition)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaDiscoveredDataType` instance from the supplied values.

#### `BrowseName`

```csharp
public IndustrialQualifiedName BrowseName { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets browse Name.

#### `Definition`

```csharp
public OpcUaDataTypeDefinition Definition { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets definition.

#### `NodeId`

```csharp
public IndustrialNodeId NodeId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets node Id.

## OpcUaEndpointDescription

```csharp
public sealed class OpcUaEndpointDescription
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Endpoint Description in the public driver contract.

### Members

#### `OpcUaEndpointDescription`

```csharp
public OpcUaEndpointDescription(string EndpointUrl, OpcUaApplicationDescription Server, byte[] ServerCertificate, OpcUaMessageSecurityMode SecurityMode, string SecurityPolicyUri, OpcUaUserTokenPolicy[] UserIdentityTokens, string TransportProfileUri, byte SecurityLevel)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaEndpointDescription` instance from the supplied values.

#### `Supports`

```csharp
public bool Supports(OpcUaMessageSecurityMode mode, string securityPolicyUri)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs supports.

#### `EndpointUrl`

```csharp
public string EndpointUrl { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets endpoint Url.

#### `SecurityLevel`

```csharp
public byte SecurityLevel { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets security Level.

#### `SecurityMode`

```csharp
public OpcUaMessageSecurityMode SecurityMode { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets security Mode.

#### `SecurityPolicyUri`

```csharp
public string SecurityPolicyUri { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets security Policy Uri.

#### `Server`

```csharp
public OpcUaApplicationDescription Server { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets server.

#### `ServerCertificate`

```csharp
public byte[] ServerCertificate { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets server Certificate.

#### `TransportProfileUri`

```csharp
public string TransportProfileUri { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets transport Profile Uri.

#### `UserIdentityTokens`

```csharp
public OpcUaUserTokenPolicy[] UserIdentityTokens { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets user Identity Tokens.

## OpcUaEndpointSelector

```csharp
public static class OpcUaEndpointSelector
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Endpoint Selector in the public driver contract.

### Members

#### `Select`

```csharp
public static DriverResult<OpcUaEndpointDescription> Select(IReadOnlyList<OpcUaEndpointDescription> endpoints, OpcUaMessageSecurityMode mode, string securityPolicyUri, OpcUaUserTokenType userTokenType = Anonymous)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs select.

## OpcUaEnumDataTypeDefinition

```csharp
public sealed class OpcUaEnumDataTypeDefinition
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Enum Data Type Definition in the public driver contract.

### Members

#### `OpcUaEnumDataTypeDefinition`

```csharp
public OpcUaEnumDataTypeDefinition(OpcUaEnumFieldDefinition[] Fields)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaEnumDataTypeDefinition` instance from the supplied values.

#### `Fields`

```csharp
public OpcUaEnumFieldDefinition[] Fields { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets fields.

## OpcUaEnumFieldDefinition

```csharp
public struct OpcUaEnumFieldDefinition
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Enum Field Definition in the public driver contract.

### Members

#### `OpcUaEnumFieldDefinition`

```csharp
public OpcUaEnumFieldDefinition(long Value, IndustrialLocalizedText DisplayName, IndustrialLocalizedText Description, string Name)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaEnumFieldDefinition` instance from the supplied values.

#### `Description`

```csharp
public IndustrialLocalizedText Description { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets description.

#### `DisplayName`

```csharp
public IndustrialLocalizedText DisplayName { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets display Name.

#### `Name`

```csharp
public string Name { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets name.

#### `Value`

```csharp
public long Value { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets value.

## OpcUaEventField

```csharp
public sealed class OpcUaEventField
```

One event field to return (a SimpleAttributeOperand select clause, OPC 10000-4 §7.4.4.5): the BrowseName path from the event type to the property, read with `OpcUaEventField.AttributeId`.

### Members

#### `OpcUaEventField`

```csharp
public OpcUaEventField(IndustrialNodeId typeDefinitionId, OpcUaAttributeId attributeId, IndustrialQualifiedName[] browsePath)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaEventField` instance from the supplied values.

#### `Property`

```csharp
public static OpcUaEventField Property(string[] browseNames)
```

A BaseEventType property path in namespace 0, for example `Property("Severity")`.

#### `AttributeId`

```csharp
public OpcUaAttributeId AttributeId { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets attribute Id.

#### `BrowsePath`

```csharp
public IReadOnlyList<IndustrialQualifiedName> BrowsePath { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets browse Path.

#### `ConditionId`

```csharp
public static OpcUaEventField ConditionId { get;  }
```

The ConditionId of a condition event (the NodeId of the ConditionType instance, OPC 10000-9 §5.5.2). Select it to acknowledge alarms; it is null for events that are not conditions.

#### `TypeDefinitionId`

```csharp
public IndustrialNodeId TypeDefinitionId { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets type Definition Id.

## OpcUaEventFilter

```csharp
public sealed class OpcUaEventFilter
```

EventFilter for an event monitored item (OPC 10000-4 §7.22.3): the fields each notification carries, optionally restricted to some event types (OfType, including subtypes) and a minimum Severity.

### Members

#### `OpcUaEventFilter`

```csharp
public OpcUaEventFilter(OpcUaEventField[] selectClauses)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaEventFilter` instance from the supplied values.

#### `BaseEventFields`

```csharp
public static OpcUaEventFilter BaseEventFields()
```

EventId, EventType, SourceNode, SourceName, Time, ReceiveTime, Message and Severity, in that order (indexes 0 to 7 of `OpcUaEventNotification.Fields`).

#### `EventTypes`

```csharp
public IReadOnlyList<IndustrialNodeId> EventTypes { get; init;  }
```

Only events of these types or their subtypes; empty for every event type.

#### `MinimumSeverity`

```csharp
public ushort? MinimumSeverity { get; init;  }
```

Only events whose Severity is at least this value (1 to 1000).

#### `SelectClauses`

```csharp
public IReadOnlyList<OpcUaEventField> SelectClauses { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets select Clauses.

## OpcUaEventMonitoredItemRequest

```csharp
public sealed class OpcUaEventMonitoredItemRequest
```

An event monitored item on an object whose EventNotifier attribute subscribes to events.

### Members

#### `OpcUaEventMonitoredItemRequest`

```csharp
public OpcUaEventMonitoredItemRequest(IndustrialNodeId notifier, uint clientHandle, OpcUaEventFilter filter)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaEventMonitoredItemRequest` instance from the supplied values.

#### `ClientHandle`

```csharp
public uint ClientHandle { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets client Handle.

#### `DiscardOldest`

```csharp
public bool DiscardOldest { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets discard Oldest.

#### `Filter`

```csharp
public OpcUaEventFilter Filter { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets filter.

#### `MonitoringMode`

```csharp
public OpcUaMonitoringMode MonitoringMode { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets monitoring Mode.

#### `Notifier`

```csharp
public IndustrialNodeId Notifier { get;  }
```

The event notifier, usually the Server object (i=2253) or an area or source object.

#### `QueueSize`

```csharp
public uint QueueSize { get; init;  }
```

Events the server queues between Publish responses; events are not sampled.

## OpcUaEventMonitoredItemResult

```csharp
public sealed class OpcUaEventMonitoredItemResult
```

CreateMonitoredItems result for an event item. `OpcUaEventMonitoredItemResult.SelectClauseResults` holds one StatusCode per select clause when the server reports problems (for example Bad_BrowseNameInvalid); it is empty when every clause was accepted.

### Members

#### `OpcUaEventMonitoredItemResult`

```csharp
public OpcUaEventMonitoredItemResult(uint statusCode, uint monitoredItemId, uint revisedQueueSize, uint[] selectClauseResults)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaEventMonitoredItemResult` instance from the supplied values.

#### `IsGood`

```csharp
public bool IsGood { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets is Good.

#### `MonitoredItemId`

```csharp
public uint MonitoredItemId { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets monitored Item Id.

#### `RevisedQueueSize`

```csharp
public uint RevisedQueueSize { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets revised Queue Size.

#### `SelectClauseResults`

```csharp
public IReadOnlyList<uint> SelectClauseResults { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets select Clause Results.

#### `StatusCode`

```csharp
public uint StatusCode { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets status Code.

## OpcUaEventNotification

```csharp
public sealed class OpcUaEventNotification
```

One event: the monitored item's client handle and the selected fields in select-clause order.

### Members

#### `OpcUaEventNotification`

```csharp
public OpcUaEventNotification(uint clientHandle, DriverValue[] fields)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaEventNotification` instance from the supplied values.

#### `ClientHandle`

```csharp
public uint ClientHandle { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets client Handle.

#### `Fields`

```csharp
public IReadOnlyList<DriverValue> Fields { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets fields.

## OpcUaHistoryReadNode

```csharp
public struct OpcUaHistoryReadNode
```

A node to read history for, with the continuation point of a previous partial read, if any.

### Members

#### `OpcUaHistoryReadNode`

```csharp
public OpcUaHistoryReadNode(IndustrialNodeId NodeId, ReadOnlyMemory<byte> ContinuationPoint = default, string IndexRange = null)
```

A node to read history for, with the continuation point of a previous partial read, if any.

#### `ContinuationPoint`

```csharp
public ReadOnlyMemory<byte> ContinuationPoint { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets continuation Point.

#### `IndexRange`

```csharp
public string IndexRange { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets index Range.

#### `NodeId`

```csharp
public IndustrialNodeId NodeId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets node Id.

## OpcUaHistoryReadRawRequest

```csharp
public struct OpcUaHistoryReadRawRequest
```

ReadRawModifiedDetails (OPC 10000-11 §6.4.3). Leave `OpcUaHistoryReadRawRequest.StartTime` or `OpcUaHistoryReadRawRequest.EndTime` unset (`DateTime.MinValue`) for an open interval; at least one must be set, and `OpcUaHistoryReadRawRequest.NumValuesPerNode` must be non-zero when one is unset.

### Members

#### `OpcUaHistoryReadRawRequest`

```csharp
public OpcUaHistoryReadRawRequest(DateTime StartTime, DateTime EndTime, uint NumValuesPerNode = 0, bool ReturnBounds = false, bool IsReadModified = false, OpcUaTimestampsToReturn TimestampsToReturn = Both)
```

ReadRawModifiedDetails (OPC 10000-11 §6.4.3). Leave `OpcUaHistoryReadRawRequest.StartTime` or `OpcUaHistoryReadRawRequest.EndTime` unset (`DateTime.MinValue`) for an open interval; at least one must be set, and `OpcUaHistoryReadRawRequest.NumValuesPerNode` must be non-zero when one is unset.

#### `EndTime`

```csharp
public DateTime EndTime { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets end Time.

#### `IsReadModified`

```csharp
public bool IsReadModified { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets is Read Modified.

#### `NumValuesPerNode`

```csharp
public uint NumValuesPerNode { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets num Values Per Node.

#### `ReturnBounds`

```csharp
public bool ReturnBounds { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets return Bounds.

#### `StartTime`

```csharp
public DateTime StartTime { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets start Time.

#### `TimestampsToReturn`

```csharp
public OpcUaTimestampsToReturn TimestampsToReturn { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets timestamps To Return.

## OpcUaHistoryReadResult

```csharp
public sealed class OpcUaHistoryReadResult
```

Per-node HistoryRead result. A non-empty `OpcUaHistoryReadResult.ContinuationPoint` means more values exist; pass it back in `OpcUaHistoryReadNode.ContinuationPoint`, or release it with `OpcUaTcpClient.ReleaseHistoryContinuationPointsAsync`.

### Members

#### `OpcUaHistoryReadResult`

```csharp
public OpcUaHistoryReadResult(uint statusCode, byte[] continuationPoint, DriverSample[] values)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaHistoryReadResult` instance from the supplied values.

#### `ContinuationPoint`

```csharp
public byte[] ContinuationPoint { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets continuation Point.

#### `HasContinuationPoint`

```csharp
public bool HasContinuationPoint { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets has Continuation Point.

#### `Quality`

```csharp
public DriverQuality Quality { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets quality.

#### `StatusCode`

```csharp
public uint StatusCode { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets status Code.

#### `Values`

```csharp
public IReadOnlyList<DriverSample> Values { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets values.

## OpcUaManagedSubscriptionPlan

```csharp
public sealed class OpcUaManagedSubscriptionPlan
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Managed Subscription Plan in the public driver contract.

### Members

#### `OpcUaManagedSubscriptionPlan`

```csharp
public OpcUaManagedSubscriptionPlan(OpcUaSubscriptionRequest Subscription, IReadOnlyList<OpcUaMonitoredItemRequest> MonitoredItems)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaManagedSubscriptionPlan` instance from the supplied values.

#### `MonitoredItems`

```csharp
public IReadOnlyList<OpcUaMonitoredItemRequest> MonitoredItems { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets monitored Items.

#### `Subscription`

```csharp
public OpcUaSubscriptionRequest Subscription { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets subscription.

## OpcUaMessageSecurityMode

```csharp
public enum OpcUaMessageSecurityMode
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported opc Ua Message Security Mode choices. Select only values documented by the target protocol or device profile.

### Members

#### `None`

```csharp
None = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the none option.

#### `Sign`

```csharp
Sign = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the sign option.

#### `SignAndEncrypt`

```csharp
SignAndEncrypt = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the sign And Encrypt option.

## OpcUaMethodCall

```csharp
public sealed class OpcUaMethodCall
```

One method call for the Call service (OPC 10000-4 §5.11.2): the Object or ObjectType that owns the method, the Method node, and the input arguments in declaration order. Each argument must carry the exact DataType the method declares; servers report a mismatch per argument (Bad_TypeMismatch).

### Members

#### `OpcUaMethodCall`

```csharp
public OpcUaMethodCall(IndustrialNodeId objectId, IndustrialNodeId methodId, DriverValue[] inputArguments)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaMethodCall` instance from the supplied values.

#### `InputArguments`

```csharp
public IReadOnlyList<DriverValue> InputArguments { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets input Arguments.

#### `MethodId`

```csharp
public IndustrialNodeId MethodId { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets method Id.

#### `ObjectId`

```csharp
public IndustrialNodeId ObjectId { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets object Id.

## OpcUaMethodResult

```csharp
public sealed class OpcUaMethodResult
```

Per-call result. A Bad `OpcUaMethodResult.StatusCode` (for example Bad_MethodInvalid or Bad_InvalidArgument) fails only this call; `OpcUaMethodResult.InputArgumentResults` then names the offending arguments when the server reports them.

### Members

#### `OpcUaMethodResult`

```csharp
public OpcUaMethodResult(uint statusCode, uint[] inputArgumentResults, DriverValue[] outputArguments)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaMethodResult` instance from the supplied values.

#### `InputArgumentResults`

```csharp
public IReadOnlyList<uint> InputArgumentResults { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets input Argument Results.

#### `IsGood`

```csharp
public bool IsGood { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets is Good.

#### `OutputArguments`

```csharp
public IReadOnlyList<DriverValue> OutputArguments { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets output Arguments.

#### `Quality`

```csharp
public DriverQuality Quality { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets quality.

#### `StatusCode`

```csharp
public uint StatusCode { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets status Code.

## OpcUaMonitoredItemModification

```csharp
public struct OpcUaMonitoredItemModification
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Monitored Item Modification in the public driver contract.

### Members

#### `OpcUaMonitoredItemModification`

```csharp
public OpcUaMonitoredItemModification(uint MonitoredItemId, uint ClientHandle, double SamplingInterval = -1, uint QueueSize = 1, bool DiscardOldest = true)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaMonitoredItemModification` instance from the supplied values.

#### `ClientHandle`

```csharp
public uint ClientHandle { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets client Handle.

#### `DiscardOldest`

```csharp
public bool DiscardOldest { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets discard Oldest.

#### `MonitoredItemId`

```csharp
public uint MonitoredItemId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets monitored Item Id.

#### `QueueSize`

```csharp
public uint QueueSize { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets queue Size.

#### `SamplingInterval`

```csharp
public double SamplingInterval { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets sampling Interval.

## OpcUaMonitoredItemModificationResult

```csharp
public struct OpcUaMonitoredItemModificationResult
```

*Undocumented: no XML summary; this text is generated from the name.* Carries the structured result of opc Ua Monitored Item Modification, including protocol status where applicable.

### Members

#### `OpcUaMonitoredItemModificationResult`

```csharp
public OpcUaMonitoredItemModificationResult(uint StatusCode, double RevisedSamplingInterval, uint RevisedQueueSize, DriverExtensionObject FilterResult)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaMonitoredItemModificationResult` instance from the supplied values.

#### `FilterResult`

```csharp
public DriverExtensionObject FilterResult { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets filter Result.

#### `RevisedQueueSize`

```csharp
public uint RevisedQueueSize { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets revised Queue Size.

#### `RevisedSamplingInterval`

```csharp
public double RevisedSamplingInterval { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets revised Sampling Interval.

#### `StatusCode`

```csharp
public uint StatusCode { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets status Code.

## OpcUaMonitoredItemRequest

```csharp
public struct OpcUaMonitoredItemRequest
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the request parameters for opc Ua Monitored Item.

### Members

#### `OpcUaMonitoredItemRequest`

```csharp
public OpcUaMonitoredItemRequest(OpcUaNodeDescriptor Node, uint ClientHandle, OpcUaMonitoringMode MonitoringMode = Reporting, double SamplingInterval = -1, uint QueueSize = 1, bool DiscardOldest = true)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaMonitoredItemRequest` instance from the supplied values.

#### `ClientHandle`

```csharp
public uint ClientHandle { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets client Handle.

#### `DiscardOldest`

```csharp
public bool DiscardOldest { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets discard Oldest.

#### `MonitoringMode`

```csharp
public OpcUaMonitoringMode MonitoringMode { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets monitoring Mode.

#### `Node`

```csharp
public OpcUaNodeDescriptor Node { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets node.

#### `QueueSize`

```csharp
public uint QueueSize { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets queue Size.

#### `SamplingInterval`

```csharp
public double SamplingInterval { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets sampling Interval.

## OpcUaMonitoredItemResult

```csharp
public struct OpcUaMonitoredItemResult
```

*Undocumented: no XML summary; this text is generated from the name.* Carries the structured result of opc Ua Monitored Item, including protocol status where applicable.

### Members

#### `OpcUaMonitoredItemResult`

```csharp
public OpcUaMonitoredItemResult(uint StatusCode, uint MonitoredItemId, double RevisedSamplingInterval, uint RevisedQueueSize, DriverExtensionObject FilterResult)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaMonitoredItemResult` instance from the supplied values.

#### `FilterResult`

```csharp
public DriverExtensionObject FilterResult { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets filter Result.

#### `MonitoredItemId`

```csharp
public uint MonitoredItemId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets monitored Item Id.

#### `RevisedQueueSize`

```csharp
public uint RevisedQueueSize { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets revised Queue Size.

#### `RevisedSamplingInterval`

```csharp
public double RevisedSamplingInterval { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets revised Sampling Interval.

#### `StatusCode`

```csharp
public uint StatusCode { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets status Code.

## OpcUaMonitoredValue

```csharp
public struct OpcUaMonitoredValue
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Monitored Value in the public driver contract.

### Members

#### `OpcUaMonitoredValue`

```csharp
public OpcUaMonitoredValue(uint ClientHandle, DriverSample Sample)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaMonitoredValue` instance from the supplied values.

#### `ClientHandle`

```csharp
public uint ClientHandle { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets client Handle.

#### `Sample`

```csharp
public DriverSample Sample { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets sample.

## OpcUaMonitoringMode

```csharp
public enum OpcUaMonitoringMode
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported opc Ua Monitoring Mode choices. Select only values documented by the target protocol or device profile.

### Members

#### `Disabled`

```csharp
Disabled = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the disabled option.

#### `Sampling`

```csharp
Sampling = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the sampling option.

#### `Reporting`

```csharp
Reporting = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the reporting option.

## OpcUaNodeClass

```csharp
public enum OpcUaNodeClass
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported opc Ua Node Class choices. Select only values documented by the target protocol or device profile.

### Members

#### `Unspecified`

```csharp
Unspecified = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the unspecified option.

#### `Object`

```csharp
Object = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the object option.

#### `Variable`

```csharp
Variable = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the variable option.

#### `Method`

```csharp
Method = 4
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the method option.

#### `ObjectType`

```csharp
ObjectType = 8
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the object Type option.

#### `VariableType`

```csharp
VariableType = 16
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the variable Type option.

#### `ReferenceType`

```csharp
ReferenceType = 32
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the reference Type option.

#### `DataType`

```csharp
DataType = 64
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the data Type option.

#### `View`

```csharp
View = 128
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the view option.

#### `All`

```csharp
All = 255
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the all option.

## OpcUaNodeDescriptor

```csharp
public struct OpcUaNodeDescriptor
```

Compiled OPC UA attribute address. NodeId parsing is kept out of the polling hot path.

### Members

#### `OpcUaNodeDescriptor`

```csharp
public OpcUaNodeDescriptor(IndustrialNodeId NodeId, OpcUaAttributeId AttributeId = Value, string IndexRange = null, IndustrialQualifiedName DataEncoding = default)
```

Compiled OPC UA attribute address. NodeId parsing is kept out of the polling hot path.

#### `ToString`

```csharp
public string ToString()
```

*Undocumented: no XML summary; this text is generated from the name.* Returns the canonical text representation of this value.

#### `AttributeId`

```csharp
public OpcUaAttributeId AttributeId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets attribute Id.

#### `DataEncoding`

```csharp
public IndustrialQualifiedName DataEncoding { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets data Encoding.

#### `IndexRange`

```csharp
public string IndexRange { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets index Range.

#### `NodeId`

```csharp
public IndustrialNodeId NodeId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets node Id.

## OpcUaPublishResponse

```csharp
public sealed class OpcUaPublishResponse
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Publish Response in the public driver contract.

### Members

#### `OpcUaPublishResponse`

```csharp
public OpcUaPublishResponse(uint subscriptionId, uint[] availableSequenceNumbers, bool moreNotifications, uint sequenceNumber, DateTime publishTimeUtc, OpcUaMonitoredValue[] values, DriverExtensionObject[] unknownNotifications, uint[] acknowledgementResults = null, OpcUaEventNotification[] events = null)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaPublishResponse` instance from the supplied values.

#### `AcknowledgementResults`

```csharp
public IReadOnlyList<uint> AcknowledgementResults { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets acknowledgement Results.

#### `AvailableSequenceNumbers`

```csharp
public IReadOnlyList<uint> AvailableSequenceNumbers { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets available Sequence Numbers.

#### `Events`

```csharp
public IReadOnlyList<OpcUaEventNotification> Events { get;  }
```

Event notifications from event monitored items, in server order.

#### `IsKeepAlive`

```csharp
public bool IsKeepAlive { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets is Keep Alive.

#### `MoreNotifications`

```csharp
public bool MoreNotifications { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets more Notifications.

#### `PublishTimeUtc`

```csharp
public DateTime PublishTimeUtc { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets publish Time Utc.

#### `SequenceNumber`

```csharp
public uint SequenceNumber { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets sequence Number.

#### `SubscriptionId`

```csharp
public uint SubscriptionId { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets subscription Id.

#### `UnknownNotifications`

```csharp
public IReadOnlyList<DriverExtensionObject> UnknownNotifications { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets unknown Notifications.

#### `Values`

```csharp
public IReadOnlyList<OpcUaMonitoredValue> Values { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets values.

## OpcUaReferenceDescription

```csharp
public struct OpcUaReferenceDescription
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Reference Description in the public driver contract.

### Members

#### `OpcUaReferenceDescription`

```csharp
public OpcUaReferenceDescription(IndustrialNodeId ReferenceTypeId, bool IsForward, IndustrialExpandedNodeId NodeId, IndustrialQualifiedName BrowseName, IndustrialLocalizedText DisplayName, OpcUaNodeClass NodeClass, IndustrialExpandedNodeId TypeDefinition)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaReferenceDescription` instance from the supplied values.

#### `BrowseName`

```csharp
public IndustrialQualifiedName BrowseName { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets browse Name.

#### `DisplayName`

```csharp
public IndustrialLocalizedText DisplayName { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets display Name.

#### `IsForward`

```csharp
public bool IsForward { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets is Forward.

#### `NodeClass`

```csharp
public OpcUaNodeClass NodeClass { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets node Class.

#### `NodeId`

```csharp
public IndustrialExpandedNodeId NodeId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets node Id.

#### `ReferenceTypeId`

```csharp
public IndustrialNodeId ReferenceTypeId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets reference Type Id.

#### `TypeDefinition`

```csharp
public IndustrialExpandedNodeId TypeDefinition { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets type Definition.

## OpcUaRelativePathElement

```csharp
public struct OpcUaRelativePathElement
```

One element of a RelativePath (OPC 10000-4 §7.31): follow references of `OpcUaRelativePathElement.ReferenceTypeId` (default HierarchicalReferences, i=33) to a target whose BrowseName is `OpcUaRelativePathElement.TargetName`.

### Members

#### `OpcUaRelativePathElement`

```csharp
public OpcUaRelativePathElement(IndustrialQualifiedName TargetName, IndustrialNodeId ReferenceTypeId = null, bool IsInverse = false, bool IncludeSubtypes = true)
```

One element of a RelativePath (OPC 10000-4 §7.31): follow references of `OpcUaRelativePathElement.ReferenceTypeId` (default HierarchicalReferences, i=33) to a target whose BrowseName is `OpcUaRelativePathElement.TargetName`.

#### `IncludeSubtypes`

```csharp
public bool IncludeSubtypes { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets include Subtypes.

#### `IsInverse`

```csharp
public bool IsInverse { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets is Inverse.

#### `ReferenceTypeId`

```csharp
public IndustrialNodeId ReferenceTypeId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets reference Type Id.

#### `TargetName`

```csharp
public IndustrialQualifiedName TargetName { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets target Name.

## OpcUaSecureChannelInfo

```csharp
public struct OpcUaSecureChannelInfo
```

*Undocumented: no XML summary; this text is generated from the name.* Reports negotiated or discovered opc Ua Secure Channel information.

### Members

#### `OpcUaSecureChannelInfo`

```csharp
public OpcUaSecureChannelInfo(uint ChannelId, uint TokenId, DateTime CreatedAtUtc, uint RevisedLifetimeMilliseconds, uint ServerProtocolVersion)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaSecureChannelInfo` instance from the supplied values.

#### `ChannelId`

```csharp
public uint ChannelId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets channel Id.

#### `CreatedAtUtc`

```csharp
public DateTime CreatedAtUtc { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets created At Utc.

#### `RevisedLifetimeMilliseconds`

```csharp
public uint RevisedLifetimeMilliseconds { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets revised Lifetime Milliseconds.

#### `ServerProtocolVersion`

```csharp
public uint ServerProtocolVersion { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets server Protocol Version.

#### `TokenId`

```csharp
public uint TokenId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets token Id.

## OpcUaSecurityPolicyUris

```csharp
public static class OpcUaSecurityPolicyUris
```

SecurityPolicy URIs (OPC 10000-7) for `OpcUaTcpOptions.SecurityPolicyUri` and for matching the `OpcUaEndpointDescription.SecurityPolicyUri` a server advertises.

### Members

#### `Aes128Sha256RsaOaep`

```csharp
Aes128Sha256RsaOaep = http://opcfoundation.org/UA/SecurityPolicy#Aes128_Sha256_RsaOaep
```

Aes128_Sha256_RsaOaep: RSA PKCS#1 v1.5 SHA-256 signatures, RSA-OAEP (SHA-1), AES-128-CBC, HMAC-SHA256.

#### `Aes256Sha256RsaPss`

```csharp
Aes256Sha256RsaPss = http://opcfoundation.org/UA/SecurityPolicy#Aes256_Sha256_RsaPss
```

Aes256_Sha256_RsaPss: RSA-PSS SHA-256 signatures, RSA-OAEP SHA-256, AES-256-CBC, HMAC-SHA256.

#### `Basic256Sha256`

```csharp
Basic256Sha256 = http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256
```

Basic256Sha256: RSA PKCS#1 v1.5 SHA-256 signatures, RSA-OAEP (SHA-1), AES-256-CBC, HMAC-SHA256.

#### `None`

```csharp
None = http://opcfoundation.org/UA/SecurityPolicy#None
```

No signing or encryption; requires `OpcUaMessageSecurityMode.None`.

## OpcUaSecurityTokenRequestType

```csharp
public enum OpcUaSecurityTokenRequestType
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported opc Ua Security Token Request Type choices. Select only values documented by the target protocol or device profile.

### Members

#### `Issue`

```csharp
Issue = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the issue option.

#### `Renew`

```csharp
Renew = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the renew option.

## OpcUaSessionInfo

```csharp
public struct OpcUaSessionInfo
```

*Undocumented: no XML summary; this text is generated from the name.* Reports negotiated or discovered opc Ua Session information.

### Members

#### `OpcUaSessionInfo`

```csharp
public OpcUaSessionInfo(IndustrialNodeId SessionId, IndustrialNodeId AuthenticationToken, double RevisedSessionTimeoutMilliseconds, uint MaxRequestMessageSize, string AnonymousPolicyId, bool IsActivated, OpcUaUserTokenPolicy[] UserIdentityTokens = null)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaSessionInfo` instance from the supplied values.

#### `AnonymousPolicyId`

```csharp
public string AnonymousPolicyId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets anonymous Policy Id.

#### `AuthenticationToken`

```csharp
public IndustrialNodeId AuthenticationToken { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets authentication Token.

#### `IsActivated`

```csharp
public bool IsActivated { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets is Activated.

#### `MaxRequestMessageSize`

```csharp
public uint MaxRequestMessageSize { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets max Request Message Size.

#### `RevisedSessionTimeoutMilliseconds`

```csharp
public double RevisedSessionTimeoutMilliseconds { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets revised Session Timeout Milliseconds.

#### `SessionId`

```csharp
public IndustrialNodeId SessionId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets session Id.

#### `UserIdentityTokens`

```csharp
public OpcUaUserTokenPolicy[] UserIdentityTokens { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets user Identity Tokens.

## OpcUaStructureDataTypeDefinition

```csharp
public sealed class OpcUaStructureDataTypeDefinition
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Structure Data Type Definition in the public driver contract.

### Members

#### `OpcUaStructureDataTypeDefinition`

```csharp
public OpcUaStructureDataTypeDefinition(IndustrialNodeId DefaultEncodingId, IndustrialNodeId BaseDataType, OpcUaStructureType StructureType, OpcUaStructureFieldDefinition[] Fields)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaStructureDataTypeDefinition` instance from the supplied values.

#### `BaseDataType`

```csharp
public IndustrialNodeId BaseDataType { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets base Data Type.

#### `DefaultEncodingId`

```csharp
public IndustrialNodeId DefaultEncodingId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets default Encoding Id.

#### `Fields`

```csharp
public OpcUaStructureFieldDefinition[] Fields { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets fields.

#### `StructureType`

```csharp
public OpcUaStructureType StructureType { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets structure Type.

## OpcUaStructureFieldDefinition

```csharp
public struct OpcUaStructureFieldDefinition
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Structure Field Definition in the public driver contract.

### Members

#### `OpcUaStructureFieldDefinition`

```csharp
public OpcUaStructureFieldDefinition(string Name, IndustrialLocalizedText Description, IndustrialNodeId DataType, int ValueRank, uint[] ArrayDimensions, uint MaxStringLength, bool IsOptional)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaStructureFieldDefinition` instance from the supplied values.

#### `ArrayDimensions`

```csharp
public uint[] ArrayDimensions { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets array Dimensions.

#### `DataType`

```csharp
public IndustrialNodeId DataType { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets data Type.

#### `Description`

```csharp
public IndustrialLocalizedText Description { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets description.

#### `IsOptional`

```csharp
public bool IsOptional { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets is Optional.

#### `MaxStringLength`

```csharp
public uint MaxStringLength { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets max String Length.

#### `Name`

```csharp
public string Name { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets name.

#### `ValueRank`

```csharp
public int ValueRank { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets value Rank.

## OpcUaStructureType

```csharp
public enum OpcUaStructureType
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported opc Ua Structure Type choices. Select only values documented by the target protocol or device profile.

### Members

#### `Structure`

```csharp
Structure = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the structure option.

#### `StructureWithOptionalFields`

```csharp
StructureWithOptionalFields = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the structure With Optional Fields option.

#### `Union`

```csharp
Union = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the union option.

#### `StructureWithSubtypedValues`

```csharp
StructureWithSubtypedValues = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the structure With Subtyped Values option.

#### `UnionWithSubtypedValues`

```csharp
UnionWithSubtypedValues = 4
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the union With Subtyped Values option.

## OpcUaSubscription

```csharp
public sealed class OpcUaSubscription
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Subscription in the public driver contract.

### Members

#### `DisposeAsync`

```csharp
public ValueTask DisposeAsync()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs dispose Async.

#### `ReadNotificationsAsync`

```csharp
public IAsyncEnumerable<OpcUaPublishResponse> ReadNotificationsAsync(CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads notifications asynchronously.

#### `Completion`

```csharp
public Task Completion { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets completion.

#### `DeliveredNotifications`

```csharp
public int DeliveredNotifications { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets delivered Notifications.

#### `Fault`

```csharp
public DriverError? Fault { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets fault.

#### `PublishCycles`

```csharp
public int PublishCycles { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets publish Cycles.

#### `RecreationCount`

```csharp
public int RecreationCount { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets recreation Count.

#### `SubscriptionId`

```csharp
public uint SubscriptionId { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets subscription Id.

## OpcUaSubscriptionAcknowledgement

```csharp
public struct OpcUaSubscriptionAcknowledgement
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Subscription Acknowledgement in the public driver contract.

### Members

#### `OpcUaSubscriptionAcknowledgement`

```csharp
public OpcUaSubscriptionAcknowledgement(uint SubscriptionId, uint SequenceNumber)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaSubscriptionAcknowledgement` instance from the supplied values.

#### `SequenceNumber`

```csharp
public uint SequenceNumber { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets sequence Number.

#### `SubscriptionId`

```csharp
public uint SubscriptionId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets subscription Id.

## OpcUaSubscriptionFaultException

```csharp
public sealed class OpcUaSubscriptionFaultException
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Subscription Fault Exception in the public driver contract.

### Members

#### `OpcUaSubscriptionFaultException`

```csharp
public OpcUaSubscriptionFaultException(DriverError error)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaSubscriptionFaultException` instance from the supplied values.

#### `Error`

```csharp
public DriverError Error { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets error.

## OpcUaSubscriptionInfo

```csharp
public struct OpcUaSubscriptionInfo
```

*Undocumented: no XML summary; this text is generated from the name.* Reports negotiated or discovered opc Ua Subscription information.

### Members

#### `OpcUaSubscriptionInfo`

```csharp
public OpcUaSubscriptionInfo(uint SubscriptionId, double RevisedPublishingInterval, uint RevisedLifetimeCount, uint RevisedMaxKeepAliveCount)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaSubscriptionInfo` instance from the supplied values.

#### `RevisedLifetimeCount`

```csharp
public uint RevisedLifetimeCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets revised Lifetime Count.

#### `RevisedMaxKeepAliveCount`

```csharp
public uint RevisedMaxKeepAliveCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets revised Max Keep Alive Count.

#### `RevisedPublishingInterval`

```csharp
public double RevisedPublishingInterval { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets revised Publishing Interval.

#### `SubscriptionId`

```csharp
public uint SubscriptionId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets subscription Id.

## OpcUaSubscriptionRequest

```csharp
public struct OpcUaSubscriptionRequest
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the request parameters for opc Ua Subscription.

### Members

#### `OpcUaSubscriptionRequest`

```csharp
public OpcUaSubscriptionRequest(double RequestedPublishingInterval = 1000, uint RequestedLifetimeCount = 60, uint RequestedMaxKeepAliveCount = 10, uint MaxNotificationsPerPublish = 0, bool PublishingEnabled = true, byte Priority = 0)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaSubscriptionRequest` instance from the supplied values.

#### `Default`

```csharp
public static OpcUaSubscriptionRequest Default { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets default.

#### `MaxNotificationsPerPublish`

```csharp
public uint MaxNotificationsPerPublish { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets max Notifications Per Publish.

#### `Priority`

```csharp
public byte Priority { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets priority.

#### `PublishingEnabled`

```csharp
public bool PublishingEnabled { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets publishing Enabled.

#### `RequestedLifetimeCount`

```csharp
public uint RequestedLifetimeCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets requested Lifetime Count.

#### `RequestedMaxKeepAliveCount`

```csharp
public uint RequestedMaxKeepAliveCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets requested Max Keep Alive Count.

#### `RequestedPublishingInterval`

```csharp
public double RequestedPublishingInterval { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets requested Publishing Interval.

## OpcUaSubscriptionRuntimeOptions

```csharp
public sealed class OpcUaSubscriptionRuntimeOptions
```

*Undocumented: no XML summary; this text is generated from the name.* Configures opc Ua Subscription Runtime. Create and validate it once for each endpoint.

### Members

#### `OpcUaSubscriptionRuntimeOptions`

```csharp
public OpcUaSubscriptionRuntimeOptions()
```

Creates an instance with default member values.

#### `MaximumReconnectAttempts`

```csharp
public int MaximumReconnectAttempts { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets maximum Reconnect Attempts.

#### `MaximumRepublishGap`

```csharp
public int MaximumRepublishGap { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets maximum Republish Gap.

#### `NotificationBufferCapacity`

```csharp
public int NotificationBufferCapacity { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets notification Buffer Capacity.

#### `ReconnectDelay`

```csharp
public TimeSpan ReconnectDelay { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets reconnect Delay.

## OpcUaTcpClient

```csharp
public sealed class OpcUaTcpClient
```

Native UA-TCP / UA Binary client with SecurityPolicy#None SecureChannel, anonymous Session, typed Read/Write/Browse/Subscription services, DataTypeDefinition discovery and lossless DataValue quality/timestamp mapping. Secure policies are a separate milestone.

### Members

#### `OpcUaTcpClient`

```csharp
public OpcUaTcpClient(OpcUaTcpOptions options)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaTcpClient` instance from the supplied values.

#### `AcknowledgeConditionAsync`

```csharp
public ValueTask<DriverResult> AcknowledgeConditionAsync(IndustrialNodeId conditionId, ReadOnlyMemory<byte> eventId, IndustrialLocalizedText comment, CancellationToken cancellationToken = default)
```

Acknowledges a condition event (AcknowledgeableConditionType.Acknowledge, OPC 10000-9 §5.7.3). `conditionId` comes from `OpcUaEventField.ConditionId` and `eventId` from the EventId field of the event being acknowledged.

#### `ActivateAnonymousSessionAsync`

```csharp
public ValueTask<DriverResult> ActivateAnonymousSessionAsync(CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs activate Anonymous Session Async.

#### `ActivateCertificateSessionAsync`

```csharp
public ValueTask<DriverResult> ActivateCertificateSessionAsync(X509Certificate2 userCertificate, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs activate Certificate Session Async.

#### `ActivateUserNameSessionAsync`

```csharp
public ValueTask<DriverResult> ActivateUserNameSessionAsync(string userName, string password, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs activate User Name Session Async.

#### `BrowseAllAsync`

```csharp
public ValueTask<DriverResult<OpcUaReferenceDescription[]>> BrowseAllAsync(OpcUaBrowseDescription description, uint requestedMaxReferencesPerNode = 0, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Browses all asynchronously.

#### `BrowseAsync`

```csharp
public ValueTask<DriverResult<OpcUaBrowseResult>> BrowseAsync(OpcUaBrowseDescription description, uint requestedMaxReferencesPerNode = 0, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Browses the requested value asynchronously.

#### `BrowseAsync`

```csharp
public ValueTask<DriverResult<OpcUaBrowseResult[]>> BrowseAsync(ReadOnlyMemory<OpcUaBrowseDescription> descriptions, uint requestedMaxReferencesPerNode = 0, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Browses the requested value asynchronously.

#### `BrowseNextAsync`

```csharp
public ValueTask<DriverResult<OpcUaBrowseResult>> BrowseNextAsync(ReadOnlyMemory<byte> continuationPoint, bool releaseContinuationPoint = false, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Browses next asynchronously.

#### `CallAsync`

```csharp
public ValueTask<DriverResult<OpcUaMethodResult>> CallAsync(IndustrialNodeId objectId, IndustrialNodeId methodId, ReadOnlyMemory<DriverValue> inputArguments = default, CancellationToken cancellationToken = default)
```

Calls one method and returns its result. A method that the server rejects returns a successful `DriverResult` whose `OpcUaMethodResult.StatusCode` is Bad; transport, Session and ServiceFault failures return a failed result. A method call is not idempotent in general: after a timeout or a lost connection the outcome is unknown and the call is never repeated.

#### `CallAsync`

```csharp
public ValueTask<DriverResult<OpcUaMethodResult[]>> CallAsync(ReadOnlyMemory<OpcUaMethodCall> calls, CancellationToken cancellationToken = default)
```

Calls methods with the Call service (OPC 10000-4 §5.11.2), one result per call in order.

#### `CloseSessionAsync`

```csharp
public ValueTask<DriverResult> CloseSessionAsync(bool deleteSubscriptions = true, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs close Session Async.

#### `ConnectAsync`

```csharp
public ValueTask<DriverResult> ConnectAsync(CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Opens and initializes the protocol connection asynchronously.

#### `CreateEventMonitoredItemsAsync`

```csharp
public ValueTask<DriverResult<OpcUaEventMonitoredItemResult[]>> CreateEventMonitoredItemsAsync(uint subscriptionId, ReadOnlyMemory<OpcUaEventMonitoredItemRequest> items, CancellationToken cancellationToken = default)
```

Creates event monitored items (AttributeId EventNotifier with an EventFilter, OPC 10000-4 §5.12.2 and §7.22.3). Event notifications arrive in `OpcUaPublishResponse.Events`.

#### `CreateManagedSubscriptionAsync`

```csharp
public ValueTask<DriverResult<OpcUaSubscription>> CreateManagedSubscriptionAsync(OpcUaManagedSubscriptionPlan plan, OpcUaSubscriptionRuntimeOptions options = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates managed Subscription.

#### `CreateMonitoredItemsAsync`

```csharp
public ValueTask<DriverResult<OpcUaMonitoredItemResult[]>> CreateMonitoredItemsAsync(uint subscriptionId, ReadOnlyMemory<OpcUaMonitoredItemRequest> items, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates monitored Items.

#### `CreateSessionAsync`

```csharp
public ValueTask<DriverResult> CreateSessionAsync(CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates session.

#### `CreateSubscriptionAsync`

```csharp
public ValueTask<DriverResult<OpcUaSubscriptionInfo>> CreateSubscriptionAsync(OpcUaSubscriptionRequest request, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates subscription.

#### `DeleteMonitoredItemsAsync`

```csharp
public ValueTask<DriverResult<uint[]>> DeleteMonitoredItemsAsync(uint subscriptionId, ReadOnlyMemory<uint> monitoredItemIds, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Deletes monitored Items.

#### `DeleteSubscriptionsAsync`

```csharp
public ValueTask<DriverResult<uint[]>> DeleteSubscriptionsAsync(ReadOnlyMemory<uint> subscriptionIds, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Deletes subscriptions.

#### `DisconnectAsync`

```csharp
public ValueTask<DriverResult> DisconnectAsync(CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Closes the protocol connection gracefully and releases connection state.

#### `DiscoverEndpointsAsync`

```csharp
public static ValueTask<DriverResult<OpcUaEndpointDescription[]>> DiscoverEndpointsAsync(Uri discoveryUrl, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Discovers endpoints.

#### `DisposeAsync`

```csharp
public ValueTask DisposeAsync()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs dispose Async.

#### `GetEndpointsAsync`

```csharp
public ValueTask<DriverResult<OpcUaEndpointDescription[]>> GetEndpointsAsync(IReadOnlyList<string> profileUris = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Gets endpoints.

#### `HistoryReadAtTimeAsync`

```csharp
public ValueTask<DriverResult<OpcUaHistoryReadResult[]>> HistoryReadAtTimeAsync(ReadOnlyMemory<OpcUaHistoryReadNode> nodes, ReadOnlyMemory<DateTime> requestedTimes, bool useSimpleBounds = true, OpcUaTimestampsToReturn timestampsToReturn = Both, CancellationToken cancellationToken = default)
```

Reads values at the given times with HistoryRead and ReadAtTimeDetails (OPC 10000-11 §6.4.5). The server interpolates between stored values when `useSimpleBounds` is true.

#### `HistoryReadRawAsync`

```csharp
public ValueTask<DriverResult<OpcUaHistoryReadResult[]>> HistoryReadRawAsync(ReadOnlyMemory<OpcUaHistoryReadNode> nodes, OpcUaHistoryReadRawRequest request, CancellationToken cancellationToken = default)
```

Reads raw (or modified) historical values with HistoryRead and ReadRawModifiedDetails (OPC 10000-11 §6.4.3). A result with a continuation point has more values; call again with that continuation point, or release it.

#### `ModifyMonitoredItemsAsync`

```csharp
public ValueTask<DriverResult<OpcUaMonitoredItemModificationResult[]>> ModifyMonitoredItemsAsync(uint subscriptionId, ReadOnlyMemory<OpcUaMonitoredItemModification> items, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Modifies monitored Items.

#### `OpenAnonymousSessionAsync`

```csharp
public ValueTask<DriverResult> OpenAnonymousSessionAsync(CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs open Anonymous Session Async.

#### `OpenCertificateSessionAsync`

```csharp
public ValueTask<DriverResult> OpenCertificateSessionAsync(X509Certificate2 userCertificate, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs open Certificate Session Async.

#### `OpenUserNameSessionAsync`

```csharp
public ValueTask<DriverResult> OpenUserNameSessionAsync(string userName, string password, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs open User Name Session Async.

#### `PublishAsync`

```csharp
public ValueTask<DriverResult<OpcUaPublishResponse>> PublishAsync(ReadOnlyMemory<OpcUaSubscriptionAcknowledgement> acknowledgements, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Publishes or receives the requested value.

#### `ReadAsync`

```csharp
public ValueTask<DriverResult<DriverSample>> ReadAsync(OpcUaNodeDescriptor descriptor, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads the requested value asynchronously.

#### `ReadAsync`

```csharp
public ValueTask<DriverResult<int>> ReadAsync(ReadOnlyMemory<OpcUaNodeDescriptor> nodes, Memory<DriverSample> destination, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads the requested value asynchronously.

#### `ReadDataTypeDefinitionAsync`

```csharp
public ValueTask<DriverResult<OpcUaDiscoveredDataType>> ReadDataTypeDefinitionAsync(IndustrialNodeId dataTypeNodeId, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads data Type Definition asynchronously.

#### `RefreshConditionsAsync`

```csharp
public ValueTask<DriverResult> RefreshConditionsAsync(uint subscriptionId, CancellationToken cancellationToken = default)
```

Asks the server to resend the current state of every condition to the subscription (ConditionRefresh, OPC 10000-9 §5.5.7). The server brackets the resent events with RefreshStartEventType and RefreshEndEventType events.

#### `ReleaseHistoryContinuationPointsAsync`

```csharp
public ValueTask<DriverResult> ReleaseHistoryContinuationPointsAsync(ReadOnlyMemory<OpcUaHistoryReadNode> nodes, CancellationToken cancellationToken = default)
```

Releases the server resources of history continuation points without reading more values (HistoryRead with releaseContinuationPoints set).

#### `RenewSecureChannelAsync`

```csharp
public ValueTask<DriverResult> RenewSecureChannelAsync(CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs renew Secure Channel Async.

#### `RepublishAsync`

```csharp
public ValueTask<DriverResult<OpcUaPublishResponse>> RepublishAsync(uint subscriptionId, uint retransmitSequenceNumber, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs republish Async.

#### `SetMonitoringModeAsync`

```csharp
public ValueTask<DriverResult<uint[]>> SetMonitoringModeAsync(uint subscriptionId, OpcUaMonitoringMode mode, ReadOnlyMemory<uint> monitoredItemIds, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Sets monitoring Mode.

#### `StartSubscriptionPumpAsync`

```csharp
public ValueTask<DriverResult<OpcUaSubscription>> StartSubscriptionPumpAsync(uint subscriptionId, OpcUaSubscriptionRuntimeOptions options = null)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs start Subscription Pump Async.

#### `TranslateBrowsePathsToNodeIdsAsync`

```csharp
public ValueTask<DriverResult<OpcUaBrowsePathResult[]>> TranslateBrowsePathsToNodeIdsAsync(ReadOnlyMemory<OpcUaBrowsePath> paths, CancellationToken cancellationToken = default)
```

Resolves browse paths to NodeIds with TranslateBrowsePathsToNodeIds (OPC 10000-4 §5.8.4). Each path gets its own status; a path that matches nothing returns a Bad status (for example Bad_NoMatch) instead of failing the call.

#### `WriteAsync`

```csharp
public ValueTask<DriverResult> WriteAsync(OpcUaNodeDescriptor descriptor, DriverValue value, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Writes the requested value asynchronously.

#### `WriteAsync`

```csharp
public ValueTask<DriverResult> WriteAsync(ReadOnlyMemory<OpcUaWriteItem> items, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Writes the requested value asynchronously.

#### `IsConnected`

```csharp
public bool IsConnected { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets is Connected.

#### `IsSessionActive`

```csharp
public bool IsSessionActive { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets is Session Active.

#### `NegotiatedLimits`

```csharp
public OpcUaConnectionLimits? NegotiatedLimits { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets negotiated Limits.

#### `SecureChannel`

```csharp
public OpcUaSecureChannelInfo? SecureChannel { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets secure Channel.

#### `Session`

```csharp
public OpcUaSessionInfo? Session { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets session.

#### `SessionReactivated`

```csharp
public bool SessionReactivated { get;  }
```

True when the last Open*SessionAsync activated the Session of a lost SecureChannel again instead of creating a new one (`OpcUaTcpOptions.ReactivateSessionOnReconnect`).

## OpcUaTcpOptions

```csharp
public sealed class OpcUaTcpOptions
```

*Undocumented: no XML summary; this text is generated from the name.* Configures opc Ua Tcp. Create and validate it once for each endpoint.

### Members

#### `OpcUaTcpOptions`

```csharp
public OpcUaTcpOptions()
```

Creates an instance with default member values.

#### `ApplicationIdentity`

```csharp
public OpcUaApplicationIdentity ApplicationIdentity { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets application Identity.

#### `ApplicationName`

```csharp
public string ApplicationName { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets application Name.

#### `ApplicationUri`

```csharp
public string ApplicationUri { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets application Uri.

#### `CertificateTrust`

```csharp
public OpcUaCertificateTrustOptions CertificateTrust { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets certificate Trust.

#### `ConnectTimeout`

```csharp
public TimeSpan ConnectTimeout { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets connect Timeout.

#### `EndpointServerCertificate`

```csharp
public X509Certificate2 EndpointServerCertificate { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets endpoint Server Certificate.

#### `EndpointUrl`

```csharp
public Uri EndpointUrl { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets endpoint Url.

#### `MaxBrowseContinuationPages`

```csharp
public int MaxBrowseContinuationPages { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets max Browse Continuation Pages.

#### `MaxBrowseReferences`

```csharp
public int MaxBrowseReferences { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets max Browse References.

#### `MaxChunkCount`

```csharp
public uint MaxChunkCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets max Chunk Count.

#### `MaxMessageSize`

```csharp
public uint MaxMessageSize { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets max Message Size.

#### `MaxResponseMessageSize`

```csharp
public uint MaxResponseMessageSize { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets max Response Message Size.

#### `PreferredLocales`

```csharp
public string[] PreferredLocales { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets preferred Locales.

#### `ProductUri`

```csharp
public string ProductUri { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets product Uri.

#### `PublishRequestTimeout`

```csharp
public TimeSpan PublishRequestTimeout { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets publish Request Timeout.

#### `ReactivateSessionOnReconnect`

```csharp
public bool ReactivateSessionOnReconnect { get; init;  }
```

When the SecureChannel is lost while a Session is active, the next Open*SessionAsync on a new channel first activates that Session again (OPC 10000-4 5.6.3) instead of creating a new one, so the server does not keep an orphaned Session until its timeout. The Session's subscriptions survive; those without a running pump are deleted. When the server no longer knows the Session, a new one is created. Default true.

#### `ReceiveBufferSize`

```csharp
public uint ReceiveBufferSize { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets receive Buffer Size.

#### `RequestedSecureChannelLifetimeMilliseconds`

```csharp
public uint RequestedSecureChannelLifetimeMilliseconds { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets requested Secure Channel Lifetime Milliseconds.

#### `RequestedSessionTimeoutMilliseconds`

```csharp
public double RequestedSessionTimeoutMilliseconds { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets requested Session Timeout Milliseconds.

#### `RequestTimeout`

```csharp
public TimeSpan RequestTimeout { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets request Timeout.

#### `SecurityMode`

```csharp
public OpcUaMessageSecurityMode SecurityMode { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets security Mode.

#### `SecurityPolicyUri`

```csharp
public string SecurityPolicyUri { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets security Policy Uri.

#### `SendBufferSize`

```csharp
public uint SendBufferSize { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets send Buffer Size.

#### `SessionName`

```csharp
public string SessionName { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets session Name.

## OpcUaTimestampsToReturn

```csharp
public enum OpcUaTimestampsToReturn
```

Which timestamps a HistoryRead returns (OPC 10000-4 §7.40, TimestampsToReturn).

### Members

#### `Source`

```csharp
Source = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the source option.

#### `Server`

```csharp
Server = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the server option.

#### `Both`

```csharp
Both = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the both option.

## OpcUaUserTokenPolicy

```csharp
public sealed class OpcUaUserTokenPolicy
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua User Token Policy in the public driver contract.

### Members

#### `OpcUaUserTokenPolicy`

```csharp
public OpcUaUserTokenPolicy(string PolicyId, OpcUaUserTokenType TokenType, string IssuedTokenType, string IssuerEndpointUrl, string SecurityPolicyUri)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaUserTokenPolicy` instance from the supplied values.

#### `IssuedTokenType`

```csharp
public string IssuedTokenType { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets issued Token Type.

#### `IssuerEndpointUrl`

```csharp
public string IssuerEndpointUrl { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets issuer Endpoint Url.

#### `PolicyId`

```csharp
public string PolicyId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets policy Id.

#### `SecurityPolicyUri`

```csharp
public string SecurityPolicyUri { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets security Policy Uri.

#### `TokenType`

```csharp
public OpcUaUserTokenType TokenType { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets token Type.

## OpcUaUserTokenType

```csharp
public enum OpcUaUserTokenType
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported opc Ua User Token Type choices. Select only values documented by the target protocol or device profile.

### Members

#### `Anonymous`

```csharp
Anonymous = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the anonymous option.

#### `UserName`

```csharp
UserName = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the user Name option.

#### `Certificate`

```csharp
Certificate = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the certificate option.

#### `IssuedToken`

```csharp
IssuedToken = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the issued Token option.

## OpcUaWriteItem

```csharp
public struct OpcUaWriteItem
```

*Undocumented: no XML summary; this text is generated from the name.* Represents opc Ua Write Item in the public driver contract.

### Members

#### `OpcUaWriteItem`

```csharp
public OpcUaWriteItem(OpcUaNodeDescriptor Descriptor, DriverSample Sample)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `OpcUaWriteItem` instance from the supplied values.

#### `Value`

```csharp
public static OpcUaWriteItem Value(OpcUaNodeDescriptor descriptor, DriverValue value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs value.

#### `Descriptor`

```csharp
public OpcUaNodeDescriptor Descriptor { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets descriptor.

#### `Sample`

```csharp
public DriverSample Sample { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets sample.

