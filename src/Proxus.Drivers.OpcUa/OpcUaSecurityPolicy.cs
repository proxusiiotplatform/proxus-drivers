using System.Security.Cryptography;

namespace Proxus.Drivers.OpcUa;

/// <summary>
///     SecurityPolicy URIs (OPC 10000-7) for <see cref="OpcUaTcpOptions.SecurityPolicyUri" /> and for matching the
///     <see cref="OpcUaEndpointDescription.SecurityPolicyUri" /> a server advertises.
/// </summary>
public static class OpcUaSecurityPolicyUris
{
    /// <summary>No signing or encryption; requires <see cref="OpcUaMessageSecurityMode.None" />.</summary>
    public const string None = "http://opcfoundation.org/UA/SecurityPolicy#None";

    /// <summary>Basic256Sha256: RSA PKCS#1 v1.5 SHA-256 signatures, RSA-OAEP (SHA-1), AES-256-CBC, HMAC-SHA256.</summary>
    public const string Basic256Sha256 = "http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256";

    /// <summary>Aes256_Sha256_RsaPss: RSA-PSS SHA-256 signatures, RSA-OAEP SHA-256, AES-256-CBC, HMAC-SHA256.</summary>
    public const string Aes256Sha256RsaPss = "http://opcfoundation.org/UA/SecurityPolicy#Aes256_Sha256_RsaPss";

    /// <summary>Aes128_Sha256_RsaOaep: RSA PKCS#1 v1.5 SHA-256 signatures, RSA-OAEP (SHA-1), AES-128-CBC, HMAC-SHA256.</summary>
    public const string Aes128Sha256RsaOaep = "http://opcfoundation.org/UA/SecurityPolicy#Aes128_Sha256_RsaOaep";
}

internal sealed record OpcUaSecurityPolicyProfile(
    string Uri,
    int NonceLength,
    int SymmetricSignatureKeyLength,
    int SymmetricEncryptionKeyLength,
    int SymmetricInitializationVectorLength,
    int SymmetricSignatureLength,
    int SymmetricBlockSize,
    int AsymmetricEncryptionHashLength,
    string AsymmetricSignatureAlgorithmUri,
    string AsymmetricEncryptionAlgorithmUri,
    RSASignaturePadding AsymmetricSignaturePadding,
    RSAEncryptionPadding AsymmetricEncryptionPadding,
    int MinimumRsaKeySize,
    int MaximumRsaKeySize);

internal static class OpcUaSecurityPolicies
{
    internal const string Basic256Sha256Uri = OpcUaSecurityPolicyUris.Basic256Sha256;
    internal const string Aes256Sha256RsaPssUri = OpcUaSecurityPolicyUris.Aes256Sha256RsaPss;
    internal const string Aes128Sha256RsaOaepUri = OpcUaSecurityPolicyUris.Aes128Sha256RsaOaep;

    internal static readonly OpcUaSecurityPolicyProfile Aes256Sha256RsaPss = new(
        Aes256Sha256RsaPssUri,
        32,
        32,
        32,
        16,
        32,
        16,
        32,
        OpcUaProtocol.RsaPssSha256SignatureAlgorithm,
        OpcUaProtocol.RsaOaepSha256EncryptionAlgorithm,
        RSASignaturePadding.Pss,
        RSAEncryptionPadding.OaepSHA256,
        2048,
        4096);

    internal static readonly OpcUaSecurityPolicyProfile Basic256Sha256 = new(
        Basic256Sha256Uri,
        32,
        32,
        32,
        16,
        32,
        16,
        20,
        OpcUaProtocol.RsaSha256SignatureAlgorithm,
        OpcUaProtocol.RsaOaepEncryptionAlgorithm,
        RSASignaturePadding.Pkcs1,
        RSAEncryptionPadding.OaepSHA1,
        2048,
        4096);

    internal static readonly OpcUaSecurityPolicyProfile Aes128Sha256RsaOaep = new(
        Aes128Sha256RsaOaepUri,
        32,
        32,
        16,
        16,
        32,
        16,
        20,
        OpcUaProtocol.RsaSha256SignatureAlgorithm,
        OpcUaProtocol.RsaOaepEncryptionAlgorithm,
        RSASignaturePadding.Pkcs1,
        RSAEncryptionPadding.OaepSHA1,
        2048,
        4096);

    internal static bool TryResolve(string uri, out OpcUaSecurityPolicyProfile? policy)
    {
        if (string.Equals(uri, Aes256Sha256RsaPssUri, StringComparison.Ordinal))
        {
            policy = Aes256Sha256RsaPss;
            return true;
        }

        if (string.Equals(uri, Basic256Sha256Uri, StringComparison.Ordinal))
        {
            policy = Basic256Sha256;
            return true;
        }

        if (string.Equals(uri, Aes128Sha256RsaOaepUri, StringComparison.Ordinal))
        {
            policy = Aes128Sha256RsaOaep;
            return true;
        }

        policy = null;
        return false;
    }
}
