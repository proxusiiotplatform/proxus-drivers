---
id: public-errors-opc-ua
title: OPC UA errors
audience: developer
status: generated
since: 0.1.0
last_verified: 2026-08-13
---

# OPC UA errors

The message is a concise explanation of the condition. At runtime, use the complete structured
error for retry, connection health, native status, cause, and suggested operator action.

| Code | Explanation |
|---|---|
| `UA_ACK_BUFFER` | Server negotiated invalid buffers receive={serverReceive}, send={serverSend}. |
| `UA_ACK_BUFFER_LIMIT` | Server Acknowledge exceeds buffer limits proposed by the client. |
| `UA_ACK_EXPECTED` | Server did not return an OPC UA Acknowledge message. |
| `UA_ACK_LENGTH` | OPC UA Acknowledge message length was {message.Length}, expected 28. |
| `UA_APPLICATION_DESCRIPTION` | ApplicationUri, ProductUri and ApplicationName are required. |
| `UA_ATTRIBUTE_ID` | Unknown OPC UA AttributeId {(uint)node.AttributeId}. |
| `UA_BROWSE_DIRECTION` | Unknown BrowseDirection {(uint)item.Direction}. |
| `UA_BROWSE_LIMITS` | MaxBrowseContinuationPages and MaxBrowseReferences must both be positive. |
| `UA_BROWSE_PAGE_LIMIT` | Browse exceeded the configured continuation-page limit {_options.MaxBrowseContinuationPages}. |
| `UA_BROWSE_PATH` | Each browse path needs between 1 and {MaximumBrowsePathElements} elements. |
| `UA_BROWSE_REFERENCE_LIMIT` | Browse exceeded the configured reference limit {_options.MaxBrowseReferences}. |
| `UA_BROWSE_RESULT_MASK` | Browse ResultMask 0x{(uint)item.ResultMask:X} contains unsupported bits. |
| `UA_BROWSE_STATUS` | Server returned a Bad StatusCode for Browse of {nodeId}. |
| `UA_BUFFER_TOO_SMALL` | OPC UA TCP buffers must be at least {OpcUaProtocol.MinimumBufferSize} bytes for non-ECC profiles. |
| `UA_CALL_REQUEST` | Each method call needs at most {MaximumMethodArguments} input arguments. |
| `UA_CERT_APPLICATION_URI` | Certificate SubjectAltName does not contain ApplicationUri '{expectedApplicationUri}'. |
| `UA_CERT_EXTENDED_KEY_USAGE` | The certificate ExtendedKeyUsage does not contain {requiredExtendedKeyUsage}. |
| `UA_CERT_HOSTNAME` | Certificate SubjectAltName does not match endpoint host '{endpointHost}'. |
| `UA_CERT_KEY_USAGE` | The certificate KeyUsage must allow DigitalSignature and KeyEncipherment. |
| `UA_CERT_RSA_SIZE` | The {role} RSA key is {keySize} bits; {policy.Uri} requires {policy.MinimumRsaKeySize} through {policy.MaximumRsaKeySize} bits. |
| `UA_CERT_TIME_INVALID` | Certificate validity is {certificate.NotBefore:u} through {certificate.NotAfter:u}. |
| `UA_CHANNEL_CLOSED` | The OPC UA SecureChannel was closed before the service response arrived. |
| `UA_CHANNEL_LIFETIME` | Requested SecureChannel lifetime must be greater than zero. |
| `UA_CHANNEL_LOST` | SecureChannel is not available. |
| `UA_CHANNEL_REQUIRED` | An open SecureChannel is required before renewal. |
| `UA_CHUNKING_NOT_IMPLEMENTED` | Server returned OPC UA chunk type '{(char)message[3]}'; multi-chunk assembly is not enabled in this milestone. |
| `UA_CLIENT_CERT_PRIVATE_KEY` | The client application certificate does not have a private key. |
| `UA_CLIENT_CERT_REQUIRED` | A client application certificate with its private key is required. |
| `UA_CLIENT_CERT_RSA` | The client application certificate must use an RSA key. |
| `UA_CLOSE_CHANNEL_CANCELLED` | CloseSecureChannel was cancelled by the caller. |
| `UA_CONDITION_EVENT_ID` | Acknowledge needs the EventId of the event being acknowledged. |
| `UA_CONNECT_CANCELLED` | OPC UA connection was cancelled by the caller. |
| `UA_CONNECT_TIMEOUT` | OPC UA connection or handshake timed out. |
| `UA_CONNECT_TRANSPORT` | OPC UA transport failed: {ex.Message} |
| `UA_CONTINUATION_POINT` | BrowseNext requires a non-empty continuation point. |
| `UA_DATATYPE_ATTRIBUTE` | Server returned a Bad StatusCode while reading {(i == 0 ? |
| `UA_DELETE_MONITORED_ITEMS_OPTIONS` | SubscriptionId and monitored item identifiers must be non-zero. |
| `UA_DELETE_SUBSCRIPTIONS_OPTIONS` | At least one non-zero SubscriptionId is required. |
| `UA_DISPOSED` | OPC UA client has been disposed. |
| `UA_ENDPOINT_NOT_FOUND` | CreateSession did not return the configured security endpoint. |
| `UA_ENDPOINT_PORT` | Endpoint URL must contain a valid TCP port. |
| `UA_ENDPOINT_SCHEME` | Endpoint URL must use the opc.tcp scheme. |
| `UA_EUROMAP_EVENTS` | The server refused event subscriptions on {target.Name} and on the Server object. |
| `UA_EUROMAP_MACHINE_GONE` | The server no longer lists {name}. |
| `UA_EVENT_FILTER` | MinimumSeverity must be between 1 and 1000. |
| `UA_HISTORY_INTERVAL` | StartTime, EndTime or both must be set. |
| `UA_HISTORY_TIMES` | At least one requested time is required. |
| `UA_HISTORY_TIMESTAMPS` | TimestampsToReturn {(uint)timestampsToReturn} is not valid for HistoryRead. |
| `UA_IDENTITY_TOKEN_UNAVAILABLE` | Server endpoint does not advertise a {tokenType} user identity token. |
| `UA_MACHINETOOL_NO_MACHINERY` | The server does not implement OPC UA for Machinery (no http://opcfoundation.org/UA/Machinery/ namespace). |
| `UA_MACHINETOOL_TYPE` | '{machine}' is not a MachineToolType (OPC 40501-1) machine. |
| `UA_MANAGED_MONITORED_ITEM_STATUS` | Server rejected one or more managed monitored items. |
| `UA_MANAGED_SUBSCRIPTION_ITEMS` | A managed subscription requires at least one monitored item. |
| `UA_MAX_MESSAGE_SIZE` | MaxMessageSize must be zero (unlimited) or at least ReceiveBufferSize. |
| `UA_MONITORED_ITEM_OPTIONS` | MonitoringMode must be valid and QueueSize must be positive. |
| `UA_MONITORED_ITEMS` | A non-zero SubscriptionId and at least one monitored item are required. |
| `UA_MONITORING_MODE_ITEMS` | Monitored item identifiers must be non-zero. |
| `UA_MONITORING_MODE_OPTIONS` | SubscriptionId, MonitoringMode and monitored item identifiers must be valid. |
| `UA_MSG_EXPECTED` | Server did not return an OPC UA SecureConversation MSG response. |
| `UA_NO_TYPES` | No UADataType Definition nodes were found. |
| `UA_NODECLASS_MASK` | Browse NodeClassMask 0x{(uint)item.NodeClassMask:X} contains unsupported bits. |
| `UA_OPEN_SECURE_CHANNEL` | Server rejected OpenSecureChannel. |
| `UA_OPN_EXPECTED` | Server did not return an OpenSecureChannel response. |
| `UA_PROTOCOL_VERSION` | Server selected unsupported UACP protocol version {version}. |
| `UA_PUBLISH_ACK_OPTIONS` | Publish acknowledgements require non-zero SubscriptionId and SequenceNumber. |
| `UA_PUBLISH_SUBSCRIPTION_ROUTE` | Publish response for SubscriptionId {response.SubscriptionId} could not be routed from runtime {SubscriptionId}. |
| `UA_READ_DESTINATION` | Destination is smaller than NodesToRead. |
| `UA_REPUBLISH_OPTIONS` | SubscriptionId and retransmit SequenceNumber must be non-zero. |
| `UA_REPUBLISH_SEQUENCE` | Republish returned sequence {replay.Value.SequenceNumber}, expected {_nextSequenceNumber}. |
| `UA_REQUEST_ID_DUPLICATE` | OPC UA RequestId {requestId} is already pending. |
| `UA_REQUEST_TIMEOUT` | RequestTimeout and PublishRequestTimeout must both be positive. |
| `UA_RUNTIME_EMPTY` | No runtime DataType definitions were supplied. |
| `UA_RUNTIME_FIELD` | Runtime type '{source.Name}' contains an unnamed field or DataType. |
| `UA_SCHEMA_ARRAY` | Field '{fieldName}' array metadata is invalid: {ex.Message} |
| `UA_SCHEMA_DATATYPE_UNRESOLVED` | DataType {dataType} was referenced by a StructureField but was neither built-in nor discovered as a custom DataType. |
| `UA_SCHEMA_DEFINITION_KIND` | Unsupported DataTypeDefinition kind for {discovered.BrowseName}. |
| `UA_SCHEMA_ENUM_EMPTY` | Enumeration {discovered.BrowseName} has no fields. |
| `UA_SCHEMA_LIMIT` | MaximumTypes must be positive. |
| `UA_SCHEMA_NO_CUSTOM_TYPES` | All supplied roots are built-in OPC UA DataTypes; no custom DataTypeDefinition document was produced. |
| `UA_SCHEMA_ROOTS` | At least one OPC UA DataType NodeId is required for discovery. |
| `UA_SCHEMA_SOURCE` | Schema discovery SourceName cannot be empty. |
| `UA_SCHEMA_STRUCTURE_EMPTY` | Structure {discovered.BrowseName} has no fields. |
| `UA_SCHEMA_TYPE_LIMIT` | Schema discovery exceeded MaximumTypes={options.MaximumTypes}. |
| `UA_SCHEMA_VALUE_RANK` | Field '{fieldName}' has unsupported ValueRank {valueRank}. |
| `UA_SECURITY_MODE` | Unknown OPC UA MessageSecurityMode value {(uint)SecurityMode}. |
| `UA_SECURITY_POLICY` | SecurityPolicyUri '{SecurityPolicyUri}' is not supported. |
| `UA_SECURITY_POLICY_MODE` | MessageSecurityMode=None requires SecurityPolicy#None. |
| `UA_SEQUENCE_GAP_LIMIT` | Publish sequence gap exceeded configured maximum {_maximumRepublishGap}. |
| `UA_SERVER_CERT_CHAIN` | The server certificate chain is not trusted: {statuses}. |
| `UA_SERVER_CERT_REQUIRED` | Secure OpenSecureChannel requires the endpoint server certificate returned by GetEndpoints or exactly one trusted peer certificate. |
| `UA_SERVER_CERT_RSA` | The server application certificate must use an RSA key. |
| `UA_SERVER_CERT_UNTRUSTED` | The server certificate is not in the trusted peer list and no trusted roots were configured. |
| `UA_SERVICE_CANCELLED` | OPC UA service was cancelled by the caller. |
| `UA_SERVICE_TIMEOUT` | OPC UA service timed out. |
| `UA_SESSION_NOT_ACTIVE` | An activated Session is required for {service}. |
| `UA_SESSION_REQUIRED` | CreateSession must succeed before ActivateSession. |
| `UA_SESSION_TIMEOUT` | Requested session timeout must be a finite positive number of milliseconds. |
| `UA_SOURCE_EMPTY` | NodeSet2 source is empty. |
| `UA_SUBSCRIPTION_ENDED` | The event subscription ended. |
| `UA_SUBSCRIPTION_ID` | SubscriptionId must be non-zero. |
| `UA_SUBSCRIPTION_OPTIONS` | Publishing interval, lifetime count and keep-alive count must be positive. |
| `UA_SUBSCRIPTION_ROUTE_COLLISION` | Recreated SubscriptionId {recreated.Value.SubscriptionId} is already owned by another runtime. |
| `UA_SUBSCRIPTION_RUNTIME_OPTIONS` | NotificationBufferCapacity, MaximumRepublishGap and MaximumReconnectAttempts must be positive; ReconnectDelay cannot be negative. |
| `UA_WRITE_STATUS` | Server rejected Write item {i} ({items.Span[i].Descriptor}). |
