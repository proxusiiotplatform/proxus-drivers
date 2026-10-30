using System.Formats.Asn1;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Proxus.Drivers.OpcUa;

public sealed record OpcUaApplicationIdentity(X509Certificate2 Certificate);

public sealed record OpcUaCertificateTrustOptions
{
    public X509Certificate2[] TrustedPeerCertificates { get; init; } = [];
    public X509Certificate2[] TrustedRootCertificates { get; init; } = [];
    public X509RevocationMode RevocationMode { get; init; } = X509RevocationMode.NoCheck;
    public bool RequireHostName { get; init; } = true;
    public string? ExpectedApplicationUri { get; init; }
}

internal static class OpcUaCertificateValidator
{
    private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";
    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";
    private const string SubjectAlternativeNameOid = "2.5.29.17";

    internal static DriverResult ValidateConfiguredIdentity(OpcUaTcpOptions options)
    {
        if (options.ApplicationIdentity is null)
            return DriverResult.Success();
        if (!OpcUaSecurityPolicies.TryResolve(options.SecurityPolicyUri, out var policy) || policy is null)
            return Failure("UA_SECURITY_POLICY",
                $"SecurityPolicyUri '{options.SecurityPolicyUri}' does not define application certificate rules.");
        return ValidateApplicationIdentity(options.ApplicationIdentity, options.ApplicationUri, policy);
    }

    internal static bool HasConfiguredTrust(OpcUaTcpOptions options)
    {
        return options.CertificateTrust is { } trust &&
               (trust.TrustedPeerCertificates.Length != 0 || trust.TrustedRootCertificates.Length != 0);
    }

    internal static DriverResult ValidateApplicationIdentity(
        OpcUaApplicationIdentity? identity,
        string applicationUri,
        OpcUaSecurityPolicyProfile policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (identity?.Certificate is not { } certificate)
            return Failure("UA_CLIENT_CERT_REQUIRED",
                "A client application certificate with its private key is required.");
        if (!certificate.HasPrivateKey)
            return Failure("UA_CLIENT_CERT_PRIVATE_KEY",
                "The client application certificate does not have a private key.");
        using var privateKey = certificate.GetRSAPrivateKey();
        if (privateKey is null)
            return Failure("UA_CLIENT_CERT_RSA", "The client application certificate must use an RSA key.");
        var common = ValidateCommon(certificate, applicationUri, null, false, ClientAuthenticationOid);
        if (!common.IsSuccess) return common;
        return ValidateRsaSize(privateKey.KeySize, "client", policy);
    }

    internal static DriverResult ValidateServerCertificate(
        X509Certificate2 certificate,
        string endpointHost,
        OpcUaCertificateTrustOptions options,
        OpcUaSecurityPolicyProfile policy)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(policy);
        var common = ValidateCommon(certificate, options.ExpectedApplicationUri, endpointHost, options.RequireHostName,
            ServerAuthenticationOid);
        if (!common.IsSuccess) return common;
        using var publicKey = certificate.GetRSAPublicKey();
        if (publicKey is null)
            return Failure("UA_SERVER_CERT_RSA", "The server application certificate must use an RSA key.");
        var keySize = ValidateRsaSize(publicKey.KeySize, "server", policy);
        if (!keySize.IsSuccess) return keySize;

        if (options.TrustedPeerCertificates.Any(trusted => SameCertificate(certificate, trusted)))
            return DriverResult.Success();
        if (options.TrustedRootCertificates.Length == 0)
            return Failure("UA_SERVER_CERT_UNTRUSTED",
                "The server certificate is not in the trusted peer list and no trusted roots were configured.");

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = options.RevocationMode;
        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        foreach (var root in options.TrustedRootCertificates) chain.ChainPolicy.CustomTrustStore.Add(root);
        foreach (var peer in options.TrustedPeerCertificates) chain.ChainPolicy.ExtraStore.Add(peer);
        if (chain.Build(certificate)) return DriverResult.Success();
        var statuses = string.Join(", ", chain.ChainStatus.Select(static status => status.Status.ToString()));
        return Failure("UA_SERVER_CERT_CHAIN", $"The server certificate chain is not trusted: {statuses}.");
    }

    private static DriverResult ValidateCommon(
        X509Certificate2 certificate,
        string? expectedApplicationUri,
        string? endpointHost,
        bool requireHostName,
        string requiredExtendedKeyUsage)
    {
        var now = DateTime.UtcNow;
        if (now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
            return Failure("UA_CERT_TIME_INVALID",
                $"Certificate validity is {certificate.NotBefore:u} through {certificate.NotAfter:u}.");

        var keyUsage = certificate.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
        if (keyUsage is not null)
        {
            const X509KeyUsageFlags required = X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment;
            if ((keyUsage.KeyUsages & required) != required)
                return Failure("UA_CERT_KEY_USAGE",
                    "The certificate KeyUsage must allow DigitalSignature and KeyEncipherment.");
        }

        var enhancedKeyUsage = certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();
        if (enhancedKeyUsage is not null && !enhancedKeyUsage.EnhancedKeyUsages.Cast<Oid>()
                .Any(oid => oid.Value == requiredExtendedKeyUsage))
            return Failure("UA_CERT_EXTENDED_KEY_USAGE",
                $"The certificate ExtendedKeyUsage does not contain {requiredExtendedKeyUsage}.");

        var names = ReadSubjectAlternativeNames(certificate);
        if (!string.IsNullOrWhiteSpace(expectedApplicationUri) &&
            !names.Uris.Contains(expectedApplicationUri, StringComparer.Ordinal))
            return Failure("UA_CERT_APPLICATION_URI",
                $"Certificate SubjectAltName does not contain ApplicationUri '{expectedApplicationUri}'.");
        if (requireHostName && !string.IsNullOrWhiteSpace(endpointHost) && !MatchesEndpointHost(endpointHost, names))
            return Failure("UA_CERT_HOSTNAME",
                $"Certificate SubjectAltName does not match endpoint host '{endpointHost}'.");
        return DriverResult.Success();
    }

    /// <summary>Checks an RSA key against the configured policy's key-size range.</summary>
    private static DriverResult ValidateRsaSize(int keySize, string role, OpcUaSecurityPolicyProfile policy)
    {
        return keySize >= policy.MinimumRsaKeySize && keySize <= policy.MaximumRsaKeySize
            ? DriverResult.Success()
            : Failure("UA_CERT_RSA_SIZE",
                $"The {role} RSA key is {keySize} bits; {policy.Uri} requires {policy.MinimumRsaKeySize} through {policy.MaximumRsaKeySize} bits.");
    }

    private static bool MatchesEndpointHost(string endpointHost, SubjectAlternativeNames names)
    {
        if (IPAddress.TryParse(endpointHost, out var address))
            return names.IpAddresses.Any(candidate => candidate.Equals(address));
        return names.DnsNames.Any(candidate => MatchesDnsName(endpointHost, candidate));
    }

    private static bool MatchesDnsName(string host, string certificateName)
    {
        host = host.TrimEnd('.');
        certificateName = certificateName.TrimEnd('.');
        if (string.Equals(host, certificateName, StringComparison.OrdinalIgnoreCase)) return true;
        if (!certificateName.StartsWith("*.", StringComparison.Ordinal) ||
            certificateName.IndexOf('*', 1) >= 0) return false;
        var suffix = certificateName[1..];
        if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return false;
        return host.Length > suffix.Length && host.AsSpan(0, host.Length - suffix.Length).IndexOf('.') < 0;
    }

    private static SubjectAlternativeNames ReadSubjectAlternativeNames(X509Certificate2 certificate)
    {
        var result = new SubjectAlternativeNames([], [], []);
        var extension = certificate.Extensions[SubjectAlternativeNameOid];
        if (extension is null) return result;
        try
        {
            var reader = new AsnReader(extension.RawData, AsnEncodingRules.DER);
            var sequence = reader.ReadSequence();
            while (sequence.HasData)
            {
                var tag = sequence.PeekTag();
                if (tag.TagClass != TagClass.ContextSpecific)
                {
                    _ = sequence.ReadEncodedValue();
                    continue;
                }

                switch (tag.TagValue)
                {
                    case 2:
                        result.DnsNames.Add(sequence.ReadCharacterString(UniversalTagNumber.IA5String, tag));
                        break;
                    case 6:
                        result.Uris.Add(sequence.ReadCharacterString(UniversalTagNumber.IA5String, tag));
                        break;
                    case 7:
                        result.IpAddresses.Add(new IPAddress(sequence.ReadOctetString(tag)));
                        break;
                    default:
                        _ = sequence.ReadEncodedValue();
                        break;
                }
            }

            reader.ThrowIfNotEmpty();
            return result;
        }
        catch (AsnContentException ex)
        {
            throw new CryptographicException("Certificate SubjectAltName is malformed.", ex);
        }
    }

    private static bool SameCertificate(X509Certificate2 left, X509Certificate2 right)
    {
        Span<byte> leftHash = stackalloc byte[32];
        Span<byte> rightHash = stackalloc byte[32];
        SHA256.HashData(left.RawDataMemory.Span, leftHash);
        SHA256.HashData(right.RawDataMemory.Span, rightHash);
        return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }

    private static DriverResult Failure(string code, string message)
    {
        return DriverResult.Failure(new DriverError(
            DriverErrorKind.Configuration,
            code,
            message,
            Operation: DriverOperation.Validate,
            Protocol: OpcUaProtocol.Name,
            Cause: "An OPC UA application certificate or trust requirement was not satisfied.",
            SuggestedAction:
            "Use a currently valid RSA application certificate with matching URI/host identity and configure an explicit peer or root trust list.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged));
    }

    private sealed record SubjectAlternativeNames(
        List<string> DnsNames,
        List<string> Uris,
        List<IPAddress> IpAddresses);
}
