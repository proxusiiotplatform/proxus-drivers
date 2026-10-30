namespace Proxus.Drivers.OpcUa;

internal static class OpcUaProtocol
{
    public const string Name = "OPC UA Binary/TCP";
    public const string SecurityPolicyNone = OpcUaSecurityPolicyUris.None;
    public const string SecurityPolicyAes256Sha256RsaPss = OpcUaSecurityPolicies.Aes256Sha256RsaPssUri;
    public const string SecurityPolicyBasic256Sha256 = OpcUaSecurityPolicies.Basic256Sha256Uri;
    public const string SecurityPolicyAes128Sha256RsaOaep = OpcUaSecurityPolicies.Aes128Sha256RsaOaepUri;
    public const string RsaPssSha256SignatureAlgorithm = "http://opcfoundation.org/UA/security/rsa-pss-sha2-256";
    public const string RsaSha256SignatureAlgorithm = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    public const string RsaOaepEncryptionAlgorithm = "http://www.w3.org/2001/04/xmlenc#rsa-oaep";

    public const uint ProtocolVersion = 0;
    public const uint MinimumBufferSize = 8192;

    // Namespace 0 Default Binary encoding NodeIds from the OPC UA standard NodeSet.
    public const uint OpenSecureChannelRequestBinary = 446;
    public const uint OpenSecureChannelResponseBinary = 449;
    public const uint CloseSecureChannelRequestBinary = 452;
    public const uint GetEndpointsRequestBinary = 428;
    public const uint GetEndpointsResponseBinary = 431;
    public const uint CreateSessionRequestBinary = 461;
    public const uint CreateSessionResponseBinary = 464;
    public const uint ActivateSessionRequestBinary = 467;
    public const uint ActivateSessionResponseBinary = 470;
    public const uint CloseSessionRequestBinary = 473;
    public const uint CloseSessionResponseBinary = 476;
    public const uint ReadRequestBinary = 631;
    public const uint ReadResponseBinary = 634;
    public const uint WriteRequestBinary = 673;
    public const uint WriteResponseBinary = 676;
    public const uint BrowseRequestBinary = 527;
    public const uint BrowseResponseBinary = 530;
    public const uint BrowseNextRequestBinary = 533;
    public const uint BrowseNextResponseBinary = 536;
    public const uint TranslateBrowsePathsToNodeIdsRequestBinary = 554;
    public const uint TranslateBrowsePathsToNodeIdsResponseBinary = 557;
    public const uint ReadRawModifiedDetailsBinary = 649;
    public const uint ReadAtTimeDetailsBinary = 655;
    public const uint HistoryDataBinary = 658;
    public const uint HistoryModifiedDataBinary = 11227;
    public const uint HistoryReadRequestBinary = 664;
    public const uint HistoryReadResponseBinary = 667;
    public const uint HierarchicalReferences = 33;
    public const uint CallRequestBinary = 712;
    public const uint CallResponseBinary = 715;
    public const uint EventFilterBinary = 727;
    public const uint EventFilterResultBinary = 736;
    public const uint ElementOperandBinary = 594;
    public const uint LiteralOperandBinary = 597;
    public const uint SimpleAttributeOperandBinary = 603;
    public const uint EventNotificationListBinary = 916;
    public const uint BaseEventType = 2041;
    public const uint ServerObject = 2253;
    public const uint ConditionType = 2782;
    public const uint ConditionRefreshMethod = 3875;
    public const uint AcknowledgeMethod = 9111;
    public const uint CreateMonitoredItemsRequestBinary = 751;
    public const uint CreateMonitoredItemsResponseBinary = 754;
    public const uint ModifyMonitoredItemsRequestBinary = 757;
    public const uint ModifyMonitoredItemsResponseBinary = 760;
    public const uint SetMonitoringModeRequestBinary = 769;
    public const uint SetMonitoringModeResponseBinary = 772;
    public const uint DeleteMonitoredItemsRequestBinary = 781;
    public const uint DeleteMonitoredItemsResponseBinary = 784;
    public const uint CreateSubscriptionRequestBinary = 787;
    public const uint CreateSubscriptionResponseBinary = 790;
    public const uint PublishRequestBinary = 826;
    public const uint PublishResponseBinary = 829;
    public const uint RepublishRequestBinary = 832;
    public const uint RepublishResponseBinary = 835;
    public const uint DeleteSubscriptionsRequestBinary = 847;
    public const uint DeleteSubscriptionsResponseBinary = 850;
    public const uint DataChangeNotificationBinary = 811;
    public const uint StructureDefinitionBinary = 122;
    public const uint EnumDefinitionBinary = 123;
    public const uint AnonymousIdentityTokenBinary = 321;
    public const uint UserNameIdentityTokenBinary = 324;
    public const uint X509IdentityTokenBinary = 327;

    public const string RsaOaepSha256EncryptionAlgorithm =
        "http://opcfoundation.org/UA/security/rsa-oaep-sha2-256";

    public const uint ServiceFaultBinary = 397;

    public const uint AttributeValue = 13;
}

public enum OpcUaMessageSecurityMode : uint
{
    None = 1,
    Sign = 2,
    SignAndEncrypt = 3
}

public enum OpcUaSecurityTokenRequestType : uint
{
    Issue = 0,
    Renew = 1
}

public enum OpcUaAttributeId : uint
{
    NodeId = 1,
    NodeClass = 2,
    BrowseName = 3,
    DisplayName = 4,
    Description = 5,
    WriteMask = 6,
    UserWriteMask = 7,
    IsAbstract = 8,
    Symmetric = 9,
    InverseName = 10,
    ContainsNoLoops = 11,
    EventNotifier = 12,
    Value = 13,
    DataType = 14,
    ValueRank = 15,
    ArrayDimensions = 16,
    AccessLevel = 17,
    UserAccessLevel = 18,
    MinimumSamplingInterval = 19,
    Historizing = 20,
    Executable = 21,
    UserExecutable = 22,
    DataTypeDefinition = 23,
    RolePermissions = 24,
    UserRolePermissions = 25,
    AccessRestrictions = 26,
    AccessLevelEx = 27
}

public readonly record struct OpcUaConnectionLimits(
    uint ReceiveBufferSize,
    uint SendBufferSize,
    uint MaxMessageSize,
    uint MaxChunkCount);

public readonly record struct OpcUaSecureChannelInfo(
    uint ChannelId,
    uint TokenId,
    DateTime CreatedAtUtc,
    uint RevisedLifetimeMilliseconds,
    uint ServerProtocolVersion);
