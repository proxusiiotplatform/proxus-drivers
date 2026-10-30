using System.Reflection;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;

namespace Proxus.Drivers.OpcUa.SelfTests;

internal static class Program
{
    private static int _passed;
    private static int _total;

    public static async Task<int> Main()
    {
        await Run("NodeId round-trip", TestNodeIds);
        await Run("ExpandedNodeId round-trip", TestExpandedNodeId);
        await Run("Variant scalar/array round-trip", TestVariants);
        await Run("DataValue quality/timestamp round-trip", TestDataValue);
        await Run("StructureDefinition binary decode", TestStructureDefinition);
        await Run("EnumDefinition binary decode", TestEnumDefinition);
        await Run("Fragmented HEL/ACK + OpenSecureChannel", TestHandshake);
        await Run("Transport ERR maps structured error", TestTransportError);
        await Run("Invalid ACK buffer rejected", TestInvalidAck);
        await Run("Subscription services and concurrent Publish dispatch", TestSubscriptionServices);
        await Run("Concurrent requests get distinct RequestIds and ordered SequenceNumbers", TestConcurrentRequestOrdering);
        await Run("Pending service timeout closes socket", TestPendingServiceTimeout);
        await Run("Idle dispatcher does not expire connection", TestIdleDispatcher);
        await Run("Subscription duplicate gap and wrap matrix", TestSubscriptionSequenceMatrix);
        await Run("Subscription republish fault matrix", TestSubscriptionRepublishFaults);
        await Run("Subscription backpressure and acknowledgement", TestSubscriptionBackpressure);
        await Run("Managed subscription recovery retry matrix", TestManagedSubscriptionRecoveryMatrix);
        await Run("Multiple subscriptions route Publish responses", TestMultipleSubscriptionRouting);
        await Run("Subscription consumer cancellation and dispose", TestSubscriptionCancellationLifecycle);
        await Run("Aes256Sha256RsaPss policy profile", TestSecurityPolicyProfile);
        await Run("Basic256Sha256 crypto and message framing", TestBasic256Sha256);
        await Run("Aes128Sha256RsaOaep crypto and message framing", TestAes128Sha256RsaOaep);
        await Run("P_SHA256 independent vector", TestPHashSha256Vector);
        await Run("OPC UA directional key derivation", TestDirectionalKeyDerivation);
        await Run("RSA-PSS signature and tamper rejection", TestRsaPss);
        await Run("RSA-OAEP-SHA256 multi-block framing", TestRsaOaep);
        await Run("HMAC and AES symmetric primitives", TestSymmetricPrimitives);
        await Run("Application certificate validation", TestApplicationCertificateValidation);
        await Run("Server certificate peer trust", TestServerCertificatePeerTrust);
        await Run("Server certificate identity rejection", TestServerCertificateIdentityRejection);
        await Run("Server certificate root trust", TestServerCertificateRootTrust);
        await Run("Secure OPN asymmetric round-trip", TestSecureOpenRoundTrip);
        await Run("Secure OPN extended RSA padding", TestSecureOpenExtendedPadding);
        await Run("Secure OPN tamper and identity rejection", TestSecureOpenRejection);
        await Run("Secure MSG SignAndEncrypt round-trip", TestSecureMessageEncryptRoundTrip);
        await Run("Secure MSG Sign round-trip", TestSecureMessageSignRoundTrip);
        await Run("Secure MSG tamper rejection", TestSecureMessageTamperRejection);
        await Run("Secure MSG continuation envelope", TestSecureMessageContinuationEnvelope);
        await Run("Endpoint security and identity selection", TestEndpointSelection);
        await Run("Secure MSG accepts non-minimal padding", TestSecureMessageNonMinimalPadding);
        await Run("UserName token empty policy inherits endpoint policy", TestUserTokenPolicyInheritance);
        await Run("ActivateSession request failure is structured", TestActivateSessionRequestFailure);
        await Run("X509 user token signs with the token policy", TestX509UserTokenPolicy);
        await Run("X509 user token over a None channel is structured", TestX509UserTokenNoneChannel);
        await Run("Certificate and AES checks use the configured policy", TestPolicyAwareChecks);
        await Run("Lost SecureChannel tells Read and Write to reconnect", TestLostChannelDisposition);
        await Run("Session-invalidating ServiceFault tells caller to reconnect", TestSessionInvalidatingServiceFault);
        await Run("Lost SecureChannel: Session activated again, orphaned subscriptions deleted, fallback, opt-out", TestSessionReactivation);
        await Run("Pending request reports its own operation on channel close", TestPendingRequestOperationOnClose);
        await Run("Request built across a channel close stays structured", TestRequestBuiltAcrossChannelClose);
        await Run("Request racing a channel close stays structured", TestRequestRacingChannelClose);
        await Run("TranslateBrowsePathsToNodeIds encoding and per-path status", TestTranslateBrowsePaths);
        await Run("HistoryRead raw with continuation and release", TestHistoryReadRaw);
        await Run("HistoryRead request validation is structured", TestHistoryReadValidation);
        await Run("Call encoding, outputs and per-call status", TestMethodCall);
        await Run("Call with a lost response reports an unknown outcome", TestMethodCallOutcomeUnknown);
        await Run("Event monitored item filter encoding and select results", TestEventMonitoredItem);
        await Run("Event notifications decode from Publish", TestEventNotifications);
        await Run("Condition refresh and acknowledge encoding", TestConditionMethods);
        await Run("Golden corpus: recorded responses decode like Wireshark", GoldenCorpusTests.DecodeAsync);
        await Run("Golden corpus: recorded server response replay", GoldenCorpusTests.ReplayAsync);
        await Run("Golden corpus: fuzz reproducers stay structured and bounded", GoldenCorpusTests.FuzzReproducersAsync);
        await Run("UA Binary decoder hardening (array bounds, nesting, DiagnosticInfo)", GoldenCorpusTests.CodecHardeningAsync);
        Console.WriteLine($"OPC UA self-tests: {_passed}/{_total} PASS");
        return 0;
    }

    private static Task TestEndpointSelection()
    {
        var server = new OpcUaApplicationDescription(
            "urn:server", "urn:product", new IndustrialLocalizedText(null, "Server"),
            OpcUaApplicationType.Server, null, null, ["opc.tcp://server:4840"]);
        var anonymous = new OpcUaUserTokenPolicy("anonymous", OpcUaUserTokenType.Anonymous, null, null,
            OpcUaProtocol.SecurityPolicyNone);
        var userName = new OpcUaUserTokenPolicy("username", OpcUaUserTokenType.UserName, null, null,
            OpcUaProtocol.SecurityPolicyAes256Sha256RsaPss);
        var endpoints = new[]
        {
            new OpcUaEndpointDescription("opc.tcp://server:4840", server, [1],
                OpcUaMessageSecurityMode.Sign, OpcUaProtocol.SecurityPolicyAes256Sha256RsaPss,
                [anonymous, userName], null, 10),
            new OpcUaEndpointDescription("opc.tcp://server:4840", server, [1],
                OpcUaMessageSecurityMode.SignAndEncrypt, OpcUaProtocol.SecurityPolicyAes256Sha256RsaPss,
                [anonymous, userName], null, 20)
        };
        var selected = OpcUaEndpointSelector.Select(endpoints, OpcUaMessageSecurityMode.SignAndEncrypt,
            OpcUaProtocol.SecurityPolicyAes256Sha256RsaPss, OpcUaUserTokenType.UserName);
        True(selected.IsSuccess, "matching endpoint selected");
        Equal(OpcUaMessageSecurityMode.SignAndEncrypt, selected.Value.SecurityMode, "selected security mode");
        var missing = OpcUaEndpointSelector.Select(endpoints, OpcUaMessageSecurityMode.None,
            OpcUaProtocol.SecurityPolicyNone, OpcUaUserTokenType.Certificate);
        True(!missing.IsSuccess, "unsupported identity/security combination rejected");
        Equal("UA_ENDPOINT_NOT_FOUND", missing.Error.Code, "selection failure code");
        return Task.CompletedTask;
    }

    private static Task TestSecurityPolicyProfile()
    {
        True(OpcUaSecurityPolicies.TryResolve(OpcUaProtocol.SecurityPolicyAes256Sha256RsaPss, out var policy),
            "policy resolves");
        Equal(32, policy!.NonceLength, "nonce length");
        Equal(32, policy.SymmetricSignatureKeyLength, "signature key length");
        Equal(32, policy.SymmetricEncryptionKeyLength, "encryption key length");
        Equal(16, policy.SymmetricInitializationVectorLength, "IV length");
        Equal(2048, policy.MinimumRsaKeySize, "minimum RSA key size");
        Equal(4096, policy.MaximumRsaKeySize, "maximum RSA key size");
        True(!OpcUaSecurityPolicies.TryResolve(OpcUaProtocol.SecurityPolicyNone, out _),
            "None is not a secure policy profile");
        return Task.CompletedTask;
    }

    private static Task TestBasic256Sha256()
    {
        True(OpcUaSecurityPolicies.TryResolve(OpcUaProtocol.SecurityPolicyBasic256Sha256, out var resolved),
            "Basic256Sha256 resolves");
        var policy = resolved!;
        Equal(OpcUaProtocol.RsaSha256SignatureAlgorithm, policy.AsymmetricSignatureAlgorithmUri,
            "Basic256Sha256 signature algorithm");
        Equal(OpcUaProtocol.RsaOaepEncryptionAlgorithm, policy.AsymmetricEncryptionAlgorithmUri,
            "Basic256Sha256 encryption algorithm");

        using var rsa = RSA.Create(2048);
        var payload = Enumerable.Range(0, 700).Select(static value => (byte)(value * 31)).ToArray();
        var signature = OpcUaSecurityCrypto.SignRsaSha256(rsa, payload, policy);
        True(OpcUaSecurityCrypto.VerifyRsaSha256(rsa, payload, signature, policy),
            "Basic256Sha256 RSA-SHA256 signature");
        payload[0] ^= 1;
        True(!OpcUaSecurityCrypto.VerifyRsaSha256(rsa, payload, signature, policy),
            "Basic256Sha256 signature tamper rejection");
        payload[0] ^= 1;
        var ciphertext = OpcUaSecurityCrypto.EncryptRsa(rsa, payload, policy);
        var plaintext = OpcUaSecurityCrypto.DecryptRsa(rsa, ciphertext, policy);
        True(plaintext.AsSpan().SequenceEqual(payload), "Basic256Sha256 RSA-OAEP-SHA1 multi-block round-trip");

        using var clientCertificate =
            CreateApplicationCertificate("client.basic.test", "urn:proxus:test:basic-client", true);
        using var serverCertificate =
            CreateApplicationCertificate("server.basic.test", "urn:proxus:test:basic-server", false);
        var open = OpcUaAsymmetricOpenCodec.Encode(0, 17, 23, payload, clientCertificate, serverCertificate, policy);
        var decodedOpen = OpcUaAsymmetricOpenCodec.Decode(open, serverCertificate, clientCertificate, policy);
        True(decodedOpen.Body.AsSpan().SequenceEqual(payload), "Basic256Sha256 OPN round-trip");
        Equal(policy.Uri, decodedOpen.SecurityPolicyUri, "Basic256Sha256 OPN policy");

        var clientNonce = Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray();
        var serverNonce = Enumerable.Range(32, 32).Select(static value => (byte)value).ToArray();
        var (clientKeys, serverKeys) = OpcUaSecurityCrypto.DeriveKeys(clientNonce, serverNonce, policy);
        using (clientKeys)
        using (serverKeys)
        {
            var message = OpcUaSymmetricMessageCodec.Encode("MSGF"u8, 5, 7, 11, 13, payload,
                OpcUaMessageSecurityMode.SignAndEncrypt, clientKeys, policy);
            var decoded = OpcUaSymmetricMessageCodec.DecodeToUnsecuredEnvelope(message,
                OpcUaMessageSecurityMode.SignAndEncrypt, clientKeys, policy);
            True(decoded.AsSpan(24).SequenceEqual(payload), "Basic256Sha256 symmetric round-trip");
        }

        return Task.CompletedTask;
    }

    private static Task TestAes128Sha256RsaOaep()
    {
        True(OpcUaSecurityPolicies.TryResolve(OpcUaSecurityPolicyUris.Aes128Sha256RsaOaep, out var resolved),
            "Aes128Sha256RsaOaep resolves");
        var policy = resolved!;
        Equal(OpcUaProtocol.RsaSha256SignatureAlgorithm, policy.AsymmetricSignatureAlgorithmUri,
            "Aes128Sha256RsaOaep signature algorithm");
        Equal(OpcUaProtocol.RsaOaepEncryptionAlgorithm, policy.AsymmetricEncryptionAlgorithmUri,
            "Aes128Sha256RsaOaep encryption algorithm");
        Equal(16, policy.SymmetricEncryptionKeyLength, "Aes128Sha256RsaOaep AES-128 key length");
        Equal(32, policy.SymmetricSignatureKeyLength, "Aes128Sha256RsaOaep HMAC-SHA256 key length");

        // NIST SP 800-38A F.2.1 CBC-AES128.Encrypt, first two blocks.
        var key = Convert.FromHexString("2B7E151628AED2A6ABF7158809CF4F3C");
        var iv = Convert.FromHexString("000102030405060708090A0B0C0D0E0F");
        var block = Convert.FromHexString("6BC1BEE22E409F96E93D7E117393172AAE2D8A571E03AC9C9EB76FAC45AF8E51");
        var expected = Convert.FromHexString("7649ABAC8119B246CEE98E9B12E9197D5086CB9B507219EE95DB113A917678B2");
        True(OpcUaSecurityCrypto.EncryptAesCbc(key, iv, block, policy).AsSpan().SequenceEqual(expected),
            "AES-128-CBC matches the NIST SP 800-38A vector");
        True(OpcUaSecurityCrypto.DecryptAesCbc(key, iv, expected, policy).AsSpan().SequenceEqual(block),
            "AES-128-CBC NIST vector decrypts");
        Throws<CryptographicException>(() => OpcUaSecurityCrypto.EncryptAesCbc(new byte[32], iv, block, policy),
            "a 32-byte AES key is rejected for Aes128Sha256RsaOaep");

        var clientNonce = Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray();
        var serverNonce = Enumerable.Range(32, 32).Select(static value => (byte)value).ToArray();
        var (clientKeys, serverKeys) = OpcUaSecurityCrypto.DeriveKeys(clientNonce, serverNonce, policy);
        using (clientKeys)
        using (serverKeys)
        {
            // P_SHA256 is a stream, so the 64 bytes this policy needs are a prefix of the 80-byte vector the
            // directional key derivation test checks: 32-byte signing key, 16-byte AES key, 16-byte IV.
            var clientExpected =
                Convert.FromHexString(
                    "DD585DB0C102DD1A4C1ED4DD195606DEC3F7A1C789AFCA78F9479ED3A5D668AFCE49CB8F1C65A827F412C48E71C9F9CB3B5C2EE2FC2E4B3BD46D4098B5E45475");
            True(JoinKeySet(clientKeys).AsSpan().SequenceEqual(clientExpected),
                "Aes128Sha256RsaOaep key split: 32-byte signing key, 16-byte AES key, 16-byte IV");

            var payload = Enumerable.Range(0, 700).Select(static value => (byte)(value * 31)).ToArray();
            foreach (var mode in new[] { OpcUaMessageSecurityMode.Sign, OpcUaMessageSecurityMode.SignAndEncrypt })
            {
                var message = OpcUaSymmetricMessageCodec.Encode("MSGF"u8, 5, 7, 11, 13, payload, mode, clientKeys,
                    policy);
                var decoded = OpcUaSymmetricMessageCodec.DecodeToUnsecuredEnvelope(message, mode, clientKeys, policy);
                True(decoded.AsSpan(24).SequenceEqual(payload), $"Aes128Sha256RsaOaep {mode} symmetric round-trip");
                message[^1] ^= 1;
                Throws<CryptographicException>(
                    () => OpcUaSymmetricMessageCodec.DecodeToUnsecuredEnvelope(message, mode, clientKeys, policy),
                    $"Aes128Sha256RsaOaep {mode} tamper rejection");
            }

            using var clientCertificate =
                CreateApplicationCertificate("client.aes128.test", "urn:proxus:test:aes128-client", true);
            using var serverCertificate =
                CreateApplicationCertificate("server.aes128.test", "urn:proxus:test:aes128-server", false);
            var open = OpcUaAsymmetricOpenCodec.Encode(0, 17, 23, payload, clientCertificate, serverCertificate,
                policy);
            var decodedOpen = OpcUaAsymmetricOpenCodec.Decode(open, serverCertificate, clientCertificate, policy);
            True(decodedOpen.Body.AsSpan().SequenceEqual(payload), "Aes128Sha256RsaOaep OPN round-trip");
            Equal(policy.Uri, decodedOpen.SecurityPolicyUri, "Aes128Sha256RsaOaep OPN policy");
        }

        return Task.CompletedTask;
    }

    private static Task TestPHashSha256Vector()
    {
        var secret = Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray();
        var seed = Enumerable.Range(32, 32).Select(static value => (byte)value).ToArray();
        var actual = new byte[80];
        OpcUaSecurityCrypto.PHashSha256(secret, seed, actual);
        var expected =
            Convert.FromHexString(
                "B72593C43FEE5FAFA0256CD6BB904FF40C066A225DB95F66DD744E20858A2220DDF75067E3D76AC714C08E24EABD85FF425D7F5FB25E6E083B94B174E29DB89BC513E9172274D5ED54E52A3552901AE0");
        True(actual.AsSpan().SequenceEqual(expected), "P_SHA256 output matches independent Python HMAC vector");
        return Task.CompletedTask;
    }

    private static Task TestDirectionalKeyDerivation()
    {
        var clientNonce = Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray();
        var serverNonce = Enumerable.Range(32, 32).Select(static value => (byte)value).ToArray();
        var (client, server) = OpcUaSecurityCrypto.DeriveAes256Sha256RsaPssKeys(clientNonce, serverNonce);
        using (client)
        using (server)
        {
            var clientExpected =
                Convert.FromHexString(
                    "DD585DB0C102DD1A4C1ED4DD195606DEC3F7A1C789AFCA78F9479ED3A5D668AFCE49CB8F1C65A827F412C48E71C9F9CB3B5C2EE2FC2E4B3BD46D4098B5E45475A77832C6215B6E7AB85F2E668BE7AEFF");
            var serverExpected =
                Convert.FromHexString(
                    "B72593C43FEE5FAFA0256CD6BB904FF40C066A225DB95F66DD744E20858A2220DDF75067E3D76AC714C08E24EABD85FF425D7F5FB25E6E083B94B174E29DB89BC513E9172274D5ED54E52A3552901AE0");
            True(JoinKeySet(client).AsSpan().SequenceEqual(clientExpected),
                "client keys use ServerNonce as secret and ClientNonce as seed");
            True(JoinKeySet(server).AsSpan().SequenceEqual(serverExpected),
                "server keys use ClientNonce as secret and ServerNonce as seed");
            True(!client.SigningKey.AsSpan().SequenceEqual(server.SigningKey), "directional keys differ");
        }

        return Task.CompletedTask;
    }

    private static Task TestRsaPss()
    {
        using var rsa = RSA.Create(2048);
        var payload = "OPC UA OpenSecureChannel signed payload"u8.ToArray();
        var signature = OpcUaSecurityCrypto.SignRsaPssSha256(rsa, payload);
        True(OpcUaSecurityCrypto.VerifyRsaPssSha256(rsa, payload, signature), "valid RSA-PSS signature");
        payload[0] ^= 1;
        True(!OpcUaSecurityCrypto.VerifyRsaPssSha256(rsa, payload, signature), "tampered payload rejected");
        return Task.CompletedTask;
    }

    private static Task TestRsaOaep()
    {
        using var rsa = RSA.Create(2048);
        var plaintext = Enumerable.Range(0, 500).Select(static value => (byte)(value * 31)).ToArray();
        var encrypted = OpcUaSecurityCrypto.EncryptRsaOaepSha256(rsa, plaintext);
        Equal(3 * 256, encrypted.Length, "three RSA blocks");
        var decrypted = OpcUaSecurityCrypto.DecryptRsaOaepSha256(rsa, encrypted);
        True(decrypted.AsSpan().SequenceEqual(plaintext), "multi-block OAEP round-trip");
        Throws<CryptographicException>(() => OpcUaSecurityCrypto.DecryptRsaOaepSha256(rsa, encrypted.AsSpan(1)),
            "partial RSA block rejected");
        return Task.CompletedTask;
    }

    private static Task TestSymmetricPrimitives()
    {
        var key = Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray();
        var iv = Enumerable.Range(32, 16).Select(static value => (byte)value).ToArray();
        var plaintext = Enumerable.Range(0, 64).Select(static value => (byte)(255 - value)).ToArray();
        var mac = OpcUaSecurityCrypto.ComputeHmacSha256(key, plaintext);
        True(OpcUaSecurityCrypto.VerifyHmacSha256(key, plaintext, mac), "valid HMAC");
        plaintext[0] ^= 1;
        True(!OpcUaSecurityCrypto.VerifyHmacSha256(key, plaintext, mac), "tampered HMAC rejected");
        plaintext[0] ^= 1;
        var ciphertext = OpcUaSecurityCrypto.EncryptAesCbc(key, iv, plaintext,
            OpcUaSecurityPolicies.Aes256Sha256RsaPss);
        True(!ciphertext.AsSpan().SequenceEqual(plaintext), "ciphertext differs");
        var decrypted = OpcUaSecurityCrypto.DecryptAesCbc(key, iv, ciphertext,
            OpcUaSecurityPolicies.Aes256Sha256RsaPss);
        True(decrypted.AsSpan().SequenceEqual(plaintext), "AES-256-CBC round-trip");
        Throws<CryptographicException>(() => OpcUaSecurityCrypto.EncryptAesCbc(key, iv, plaintext.AsSpan(1),
            OpcUaSecurityPolicies.Aes256Sha256RsaPss),
            "partial AES block rejected");
        return Task.CompletedTask;
    }

    private static Task TestApplicationCertificateValidation()
    {
        using var certificate = CreateApplicationCertificate("client.proxus.test", "urn:proxus:test:client", true);
        var valid = OpcUaCertificateValidator.ValidateApplicationIdentity(new OpcUaApplicationIdentity(certificate),
            "urn:proxus:test:client",
            OpcUaSecurityPolicies.Aes256Sha256RsaPss);
        True(valid.IsSuccess, valid.IsSuccess ? "valid client certificate" : valid.Error.ToUserMessage());
        using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));
        var noPrivateKey =
            OpcUaCertificateValidator.ValidateApplicationIdentity(new OpcUaApplicationIdentity(publicOnly),
                "urn:proxus:test:client",
            OpcUaSecurityPolicies.Aes256Sha256RsaPss);
        True(!noPrivateKey.IsSuccess, "public-only client certificate rejected");
        Equal("UA_CLIENT_CERT_PRIVATE_KEY", noPrivateKey.Error.Code, "private-key error code");
        return Task.CompletedTask;
    }

    private static Task TestServerCertificatePeerTrust()
    {
        using var certificate = CreateApplicationCertificate("opcua.proxus.test", "urn:proxus:test:server", false);
        var trust = new OpcUaCertificateTrustOptions
        {
            TrustedPeerCertificates = [certificate],
            ExpectedApplicationUri = "urn:proxus:test:server"
        };
        var valid = OpcUaCertificateValidator.ValidateServerCertificate(certificate, "opcua.proxus.test", trust,
            OpcUaSecurityPolicies.Aes256Sha256RsaPss);
        True(valid.IsSuccess, valid.IsSuccess ? "direct peer trust" : valid.Error.ToUserMessage());
        return Task.CompletedTask;
    }

    private static Task TestServerCertificateIdentityRejection()
    {
        using var certificate = CreateApplicationCertificate("opcua.proxus.test", "urn:proxus:test:server", false);
        var wrongHost = OpcUaCertificateValidator.ValidateServerCertificate(certificate, "other.proxus.test",
            new OpcUaCertificateTrustOptions
            {
                TrustedPeerCertificates = [certificate],
                ExpectedApplicationUri = "urn:proxus:test:server"
            },
            OpcUaSecurityPolicies.Aes256Sha256RsaPss);
        True(!wrongHost.IsSuccess, "wrong hostname rejected");
        Equal("UA_CERT_HOSTNAME", wrongHost.Error.Code, "hostname error code");
        var wrongUri = OpcUaCertificateValidator.ValidateServerCertificate(certificate, "opcua.proxus.test",
            new OpcUaCertificateTrustOptions
            {
                TrustedPeerCertificates = [certificate],
                ExpectedApplicationUri = "urn:proxus:test:another-server"
            },
            OpcUaSecurityPolicies.Aes256Sha256RsaPss);
        True(!wrongUri.IsSuccess, "wrong ApplicationUri rejected");
        Equal("UA_CERT_APPLICATION_URI", wrongUri.Error.Code, "ApplicationUri error code");
        var untrusted = OpcUaCertificateValidator.ValidateServerCertificate(certificate, "opcua.proxus.test",
            new OpcUaCertificateTrustOptions
            {
                ExpectedApplicationUri = "urn:proxus:test:server"
            },
            OpcUaSecurityPolicies.Aes256Sha256RsaPss);
        True(!untrusted.IsSuccess, "empty trust list rejected");
        Equal("UA_SERVER_CERT_UNTRUSTED", untrusted.Error.Code, "trust error code");
        return Task.CompletedTask;
    }

    private static Task TestServerCertificateRootTrust()
    {
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest("CN=Proxus Test Root", rootKey, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        rootRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(rootRequest.PublicKey, false));
        using var root =
            rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        using var serverKey = RSA.Create(2048);
        var serverRequest =
            CreateCertificateRequest("opcua.chain.test", "urn:proxus:test:chain-server", serverKey, false);
        var serial = RandomNumberGenerator.GetBytes(16);
        using var issued = serverRequest.Create(root, DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow.AddDays(7), serial);
        using var server = issued.CopyWithPrivateKey(serverKey);
        var valid = OpcUaCertificateValidator.ValidateServerCertificate(server, "opcua.chain.test",
            new OpcUaCertificateTrustOptions
            {
                TrustedRootCertificates = [root],
                ExpectedApplicationUri = "urn:proxus:test:chain-server"
            },
            OpcUaSecurityPolicies.Aes256Sha256RsaPss);
        True(valid.IsSuccess, valid.IsSuccess ? "custom root trust" : valid.Error.ToUserMessage());
        using var client = CreateApplicationCertificate("client.chain.test", "urn:proxus:test:chain-client", true);
        var options = new OpcUaTcpOptions
        {
            EndpointUrl = new Uri("opc.tcp://opcua.chain.test:4840"),
            SecurityMode = OpcUaMessageSecurityMode.SignAndEncrypt,
            SecurityPolicyUri = OpcUaProtocol.SecurityPolicyAes256Sha256RsaPss,
            ApplicationUri = "urn:proxus:test:chain-client",
            ApplicationIdentity = new OpcUaApplicationIdentity(client),
            EndpointServerCertificate = server,
            CertificateTrust = new OpcUaCertificateTrustOptions
            {
                TrustedRootCertificates = [root],
                ExpectedApplicationUri = "urn:proxus:test:chain-server"
            }
        };
        var optionValidation = options.Validate();
        True(optionValidation.IsSuccess,
            optionValidation.IsSuccess
                ? "discovered leaf with root-only trust"
                : optionValidation.Error.ToUserMessage());
        return Task.CompletedTask;
    }

    private static Task TestSecureOpenRoundTrip()
    {
        using var client = CreateApplicationCertificate("client.opn.test", "urn:proxus:test:opn-client", true);
        using var server = CreateApplicationCertificate("server.opn.test", "urn:proxus:test:opn-server", false);
        var body = Enumerable.Range(0, 700).Select(static value => (byte)(value * 19)).ToArray();
        var request = OpcUaAsymmetricOpenCodec.Encode(0, 41, 77, body, client, server);
        var decodedRequest = OpcUaAsymmetricOpenCodec.Decode(request, server, client);
        Equal(0u, decodedRequest.ChannelId, "initial OPN channel id");
        Equal(41u, decodedRequest.SequenceNumber, "request sequence");
        Equal(77u, decodedRequest.RequestId, "request id");
        True(decodedRequest.Body.AsSpan().SequenceEqual(body), "request body");

        var response = OpcUaAsymmetricOpenCodec.Encode(500, 91, 77, body, server, client);
        var decodedResponse = OpcUaAsymmetricOpenCodec.Decode(response, client, server);
        Equal(500u, decodedResponse.ChannelId, "response channel id");
        Equal(91u, decodedResponse.SequenceNumber, "response sequence");
        Equal(77u, decodedResponse.RequestId, "response request id");
        True(decodedResponse.Body.AsSpan().SequenceEqual(body), "response body");
        var envelope = OpcUaAsymmetricOpenCodec.ToUnsecuredEnvelope(decodedResponse);
        True(envelope.AsSpan(0, 4).SequenceEqual("OPNF"u8), "normalized OPN envelope");
        return Task.CompletedTask;
    }

    private static Task TestSecureOpenExtendedPadding()
    {
        using var client =
            CreateApplicationCertificate("client.opn3072.test", "urn:proxus:test:opn3072-client", true, 3072);
        using var server =
            CreateApplicationCertificate("server.opn3072.test", "urn:proxus:test:opn3072-server", false, 3072);
        var body = Enumerable.Range(0, 1000).Select(static value => (byte)(value * 7)).ToArray();
        var message = OpcUaAsymmetricOpenCodec.Encode(123, 1, 2, body, client, server);
        var decoded = OpcUaAsymmetricOpenCodec.Decode(message, server, client);
        True(decoded.Body.AsSpan().SequenceEqual(body), "3072-bit extended-padding body");
        return Task.CompletedTask;
    }

    private static Task TestSecureOpenRejection()
    {
        using var client = CreateApplicationCertificate("client.reject.test", "urn:proxus:test:reject-client", true);
        using var server = CreateApplicationCertificate("server.reject.test", "urn:proxus:test:reject-server", false);
        using var otherServer =
            CreateApplicationCertificate("other.reject.test", "urn:proxus:test:other-server", false);
        var message = OpcUaAsymmetricOpenCodec.Encode(0, 1, 2, "secure-open"u8, client, server);
        Throws<CryptographicException>(() => OpcUaAsymmetricOpenCodec.Decode(message, server, otherServer),
            "unexpected sender rejected");
        message[^1] ^= 1;
        Throws<CryptographicException>(() => OpcUaAsymmetricOpenCodec.Decode(message, server, client),
            "tampered ciphertext rejected");
        return Task.CompletedTask;
    }

    private static Task TestSecureMessageEncryptRoundTrip()
    {
        var clientNonce = Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray();
        var serverNonce = Enumerable.Range(32, 32).Select(static value => (byte)value).ToArray();
        var (client, server) = OpcUaSecurityCrypto.DeriveAes256Sha256RsaPssKeys(clientNonce, serverNonce);
        using (client)
        using (server)
        {
            var body = Enumerable.Range(0, 700).Select(static value => (byte)(value * 23)).ToArray();
            var message = OpcUaSymmetricMessageCodec.Encode("MSGF"u8, 500, 700, 11, 19, body,
                OpcUaMessageSecurityMode.SignAndEncrypt, client);
            var decoded =
                OpcUaSymmetricMessageCodec.DecodeToUnsecuredEnvelope(message, OpcUaMessageSecurityMode.SignAndEncrypt,
                    client);
            Equal(500u, BinaryPrimitives.ReadUInt32LittleEndian(decoded.AsSpan(8, 4)), "MSG channel");
            Equal(700u, BinaryPrimitives.ReadUInt32LittleEndian(decoded.AsSpan(12, 4)), "MSG token");
            Equal(11u, BinaryPrimitives.ReadUInt32LittleEndian(decoded.AsSpan(16, 4)), "MSG sequence");
            Equal(19u, BinaryPrimitives.ReadUInt32LittleEndian(decoded.AsSpan(20, 4)), "MSG request");
            True(decoded.AsSpan(24).SequenceEqual(body), "MSG encrypted body");
        }

        return Task.CompletedTask;
    }

    private static Task TestSecureMessageSignRoundTrip()
    {
        var nonceA = RandomNumberGenerator.GetBytes(32);
        var nonceB = RandomNumberGenerator.GetBytes(32);
        var (client, server) = OpcUaSecurityCrypto.DeriveAes256Sha256RsaPssKeys(nonceA, nonceB);
        using (client)
        using (server)
        {
            var body = "signed-not-encrypted"u8.ToArray();
            var message =
                OpcUaSymmetricMessageCodec.Encode("MSGF"u8, 1, 2, 3, 4, body, OpcUaMessageSecurityMode.Sign, server);
            True(message.AsSpan(24, body.Length).SequenceEqual(body), "Sign mode preserves plaintext body");
            var decoded =
                OpcUaSymmetricMessageCodec.DecodeToUnsecuredEnvelope(message, OpcUaMessageSecurityMode.Sign, server);
            True(decoded.AsSpan(24).SequenceEqual(body), "Sign mode body");
        }

        return Task.CompletedTask;
    }

    private static Task TestSecureMessageTamperRejection()
    {
        var (client, server) =
            OpcUaSecurityCrypto.DeriveAes256Sha256RsaPssKeys(new byte[32], [.. Enumerable.Repeat((byte)1, 32)]);
        using (client)
        using (server)
        {
            var encrypted = OpcUaSymmetricMessageCodec.Encode("MSGF"u8, 1, 2, 3, 4, "payload"u8,
                OpcUaMessageSecurityMode.SignAndEncrypt, client);
            encrypted[^1] ^= 1;
            Throws<CryptographicException>(
                () => OpcUaSymmetricMessageCodec.DecodeToUnsecuredEnvelope(encrypted,
                    OpcUaMessageSecurityMode.SignAndEncrypt, client), "encrypted tamper rejected");
            var signed = OpcUaSymmetricMessageCodec.Encode("MSGF"u8, 1, 2, 3, 4, "payload"u8,
                OpcUaMessageSecurityMode.Sign, client);
            signed[24] ^= 1;
            Throws<CryptographicException>(
                () => OpcUaSymmetricMessageCodec.DecodeToUnsecuredEnvelope(signed, OpcUaMessageSecurityMode.Sign,
                    client), "signed tamper rejected");
        }

        return Task.CompletedTask;
    }

    private static Task TestSecureMessageNonMinimalPadding()
    {
        var (client, server) =
            OpcUaSecurityCrypto.DeriveAes256Sha256RsaPssKeys(new byte[32], [.. Enumerable.Repeat((byte)3, 32)]);
        using (client)
        using (server)
        {
            // 8-byte SequenceHeader + 7-byte body + 33 padding bytes + 32-byte HMAC = 80, a whole number of AES blocks.
            // Padding of 33 bytes exceeds one 16-byte block but is valid Part 6 padding.
            var body = "payload"u8.ToArray();
            var valid = BuildSignAndEncryptMessage(client, body, 33, corruptPadding: false);
            var decoded = OpcUaSymmetricMessageCodec.DecodeToUnsecuredEnvelope(valid,
                OpcUaMessageSecurityMode.SignAndEncrypt, client);
            True(decoded.AsSpan(24).SequenceEqual(body), "non-minimal padding removed");

            var inconsistent = BuildSignAndEncryptMessage(client, body, 33, corruptPadding: true);
            Throws<InvalidDataException>(() => OpcUaSymmetricMessageCodec.DecodeToUnsecuredEnvelope(inconsistent,
                OpcUaMessageSecurityMode.SignAndEncrypt, client), "inconsistent padding rejected");

            // A padding size byte claiming 256 bytes cannot fit inside an 80-byte plaintext.
            var oversized = BuildSignAndEncryptMessage(client, body, 33, corruptPadding: false, sizeByte: 0xFF);
            Throws<InvalidDataException>(() => OpcUaSymmetricMessageCodec.DecodeToUnsecuredEnvelope(oversized,
                OpcUaMessageSecurityMode.SignAndEncrypt, client), "padding beyond plaintext rejected");
        }

        return Task.CompletedTask;
    }

    private static byte[] BuildSignAndEncryptMessage(OpcUaSymmetricKeySet keys, byte[] body, int paddingLength,
        bool corruptPadding, byte? sizeByte = null)
    {
        using var plain = new UaBinaryWriter();
        plain.WriteUInt32(3);
        plain.WriteUInt32(4);
        plain.WriteBytes(body);
        for (var index = 0; index < paddingLength - 1; index++)
            plain.WriteByte(corruptPadding && index == 0 ? (byte)0 : (byte)(paddingLength - 1));
        plain.WriteByte(sizeByte ?? (byte)(paddingLength - 1));
        var totalLength = 16 + plain.Position + 32;
        using var signed = new UaBinaryWriter();
        signed.WriteBytes("MSGF"u8);
        signed.WriteUInt32((uint)totalLength);
        signed.WriteUInt32(1);
        signed.WriteUInt32(2);
        signed.WriteBytes(plain.WrittenSpan);
        var signature = OpcUaSecurityCrypto.ComputeHmacSha256(keys.SigningKey, signed.WrittenSpan);
        plain.WriteBytes(signature);
        var encrypted = OpcUaSecurityCrypto.EncryptAesCbc(keys.EncryptionKey, keys.InitializationVector,
            plain.WrittenSpan,
            OpcUaSecurityPolicies.Aes256Sha256RsaPss);
        return [.. signed.WrittenSpan[..16], .. encrypted];
    }

    private static Task TestUserTokenPolicyInheritance()
    {
        foreach (var empty in new[] { null, string.Empty })
        {
            var inherited = new OpcUaUserTokenPolicy("username", OpcUaUserTokenType.UserName, null, null, empty);
            Equal(OpcUaProtocol.SecurityPolicyBasic256Sha256,
                OpcUaTcpClient.ResolveUserTokenSecurityPolicyUri(inherited, OpcUaProtocol.SecurityPolicyBasic256Sha256),
                "empty token policy uses the endpoint policy");
        }

        var explicitPolicy = new OpcUaUserTokenPolicy("username", OpcUaUserTokenType.UserName, null, null,
            OpcUaProtocol.SecurityPolicyAes256Sha256RsaPss);
        Equal(OpcUaProtocol.SecurityPolicyAes256Sha256RsaPss,
            OpcUaTcpClient.ResolveUserTokenSecurityPolicyUri(explicitPolicy, OpcUaProtocol.SecurityPolicyBasic256Sha256),
            "explicit token policy wins");
        return Task.CompletedTask;
    }

    private static async Task TestActivateSessionRequestFailure()
    {
        await using var server = new SubscriptionServiceServer();
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        var connected = await client.ConnectAsync();
        True(connected.IsSuccess, connected.IsSuccess ? "connect" : connected.Error.ToUserMessage());
        // The endpoint inherits SecurityPolicy None, so a UserName token would put the password in plaintext
        // on an unencrypted channel; building the request must fail without throwing or sending anything.
        typeof(OpcUaTcpClient).GetProperty(nameof(OpcUaTcpClient.Session))!.SetValue(client,
            new OpcUaSessionInfo(IndustrialNodeId.Numeric(0, 9001), IndustrialNodeId.Numeric(0, 9002), 60_000, 65_536,
                "anonymous", false,
                [
                    new OpcUaUserTokenPolicy("anonymous", OpcUaUserTokenType.Anonymous, null, null, null),
                    new OpcUaUserTokenPolicy("username", OpcUaUserTokenType.UserName, null, null, null)
                ]));

        var failed = await client.ActivateUserNameSessionAsync("operator", "secret");
        True(!failed.IsSuccess, "plaintext UserName token over None channel rejected");
        Equal("UA_ACTIVATE_SESSION_REQUEST", failed.Error.Code, "structured request error");
        Equal(DriverConnectionDisposition.Healthy, failed.Error.Connection, "channel untouched");
        True(client.IsConnected, "client still connected");

        var activated = await client.ActivateAnonymousSessionAsync();
        True(activated.IsSuccess, activated.IsSuccess ? "anonymous activation" : activated.Error.ToUserMessage());
        var requests = server.Requests.ToArray();
        Equal(1, requests.Length, "only the anonymous ActivateSession reached the server");
        Equal(OpcUaProtocol.ActivateSessionRequestBinary, requests[0].TypeId, "ActivateSession request");
        Equal(server.OpenSequenceNumber + 1, requests[0].SequenceNumber, "no sequence-number gap");
    }

    private static Task TestX509UserTokenPolicy()
    {
        var basicToken = new OpcUaUserTokenPolicy("x509", OpcUaUserTokenType.Certificate, null, null,
            OpcUaProtocol.SecurityPolicyBasic256Sha256);
        var tokenPolicy = OpcUaTcpClient.ResolveUserTokenSignaturePolicy(basicToken,
            OpcUaProtocol.SecurityPolicyAes256Sha256RsaPss);
        Equal(OpcUaSecurityPolicies.Basic256Sha256Uri, tokenPolicy.Uri, "token policy wins over the channel policy");
        Equal(OpcUaProtocol.RsaSha256SignatureAlgorithm, tokenPolicy.AsymmetricSignatureAlgorithmUri,
            "Basic256Sha256 token signature algorithm");
        var inherited = OpcUaTcpClient.ResolveUserTokenSignaturePolicy(basicToken with { SecurityPolicyUri = null },
            OpcUaProtocol.SecurityPolicyAes256Sha256RsaPss);
        Equal(OpcUaSecurityPolicies.Aes256Sha256RsaPssUri, inherited.Uri, "empty token policy inherits the endpoint");
        Throws<NotSupportedException>(() => OpcUaTcpClient.ResolveUserTokenSignaturePolicy(
                basicToken with { SecurityPolicyUri = OpcUaProtocol.SecurityPolicyNone },
                OpcUaProtocol.SecurityPolicyAes256Sha256RsaPss),
            "SecurityPolicy None cannot sign an X509 token");

        using var user = CreateApplicationCertificate("user.proxus.test", "urn:proxus:test:user", true);
        var serverCertificate = RandomNumberGenerator.GetBytes(300);
        var serverNonce = RandomNumberGenerator.GetBytes(32);
        var (algorithm, signature) = OpcUaTcpClient.BuildUserTokenSignature(basicToken,
            OpcUaProtocol.SecurityPolicyAes256Sha256RsaPss, user, serverCertificate, serverNonce);
        Equal(OpcUaProtocol.RsaSha256SignatureAlgorithm, algorithm, "SignatureData algorithm follows the token policy");
        using var publicKey = user.GetRSAPublicKey()!;
        byte[] signedData = [.. serverCertificate, .. serverNonce];
        True(OpcUaSecurityCrypto.VerifyRsaSha256(publicKey, signedData, signature, OpcUaSecurityPolicies.Basic256Sha256),
            "signature verifies under the token policy (RSA PKCS#1 v1.5)");
        True(!OpcUaSecurityCrypto.VerifyRsaSha256(publicKey, signedData, signature,
                OpcUaSecurityPolicies.Aes256Sha256RsaPss),
            "signature is not an RSA-PSS channel-policy signature");
        return Task.CompletedTask;
    }

    private static async Task TestX509UserTokenNoneChannel()
    {
        await using var server = new SubscriptionServiceServer();
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        var connected = await client.ConnectAsync();
        True(connected.IsSuccess, connected.IsSuccess ? "connect" : connected.Error.ToUserMessage());
        typeof(OpcUaTcpClient).GetProperty(nameof(OpcUaTcpClient.Session))!.SetValue(client,
            new OpcUaSessionInfo(IndustrialNodeId.Numeric(0, 9001), IndustrialNodeId.Numeric(0, 9002), 60_000, 65_536,
                "anonymous", false,
                [
                    new OpcUaUserTokenPolicy("x509", OpcUaUserTokenType.Certificate, null, null,
                        OpcUaProtocol.SecurityPolicyBasic256Sha256)
                ]));
        using var user = CreateApplicationCertificate("user.proxus.test", "urn:proxus:test:user", true);
        var failed = await client.ActivateCertificateSessionAsync(user);
        True(!failed.IsSuccess, "X509 activation over a None channel rejected");
        Equal("UA_ACTIVATE_SESSION_REQUEST", failed.Error.Code, "structured request error");
        Equal(DriverErrorKind.NotSupported, failed.Error.Kind, "not-supported kind");
        Equal(DriverConnectionDisposition.Healthy, failed.Error.Connection, "channel untouched");
        Equal(0, server.Requests.Count, "nothing reached the server");
    }

    private static Task TestPolicyAwareChecks()
    {
        using var small = CreateApplicationCertificate("client.proxus.test", "urn:proxus:test:client", true, 1024);
        var rejected = OpcUaCertificateValidator.ValidateApplicationIdentity(new OpcUaApplicationIdentity(small),
            "urn:proxus:test:client", OpcUaSecurityPolicies.Basic256Sha256);
        True(!rejected.IsSuccess, "1024-bit key rejected");
        Equal("UA_CERT_RSA_SIZE", rejected.Error.Code, "RSA size error code");
        True(rejected.Error.Message.Contains(OpcUaSecurityPolicies.Basic256Sha256Uri, StringComparison.Ordinal),
            "the RSA size error names the configured policy: " + rejected.Error.Message);

        try
        {
            OpcUaSecurityCrypto.EncryptAesCbc(new byte[16], new byte[16], new byte[16],
                OpcUaSecurityPolicies.Basic256Sha256);
            throw new InvalidOperationException("short AES key accepted");
        }
        catch (CryptographicException ex)
        {
            True(ex.Message.Contains(OpcUaSecurityPolicies.Basic256Sha256Uri, StringComparison.Ordinal),
                "the AES error names the configured policy: " + ex.Message);
        }

        return Task.CompletedTask;
    }

    private static Task TestSecureMessageContinuationEnvelope()
    {
        var (client, server) =
            OpcUaSecurityCrypto.DeriveAes256Sha256RsaPssKeys(new byte[32], [.. Enumerable.Repeat((byte)2, 32)]);
        using (client)
        using (server)
        {
            var message = OpcUaSymmetricMessageCodec.Encode("MSGC"u8, 9, 10, 11, 12, "chunk"u8,
                OpcUaMessageSecurityMode.SignAndEncrypt, server);
            var decoded =
                OpcUaSymmetricMessageCodec.DecodeToUnsecuredEnvelope(message, OpcUaMessageSecurityMode.SignAndEncrypt,
                    server);
            True(decoded.AsSpan(0, 4).SequenceEqual("MSGC"u8), "continuation chunk type retained");
            True(decoded.AsSpan(24).SequenceEqual("chunk"u8), "continuation body retained");
        }

        return Task.CompletedTask;
    }

    private static X509Certificate2 CreateApplicationCertificate(string dnsName, string applicationUri, bool client,
        int keySize = 2048)
    {
        using var key = RSA.Create(keySize);
        var request = CreateCertificateRequest(dnsName, applicationUri, key, client);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(7));
    }

    private static CertificateRequest CreateCertificateRequest(string dnsName, string applicationUri, RSA key,
        bool client)
    {
        var request = new CertificateRequest($"CN={dnsName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        var usages = new OidCollection { new Oid(client ? "1.3.6.1.5.5.7.3.2" : "1.3.6.1.5.5.7.3.1") };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, true));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(dnsName);
        san.AddUri(new Uri(applicationUri));
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return request;
    }

    private static byte[] JoinKeySet(OpcUaSymmetricKeySet keys)
    {
        return [.. keys.SigningKey, .. keys.EncryptionKey, .. keys.InitializationVector];
    }

    private static void Throws<TException>(Action action, string message) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static Task TestNodeIds()
    {
        IndustrialNodeId[] ids =
        [
            IndustrialNodeId.Numeric(0, 42),
            IndustrialNodeId.Numeric(2, 500),
            IndustrialNodeId.Numeric(400, 70000),
            IndustrialNodeId.String(3, "Motor/Speed"),
            IndustrialNodeId.Guid(4, Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")),
            IndustrialNodeId.Opaque(5, [1, 2, 3, 4, 255])
        ];
        foreach (var id in ids)
        {
            using var writer = new UaBinaryWriter();
            writer.WriteNodeId(id);
            var reader = new UaBinaryReader(writer.WrittenSpan);
            Equal(id, reader.ReadNodeId(), id.ToString());
            Equal(0, reader.Remaining, "NodeId reader consumed payload");
        }

        return Task.CompletedTask;
    }

    private static Task TestExpandedNodeId()
    {
        var input = new IndustrialExpandedNodeId(IndustrialNodeId.String(2, "Pump"), "urn:factory:line1", 7);
        using var writer = new UaBinaryWriter();
        writer.WriteExpandedNodeId(input);
        var reader = new UaBinaryReader(writer.WrittenSpan);
        Equal(input, reader.ReadExpandedNodeId(), "ExpandedNodeId");
        return Task.CompletedTask;
    }

    private static Task TestVariants()
    {
        DriverValue[] scalars =
        [
            DriverValue.FromBoolean(true), DriverValue.FromInt8(-2), DriverValue.FromUInt8(250),
            DriverValue.FromInt16(-1234), DriverValue.FromUInt16(65000), DriverValue.FromInt32(-1234567),
            DriverValue.FromUInt32(4_000_000_000), DriverValue.FromInt64(-5_000_000_000),
            DriverValue.FromUInt64(10_000_000_000),
            DriverValue.FromFloat32(12.5f), DriverValue.FromFloat64(-0.125), DriverValue.FromString("Hot水"),
            DriverValue.FromGuid(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")),
            DriverValue.FromBytes([0, 1, 2, 254, 255]), DriverValue.FromXmlElement("<A>Hot水</A>"),
            DriverValue.FromNodeId(IndustrialNodeId.String(2, "X")), DriverValue.FromStatusCode(0x40000000),
            DriverValue.FromQualifiedName(new IndustrialQualifiedName(3, "Speed")),
            DriverValue.FromLocalizedText(new IndustrialLocalizedText("tr-TR", "Hız")),
            DriverValue.FromExtensionObject(new DriverExtensionObject(
                new IndustrialExpandedNodeId(IndustrialNodeId.Numeric(2, 5001)), DriverExtensionObjectEncoding.Binary,
                [1, 2, 3, 4]))
        ];
        foreach (var value in scalars)
        {
            using var writer = new UaBinaryWriter();
            OpcUaBinaryCodec.WriteVariant(writer, value);
            var reader = new UaBinaryReader(writer.WrittenSpan);
            var decoded = OpcUaBinaryCodec.ReadVariant(ref reader);
            Equal(value, decoded, $"Variant {value.Type}");
        }

        var array = DriverDynamicArray.Create(IndustrialDataType.Int32, [2, 3],
        [
            DriverValue.FromInt32(1), DriverValue.FromInt32(2), DriverValue.FromInt32(3), DriverValue.FromInt32(4),
            DriverValue.FromInt32(5), DriverValue.FromInt32(6)
        ]).Value;
        using (var writer = new UaBinaryWriter())
        {
            OpcUaBinaryCodec.WriteVariant(writer, DriverValue.FromArray(array));
            var reader = new UaBinaryReader(writer.WrittenSpan);
            var decoded = OpcUaBinaryCodec.ReadVariant(ref reader).AsArray();
            True(array.Equals(decoded), "2x3 array round-trip");
        }

        var empty = DriverDynamicArray.Create(IndustrialDataType.UInt16, [0], ReadOnlySpan<DriverValue>.Empty).Value;
        using (var writer = new UaBinaryWriter())
        {
            OpcUaBinaryCodec.WriteVariant(writer, DriverValue.FromArray(empty));
            var reader = new UaBinaryReader(writer.WrittenSpan);
            var decoded = OpcUaBinaryCodec.ReadVariant(ref reader).AsArray();
            Equal(0, decoded.Count, "empty array");
        }

        return Task.CompletedTask;
    }

    private static Task TestDataValue()
    {
        var source = new DateTime(2026, 8, 10, 12, 34, 56, DateTimeKind.Utc).AddTicks(1234);
        var server = source.AddMilliseconds(5);
        var sample = new DriverSample(DriverValue.FromFloat64(42.125),
            new DriverQuality(DriverQualityLevel.Uncertain, DriverQualityFlags.None, 0x40000000), source, server, 123,
            9999);
        using var writer = new UaBinaryWriter();
        OpcUaBinaryCodec.WriteDataValue(writer, sample);
        var reader = new UaBinaryReader(writer.WrittenSpan);
        var decoded = OpcUaBinaryCodec.ReadDataValue(ref reader);
        Equal(sample, decoded, "DataValue");
        return Task.CompletedTask;
    }

    private static Task TestStructureDefinition()
    {
        using var body = new UaBinaryWriter();
        body.WriteNodeId(IndustrialNodeId.Numeric(2, 5001));
        body.WriteNodeId(IndustrialNodeId.Numeric(0, 22)); // Structure
        body.WriteInt32((int)OpcUaStructureType.StructureWithOptionalFields);
        body.WriteInt32(3);

        body.WriteString("Speed");
        body.WriteLocalizedText(new IndustrialLocalizedText("en-US", "Speed"));
        body.WriteNodeId(IndustrialNodeId.Numeric(0, 11)); // Double
        body.WriteInt32(-1);
        body.WriteInt32(0);
        body.WriteUInt32(0);
        body.WriteBoolean(false);

        body.WriteString("Samples");
        body.WriteLocalizedText(new IndustrialLocalizedText("en-US", "Samples"));
        body.WriteNodeId(IndustrialNodeId.Numeric(0, 4)); // Int16
        body.WriteInt32(1);
        body.WriteInt32(1);
        body.WriteUInt32(8);
        body.WriteUInt32(0);
        body.WriteBoolean(false);

        body.WriteString("Label");
        body.WriteLocalizedText(new IndustrialLocalizedText("en-US", "Label"));
        body.WriteNodeId(IndustrialNodeId.Numeric(0, 12)); // String
        body.WriteInt32(-1);
        body.WriteInt32(0);
        body.WriteUInt32(64);
        body.WriteBoolean(true);

        var ext = new DriverExtensionObject(
            new IndustrialExpandedNodeId(IndustrialNodeId.Numeric(0, OpcUaProtocol.StructureDefinitionBinary)),
            DriverExtensionObjectEncoding.Binary,
            body.WrittenSpan);
        var decoded = OpcUaDataTypeDefinitionCodec.Decode(ext);
        True(decoded is OpcUaStructureDataTypeDefinition, "structure definition type");
        var definition = (OpcUaStructureDataTypeDefinition)decoded;
        Equal(OpcUaStructureType.StructureWithOptionalFields, definition.StructureType, "structure type");
        Equal(3, definition.Fields.Length, "structure field count");
        Equal("Speed", definition.Fields[0].Name, "first field");
        Equal(1, definition.Fields[1].ValueRank, "array rank");
        Equal((uint)8, definition.Fields[1].ArrayDimensions[0], "array dimension");
        True(definition.Fields[2].IsOptional, "optional field");
        Equal((uint)64, definition.Fields[2].MaxStringLength, "max string length");
        return Task.CompletedTask;
    }

    private static Task TestEnumDefinition()
    {
        using var body = new UaBinaryWriter();
        body.WriteInt32(3);
        WriteEnumField(body, 0, "Stopped");
        WriteEnumField(body, 1, "Running");
        WriteEnumField(body, 10, "Fault");
        var ext = new DriverExtensionObject(
            new IndustrialExpandedNodeId(IndustrialNodeId.Numeric(0, OpcUaProtocol.EnumDefinitionBinary)),
            DriverExtensionObjectEncoding.Binary,
            body.WrittenSpan);
        var decoded = OpcUaDataTypeDefinitionCodec.Decode(ext);
        True(decoded is OpcUaEnumDataTypeDefinition, "enum definition type");
        var definition = (OpcUaEnumDataTypeDefinition)decoded;
        Equal(3, definition.Fields.Length, "enum field count");
        Equal("Running", definition.Fields[1].Name, "enum name");
        Equal(1L, definition.Fields[1].Value, "enum value");
        Equal("Fault", definition.Fields[2].DisplayName.Text, "enum display name");
        return Task.CompletedTask;
    }

    private static void WriteEnumField(UaBinaryWriter writer, long value, string name)
    {
        writer.WriteInt64(value);
        writer.WriteLocalizedText(new IndustrialLocalizedText("en-US", name));
        writer.WriteLocalizedText(new IndustrialLocalizedText("en-US", $"{name} description"));
        writer.WriteString(name);
    }

    private static async Task TestHandshake()
    {
        await using var server = new HandshakeServer(ServerMode.Normal, true);
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        var result = await client.ConnectAsync();
        True(result.IsSuccess, result.IsSuccess ? "connect" : result.Error.ToUserMessage());
        True(client.IsConnected, "client connected");
        Equal((uint)65_536, client.NegotiatedLimits!.Value.ReceiveBufferSize, "negotiated receive");
        Equal((uint)100, client.SecureChannel!.Value.ChannelId, "channel id");
        Equal((uint)200, client.SecureChannel!.Value.TokenId, "token id");
    }

    private static async Task TestTransportError()
    {
        await using var server = new HandshakeServer(ServerMode.TransportError, false);
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        var result = await client.ConnectAsync();
        True(!result.IsSuccess, "ERR should fail");
        Equal("UA_TCP_ERROR", result.Error.Code, "ERR code");
        Equal(DriverOperation.Handshake, result.Error.Operation, "ERR operation");
    }

    private static async Task TestInvalidAck()
    {
        await using var server = new HandshakeServer(ServerMode.InvalidAckBuffer, false);
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        var result = await client.ConnectAsync();
        True(!result.IsSuccess, "invalid ACK should fail");
        Equal("UA_ACK_BUFFER", result.Error.Code, "ACK code");
    }

    private static async Task TestSubscriptionServices()
    {
        await using var server = new SubscriptionServiceServer();
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        var connected = await client.ConnectAsync();
        True(connected.IsSuccess, connected.IsSuccess ? "connect" : connected.Error.ToUserMessage());
        typeof(OpcUaTcpClient).GetProperty(nameof(OpcUaTcpClient.Session))!.SetValue(client,
            new OpcUaSessionInfo(IndustrialNodeId.Numeric(0, 9001), IndustrialNodeId.Numeric(0, 9002), 60_000, 65_536,
                "anonymous", true));

        var subscription = await client.CreateSubscriptionAsync(new OpcUaSubscriptionRequest());
        True(subscription.IsSuccess, subscription.IsSuccess ? "subscription" : subscription.Error.ToUserMessage());
        Equal(500u, subscription.Value.SubscriptionId, "subscription id");
        var created = await client.CreateMonitoredItemsAsync(500,
            new[] { new OpcUaMonitoredItemRequest(new OpcUaNodeDescriptor(IndustrialNodeId.Numeric(2, 7001)), 77) });
        True(created.IsSuccess, created.IsSuccess ? "monitored items" : created.Error.ToUserMessage());
        Equal(600u, created.Value[0].MonitoredItemId, "monitored item id");
        var modified =
            await client.ModifyMonitoredItemsAsync(500, new[] { new OpcUaMonitoredItemModification(600, 77) });
        True(modified.IsSuccess, modified.IsSuccess ? "modify" : modified.Error.ToUserMessage());
        var setMode = await client.SetMonitoringModeAsync(500, OpcUaMonitoringMode.Reporting, new uint[] { 600 });
        True(setMode.IsSuccess && setMode.Value[0] == 0, "set monitoring mode");

        var publishTask = client.PublishAsync(new[] { new OpcUaSubscriptionAcknowledgement(500, 99) }).AsTask();
        await server.PublishReceived.WaitAsync(TimeSpan.FromSeconds(2));
        var read = await client.ReadAsync(new OpcUaNodeDescriptor(IndustrialNodeId.Numeric(2, 7001)));
        True(read.IsSuccess && read.Value.Value.AsFloat64() == 12.5, "read while publish waits");
        server.ReleasePublish();
        var publish = await publishTask;
        True(publish.IsSuccess, publish.IsSuccess ? "publish" : publish.Error.ToUserMessage());
        True(!publish.Value.IsKeepAlive, "publish notification is not a keep-alive");
        Equal(1u, publish.Value.SequenceNumber, "publish sequence");
        Equal(77u, publish.Value.Values[0].ClientHandle, "publish client handle");
        Equal(42.5, publish.Value.Values[0].Sample.Value.AsFloat64(), "publish data value");
        Equal(0x807A0000u, publish.Value.AcknowledgementResults[0], "publish acknowledgement result");
        var republish = await client.RepublishAsync(500, 1);
        True(republish.IsSuccess && republish.Value.SequenceNumber == 1, "republish");
        var runtime = await client.StartSubscriptionPumpAsync(500,
            new OpcUaSubscriptionRuntimeOptions { NotificationBufferCapacity = 1 });
        True(runtime.IsSuccess, runtime.IsSuccess ? "subscription runtime" : runtime.Error.ToUserMessage());
        await using (var notifications = runtime.Value.ReadNotificationsAsync().GetAsyncEnumerator())
        {
            True(await notifications.MoveNextAsync(), "subscription runtime notification");
            Equal(1u, notifications.Current.SequenceNumber, "subscription runtime sequence");
        }

        await runtime.Value.DisposeAsync();
        var deletedItems = await client.DeleteMonitoredItemsAsync(500, new uint[] { 600 });
        True(deletedItems.IsSuccess && deletedItems.Value[0] == 0, "delete item");
        var deletedSubscriptions = await client.DeleteSubscriptionsAsync(new uint[] { 500 });
        True(deletedSubscriptions.IsSuccess && deletedSubscriptions.Value[0] == 0, "delete subscription");
        await Task.Delay(1_250);
        Equal(201u, client.SecureChannel!.Value.TokenId, "automatic secure channel renewal");
    }

    // Fifty Reads started at once on one connection must all succeed, and the server must see SequenceNumbers rise
    // in the order the messages arrive. Before the fix, RequestIds collided ("already pending") and SequenceNumbers
    // were assigned before the send gate, so they could reach the server out of order and the channel closed.
    private static async Task TestConcurrentRequestOrdering()
    {
        await using var server = new SubscriptionServiceServer();
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        var connected = await client.ConnectAsync();
        True(connected.IsSuccess, connected.IsSuccess ? "connect" : connected.Error.ToUserMessage());
        typeof(OpcUaTcpClient).GetProperty(nameof(OpcUaTcpClient.Session))!.SetValue(client,
            new OpcUaSessionInfo(IndustrialNodeId.Numeric(0, 9001), IndustrialNodeId.Numeric(0, 9002), 60_000, 65_536,
                "anonymous", true));
        while (server.Requests.TryDequeue(out _))
        {
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            return await client.ReadAsync(new OpcUaNodeDescriptor(IndustrialNodeId.Numeric(2, 7001)));
        })).ToArray();
        start.SetResult();
        var results = await Task.WhenAll(reads);

        var failed = results.Where(static r => !r.IsSuccess).Select(static r => r.Error.Code).ToArray();
        True(failed.Length == 0, $"concurrent reads failed: {string.Join(", ", failed.Distinct())}");
        True(results.All(static r => r.Value.Value.AsFloat64() == 12.5), "every concurrent read returned the value");
        var sequences = server.Requests.Select(static r => r.SequenceNumber).ToArray();
        Equal(50, sequences.Length, "requests seen by the server");
        for (var i = 1; i < sequences.Length; i++)
            True(sequences[i] == sequences[i - 1] + 1,
                $"SequenceNumbers out of order at arrival {i}: {sequences[i - 1]} then {sequences[i]}");
    }

    private static async Task TestPendingServiceTimeout()
    {
        await using var server = new SubscriptionServiceServer(true);
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        var connected = await client.ConnectAsync();
        True(connected.IsSuccess, "timeout test connect");
        typeof(OpcUaTcpClient).GetProperty(nameof(OpcUaTcpClient.Session))!.SetValue(client,
            new OpcUaSessionInfo(IndustrialNodeId.Numeric(0, 9001), IndustrialNodeId.Numeric(0, 9002), 60_000, 65_536,
                "anonymous", true));
        var read = await client.ReadAsync(new OpcUaNodeDescriptor(IndustrialNodeId.Numeric(2, 7001)));
        True(!read.IsSuccess, "read should time out");
        Equal("UA_SERVICE_TIMEOUT", read.Error.Code, "timeout error code");
        True(!client.IsConnected, "timeout closes the uncertain socket");
    }

    private static void SetActiveSession(OpcUaTcpClient client, bool withAnonymousPolicy = false, bool activated = true)
    {
        typeof(OpcUaTcpClient).GetProperty(nameof(OpcUaTcpClient.Session))!.SetValue(client,
            new OpcUaSessionInfo(IndustrialNodeId.Numeric(0, 9001), IndustrialNodeId.Numeric(0, 9002), 60_000, 65_536,
                "anonymous", activated,
                withAnonymousPolicy
                    ? [new OpcUaUserTokenPolicy("anonymous", OpcUaUserTokenType.Anonymous, null, null, OpcUaProtocol.SecurityPolicyNone)]
                    : null));
    }

    /// <summary>Closes the channel the way the receive loop does on a transport failure, without CloseSession.</summary>
    private static void DropChannel(OpcUaTcpClient client)
    {
        typeof(OpcUaTcpClient).GetMethod("CloseSocket", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(client, null);
    }

    private static void AssertReconnectAdvice(DriverError error, string code, DriverOperation operation, string label)
    {
        Equal(code, error.Code, $"{label} code");
        Equal(operation, error.Operation, $"{label} operation");
        Equal(DriverRetryDisposition.ReconnectAndRetry, error.Retry, $"{label} retry");
        Equal(DriverConnectionDisposition.Broken, error.Connection, $"{label} connection");
    }

    private static async Task TestLostChannelDisposition()
    {
        await using var server = new SubscriptionServiceServer { CloseOnRead = true };
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        var connected = await client.ConnectAsync();
        True(connected.IsSuccess, "lost-channel connect");
        SetActiveSession(client);
        var first = await client.ReadAsync(new OpcUaNodeDescriptor(IndustrialNodeId.Numeric(2, 7001)));
        True(!first.IsSuccess, "read on a closing server fails");
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (client.IsConnected && DateTime.UtcNow < deadline) await Task.Delay(10);
        True(!client.IsConnected, "channel is closed");

        var read = await client.ReadAsync(new OpcUaNodeDescriptor(IndustrialNodeId.Numeric(2, 7001)));
        AssertReconnectAdvice(read.Error, "UA_CHANNEL_LOST", DriverOperation.Read, "read after channel loss");
        Equal(DriverErrorKind.Transport, read.Error.Kind, "read after channel loss kind");
        var write = await client.WriteAsync(new OpcUaNodeDescriptor(IndustrialNodeId.Numeric(2, 7001)),
            DriverValue.FromFloat64(1));
        AssertReconnectAdvice(write.Error, "UA_CHANNEL_LOST", DriverOperation.Write, "write after channel loss");
        var browse = await client.BrowseAsync(new[] { new OpcUaBrowseDescription(IndustrialNodeId.Numeric(0, 85)) });
        AssertReconnectAdvice(browse.Error, "UA_CHANNEL_LOST", DriverOperation.Browse, "browse after channel loss");
        var subscription = await client.CreateSubscriptionAsync(new OpcUaSubscriptionRequest());
        AssertReconnectAdvice(subscription.Error, "UA_CHANNEL_LOST", DriverOperation.Subscribe,
            "subscription after channel loss");
    }

    private static async Task TestSessionReactivation()
    {
        await using var server = new SubscriptionServiceServer { AcceptMore = true };
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        True((await client.ConnectAsync()).IsSuccess, "first connect");
        SetActiveSession(client, withAnonymousPolicy: true);
        var subscription = await client.CreateSubscriptionAsync(new OpcUaSubscriptionRequest());
        True(subscription.IsSuccess, "subscription without a pump");

        // The channel breaks while the Session is active: the next session opens by activating it again.
        await DropAndReconnectAsync(server, client, 1);
        var reopened = await client.OpenAnonymousSessionAsync();
        True(reopened.IsSuccess, $"reactivation: {reopened.Error.Code}");
        True(client.SessionReactivated && client.IsSessionActive, "the Session was activated again");
        Equal(IndustrialNodeId.Numeric(0, 9001), client.Session!.Value.SessionId, "same SessionId");
        Equal("ActivateSession,DeleteSubscriptions", RequestNames(server), "reactivation requests: no CreateSession, the orphan deleted");

        // The server no longer knows the Session: the client creates a new one on the same channel.
        await DropAndReconnectAsync(server, client, 2);
        server.ActivateServiceFault = 0x80250000u; // BadSessionIdInvalid
        var fallback = await client.OpenAnonymousSessionAsync();
        Equal("UA_SERVICE_FAULT", fallback.Error.Code, "the test server refuses CreateSession");
        Equal(unchecked((int)0x80560000u), fallback.Error.NativeCode ?? 0, "CreateSession was the failing request");
        Equal("ActivateSession,CreateSession", RequestNames(server), "fallback requests");
        True(!client.SessionReactivated, "no reactivation reported");

        // An ActivateSession whose response is lost may still have activated the Session on the server: after the
        // timeout closes the channel, the next connection tries to activate that Session again.
        await DropAndReconnectAsync(server, client, 3);
        server.ActivateServiceFault = null;
        SetActiveSession(client, withAnonymousPolicy: true, activated: false);
        server.DropActivateResponse = true;
        var lost = await client.ActivateAnonymousSessionAsync();
        True(!lost.IsSuccess, "the unanswered ActivateSession times out");
        var waited = DateTime.UtcNow.AddSeconds(5);
        while (client.IsConnected && DateTime.UtcNow < waited) await Task.Delay(10);
        True(!client.IsConnected, "the timeout closed the channel");
        server.DropActivateResponse = false;
        while (server.ConnectionsClosed < 4 && DateTime.UtcNow < waited.AddSeconds(5)) await Task.Delay(10);
        Equal(4, server.ConnectionsClosed, "server saw the timed-out connection end");
        server.Requests.Clear();
        True((await client.ConnectAsync()).IsSuccess, "reconnect after the lost response");
        var retried = await client.OpenAnonymousSessionAsync();
        True(retried.IsSuccess && client.SessionReactivated, $"the Session of the lost ActivateSession was activated: {retried.Error.Code}");
        // The orphaned subscription was deleted at the first reactivation; nothing is left to delete.
        Equal("ActivateSession", RequestNames(server), "requests after the lost response");

        // With the option off, a new Session is created directly.
        await using var plain = new OpcUaTcpClient(new OpcUaTcpOptions
        {
            EndpointUrl = new Uri($"opc.tcp://127.0.0.1:{server.Port}/ProxusTest"), ConnectTimeout = TimeSpan.FromSeconds(2),
            RequestTimeout = TimeSpan.FromSeconds(2), ReactivateSessionOnReconnect = false
        });
        await DropAndReconnectAsync(server, client, 5, closeOnly: true);
        True((await plain.ConnectAsync()).IsSuccess, "opt-out connect");
        SetActiveSession(plain, withAnonymousPolicy: true);
        await DropAndReconnectAsync(server, plain, 6);
        server.ActivateServiceFault = null;
        _ = await plain.OpenAnonymousSessionAsync();
        Equal("CreateSession", RequestNames(server), "opt-out requests");
    }

    // Drops the client's channel as the receive loop does on a transport failure, waits until the server saw the
    // connection end, then connects again (unless closeOnly) with an empty request log.
    private static async Task DropAndReconnectAsync(SubscriptionServiceServer server, OpcUaTcpClient client, int closed,
        bool closeOnly = false)
    {
        DropChannel(client);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (server.ConnectionsClosed < closed && DateTime.UtcNow < deadline) await Task.Delay(10);
        Equal(closed, server.ConnectionsClosed, "server saw the connection end");
        server.Requests.Clear();
        if (!closeOnly) True((await client.ConnectAsync()).IsSuccess, "reconnect");
    }

    private static string RequestNames(SubscriptionServiceServer server) => string.Join(',', server.Requests.Select(static r => r.TypeId switch
    {
        OpcUaProtocol.ActivateSessionRequestBinary => "ActivateSession",
        OpcUaProtocol.CreateSessionRequestBinary => "CreateSession",
        OpcUaProtocol.DeleteSubscriptionsRequestBinary => "DeleteSubscriptions",
        var other => other.ToString(System.Globalization.CultureInfo.InvariantCulture)
    }));

    private static async Task TestSessionInvalidatingServiceFault()
    {
        await using var server = new SubscriptionServiceServer { ReadServiceFault = 0x80260000u };
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        var connected = await client.ConnectAsync();
        True(connected.IsSuccess, "service fault connect");
        SetActiveSession(client);
        var read = await client.ReadAsync(new OpcUaNodeDescriptor(IndustrialNodeId.Numeric(2, 7001)));
        AssertReconnectAdvice(read.Error, "UA_SERVICE_FAULT", DriverOperation.ReadBatch, "BadSessionClosed");
        Equal(unchecked((int)0x80260000u), read.Error.NativeCode ?? 0, "BadSessionClosed native code");
        True(!client.IsSessionActive, "an invalidated Session is no longer reported as active");

        var next = await client.ReadAsync(new OpcUaNodeDescriptor(IndustrialNodeId.Numeric(2, 7001)));
        AssertReconnectAdvice(next.Error, "UA_SESSION_NOT_ACTIVE", DriverOperation.Read, "read without Session");

        foreach (var status in new[] { 0x80250000u, 0x80270000u, 0x80220000u, 0x80860000u, 0x80870000u })
            True(OpcUaTcpClient.InvalidatesSessionOrChannel(status), $"0x{status:X8} invalidates the Session");
        True(!OpcUaTcpClient.InvalidatesSessionOrChannel(0x80340000u), "BadNodeIdUnknown keeps the Session");
    }

    private static async Task TestPendingRequestOperationOnClose()
    {
        await using var server = new SubscriptionServiceServer(true);
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        var connected = await client.ConnectAsync();
        True(connected.IsSuccess, "pending close connect");
        SetActiveSession(client);
        var read = client.ReadAsync(new OpcUaNodeDescriptor(IndustrialNodeId.Numeric(2, 7001))).AsTask();
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!server.Requests.Any(r => r.TypeId == OpcUaProtocol.ReadRequestBinary) && DateTime.UtcNow < deadline)
            await Task.Delay(5);
        DropChannel(client);
        var result = await read;
        AssertReconnectAdvice(result.Error, "UA_CHANNEL_CLOSED", DriverOperation.ReadBatch, "pending read");
    }

    /// <summary>
    ///     GC-UA-6, deterministic: a request built while the channel is open and sent after the receive
    ///     loop closed it, and a request built after the close, must both report UA_CHANNEL_LOST instead
    ///     of throwing NullReferenceException or InvalidOperationException.
    /// </summary>
    private static async Task TestRequestBuiltAcrossChannelClose()
    {
        await using var server = new SubscriptionServiceServer();
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        var connected = await client.ConnectAsync();
        True(connected.IsSuccess, "built-across connect");
        SetActiveSession(client);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var begin = typeof(OpcUaTcpClient).GetMethod("BeginServiceRequest", flags)!;
        var send = typeof(OpcUaTcpClient).GetMethod("SendServiceAsync", flags)!;

        object?[] Arguments(MethodInfo method, params object?[] leading)
        {
            var parameters = method.GetParameters();
            var arguments = new object?[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
                arguments[i] = i < leading.Length ? leading[i] : parameters[i].DefaultValue;
            return arguments;
        }

        UaBinaryWriter Build(uint requestId)
        {
            try
            {
                var writer = (UaBinaryWriter)begin.Invoke(client, Arguments(begin, OpcUaProtocol.ReadRequestBinary,
                    IndustrialNodeId.Numeric(0, 9002), requestId, requestId, null))!;
                writer.WriteDouble(0);
                writer.WriteUInt32(2);
                writer.WriteInt32(0);
                return writer;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        }

        async Task<DriverError> Send(UaBinaryWriter writer, uint requestId)
        {
            ValueTask<DriverResult<OpcUaServiceEnvelope>> pending;
            try
            {
                pending = (ValueTask<DriverResult<OpcUaServiceEnvelope>>)send.Invoke(client, Arguments(send, null,
                    writer, requestId, requestId, OpcUaProtocol.ReadResponseBinary, DriverOperation.ReadBatch,
                    CancellationToken.None))!;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }

            var result = await pending;
            True(!result.IsSuccess, "send after close fails");
            return result.Error;
        }

        using var builtBefore = Build(901);
        DropChannel(client);
        AssertReconnectAdvice(await Send(builtBefore, 901), "UA_CHANNEL_LOST", DriverOperation.ReadBatch,
            "request built before the close");
        using var builtAfter = Build(902);
        AssertReconnectAdvice(await Send(builtAfter, 902), "UA_CHANNEL_LOST", DriverOperation.ReadBatch,
            "request built after the close");
    }

    /// <summary>
    ///     GC-UA-6: the receive loop closes the channel while other threads are building and sending
    ///     requests. Every call must return a structured result; none may throw.
    /// </summary>
    private static async Task TestRequestRacingChannelClose()
    {
        var failures = 0;
        for (var round = 0; round < 100; round++)
        {
            await using var server = new SubscriptionServiceServer { CloseOnRead = true };
            await server.StartAsync();
            await using var client = CreateClient(server.Port);
            var connected = await client.ConnectAsync();
            True(connected.IsSuccess, "race connect");
            SetActiveSession(client);
            using var start = new ManualResetEventSlim();
            var reads = Enumerable.Range(0, 16).Select(_ => Task.Run(async () =>
            {
                start.Wait();
                return await client.ReadAsync(new OpcUaNodeDescriptor(IndustrialNodeId.Numeric(2, 7001)));
            })).ToArray();
            start.Set();
            foreach (var read in reads)
            {
                DriverResult<DriverSample> result;
                try
                {
                    result = await read;
                }
                catch (Exception ex)
                {
                    failures++;
                    Console.Error.WriteLine($"      race round {round}: {ex.GetType().Name}: {ex.Message}");
                    continue;
                }

                if (!result.IsSuccess)
                    True(!string.IsNullOrEmpty(result.Error.Code) && result.Error.Operation != DriverOperation.Unknown,
                        $"structured race error {result.Error.Code}");
            }
        }

        Equal(0, failures, "requests that threw while the channel closed");
    }

    private static async Task TestTranslateBrowsePaths()
    {
        await using var server = new SubscriptionServiceServer();
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        True((await client.ConnectAsync()).IsSuccess, "translate connect");
        SetActiveSession(client);
        var objects = IndustrialNodeId.Numeric(0, 85);
        var paths = new[]
        {
            OpcUaBrowsePath.FromBrowseNames(objects, new IndustrialQualifiedName(0, "Server"),
                new IndustrialQualifiedName(0, "ServerStatus"), new IndustrialQualifiedName(0, "CurrentTime")),
            new OpcUaBrowsePath(objects, new OpcUaRelativePathElement(new IndustrialQualifiedName(2, "Missing"),
                IndustrialNodeId.Numeric(0, 47), IsInverse: true, IncludeSubtypes: false))
        };
        var result = await client.TranslateBrowsePathsToNodeIdsAsync(paths);
        True(result.IsSuccess, result.IsSuccess ? "translate" : result.Error.ToUserMessage());
        Equal(2, result.Value.Length, "one result per path");
        Equal(0u, result.Value[0].StatusCode, "resolved path status");
        Equal(IndustrialNodeId.Numeric(0, 2258), result.Value[0].Targets[0].TargetId.NodeId, "resolved target");
        True(result.Value[0].Targets[0].IsComplete, "whole path resolved");
        Equal(0x806F0000u, result.Value[1].StatusCode, "Bad_NoMatch is per path");
        Equal(0, result.Value[1].Targets.Count, "no targets for Bad_NoMatch");

        // Request encoding (OPC 10000-4 §7.30/§7.31): BrowsePath[] of startingNode and RelativePath elements.
        var message = server.LastServiceRequest;
        var reader = new UaBinaryReader(message.AsSpan(SubscriptionServiceServer.RequestBodyOffset(message)));
        Equal(2, reader.ReadInt32(), "path count");
        Equal(objects, reader.ReadNodeId(), "starting node");
        Equal(3, reader.ReadInt32(), "element count");
        Equal(IndustrialNodeId.Numeric(0, 33), reader.ReadNodeId(), "default HierarchicalReferences");
        True(!reader.ReadBoolean() && reader.ReadBoolean(), "forward, include subtypes");
        Equal(new IndustrialQualifiedName(0, "Server"), reader.ReadQualifiedName(), "first BrowseName");
        _ = reader.ReadNodeId(); _ = reader.ReadBoolean(); _ = reader.ReadBoolean(); _ = reader.ReadQualifiedName();
        _ = reader.ReadNodeId(); _ = reader.ReadBoolean(); _ = reader.ReadBoolean(); _ = reader.ReadQualifiedName();
        Equal(objects, reader.ReadNodeId(), "second starting node");
        Equal(1, reader.ReadInt32(), "second element count");
        Equal(IndustrialNodeId.Numeric(0, 47), reader.ReadNodeId(), "explicit reference type");
        True(reader.ReadBoolean() && !reader.ReadBoolean(), "inverse, no subtypes");
        Equal(new IndustrialQualifiedName(2, "Missing"), reader.ReadQualifiedName(), "namespaced BrowseName");
        Equal(0, reader.Remaining, "no trailing request bytes");

        var invalid = await client.TranslateBrowsePathsToNodeIdsAsync(new[] { new OpcUaBrowsePath(objects) });
        Equal("UA_BROWSE_PATH", invalid.Error.Code, "empty path rejected before I/O");
    }

    private static async Task TestHistoryReadRaw()
    {
        await using var server = new SubscriptionServiceServer();
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        True((await client.ConnectAsync()).IsSuccess, "history connect");
        SetActiveSession(client);
        var node = IndustrialNodeId.Numeric(2, 7001);
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var request = new OpcUaHistoryReadRawRequest(start, start.AddHours(1), 100, ReturnBounds: true);
        var first = await client.HistoryReadRawAsync(new[] { new OpcUaHistoryReadNode(node) }, request);
        True(first.IsSuccess, first.IsSuccess ? "history first page" : first.Error.ToUserMessage());
        Equal(2, first.Value[0].Values.Count, "first page values");
        Equal(1.5, first.Value[0].Values[0].Value.AsFloat64(), "first value");
        True(first.Value[0].HasContinuationPoint, "continuation point returned");

        // Request encoding (OPC 10000-11 §6.4.3, OPC 10000-4 §5.10.3).
        var message = server.LastServiceRequest;
        var reader = new UaBinaryReader(message.AsSpan(SubscriptionServiceServer.RequestBodyOffset(message)));
        var details = reader.ReadExtensionObject();
        Equal(IndustrialNodeId.Numeric(0, 649), details.TypeId.NodeId, "ReadRawModifiedDetails encoding id");
        var body = new UaBinaryReader(details.EncodedBody.Span);
        True(!body.ReadBoolean(), "isReadModified");
        Equal(start, body.ReadDateTime(), "startTime");
        Equal(start.AddHours(1), body.ReadDateTime(), "endTime");
        Equal(100u, body.ReadUInt32(), "numValuesPerNode");
        True(body.ReadBoolean(), "returnBounds");
        Equal(2u, reader.ReadUInt32(), "TimestampsToReturn.Both");
        True(!reader.ReadBoolean(), "releaseContinuationPoints");
        Equal(1, reader.ReadInt32(), "node count");
        Equal(node, reader.ReadNodeId(), "node id");
        True(reader.ReadString() is null, "null index range");
        _ = reader.ReadQualifiedName();
        Equal(-1, reader.ReadInt32(), "null continuation point on the first call");

        var next = await client.HistoryReadRawAsync(
            new[] { new OpcUaHistoryReadNode(node, first.Value[0].ContinuationPoint) }, request);
        True(next.IsSuccess && next.Value[0].Values.Count == 1 && !next.Value[0].HasContinuationPoint,
            "second page ends the read");
        Equal(3.5, next.Value[0].Values[0].Value.AsFloat64(), "second page value");

        var release = await client.ReleaseHistoryContinuationPointsAsync(
            new[] { new OpcUaHistoryReadNode(node, first.Value[0].ContinuationPoint) });
        True(release.IsSuccess, "release continuation point");
        var released = server.LastServiceRequest;
        var releaseReader = new UaBinaryReader(released.AsSpan(SubscriptionServiceServer.RequestBodyOffset(released)));
        _ = releaseReader.ReadExtensionObject();
        _ = releaseReader.ReadUInt32();
        True(releaseReader.ReadBoolean(), "releaseContinuationPoints flag");
    }

    private static async Task TestMethodCall()
    {
        await using var server = new SubscriptionServiceServer();
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        True((await client.ConnectAsync()).IsSuccess, "call connect");
        SetActiveSession(client);
        var lab = IndustrialNodeId.String(1, "Lab");
        var add = IndustrialNodeId.String(1, "Add");
        var single = await client.CallAsync(lab, add, new[] { DriverValue.FromInt32(2), DriverValue.FromInt32(40) });
        True(single.IsSuccess && single.Value.IsGood, single.IsSuccess ? "call status" : single.Error.ToUserMessage());
        Equal(42, single.Value.OutputArguments[0].AsInt32(), "Add output");

        // Request encoding (OPC 10000-4 §5.11.2): CallMethodRequest[] of objectId, methodId and Variant inputs.
        var message = server.LastServiceRequest;
        var reader = new UaBinaryReader(message.AsSpan(SubscriptionServiceServer.RequestBodyOffset(message)));
        Equal(1, reader.ReadInt32(), "call count");
        Equal(lab, reader.ReadNodeId(), "object id");
        Equal(add, reader.ReadNodeId(), "method id");
        Equal(2, reader.ReadInt32(), "input count");
        Equal(2, OpcUaBinaryCodec.ReadVariant(ref reader).AsInt32(), "first input");
        Equal(40, OpcUaBinaryCodec.ReadVariant(ref reader).AsInt32(), "second input");
        Equal(0, reader.Remaining, "no trailing request bytes");

        var batch = await client.CallAsync(new[]
        {
            new OpcUaMethodCall(lab, add, DriverValue.FromInt32(1), DriverValue.FromFloat64(2)),
            new OpcUaMethodCall(lab, IndustrialNodeId.String(1, "Missing"))
        });
        True(batch.IsSuccess, "rejected calls do not fail the service");
        Equal(0x80AB0000u, batch.Value[0].StatusCode, "Bad_InvalidArgument");
        Equal(0u, batch.Value[0].InputArgumentResults[0], "first argument accepted");
        Equal(0x80740000u, batch.Value[0].InputArgumentResults[1], "second argument Bad_TypeMismatch");
        Equal(0, batch.Value[0].OutputArguments.Count, "no outputs on failure");
        Equal(0x80750000u, batch.Value[1].StatusCode, "Bad_MethodInvalid");
        True(!batch.Value[1].IsGood, "IsGood is false for a Bad status");
    }

    private static async Task TestMethodCallOutcomeUnknown()
    {
        await using var server = new SubscriptionServiceServer { DropCallResponse = true };
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        True((await client.ConnectAsync()).IsSuccess, "outcome connect");
        SetActiveSession(client);
        var call = await client.CallAsync(IndustrialNodeId.String(1, "Lab"), IndustrialNodeId.String(1, "Add"),
            new[] { DriverValue.FromInt32(1), DriverValue.FromInt32(1) });
        Equal("UA_CALL_OUTCOME_UNKNOWN", call.Error.Code, "lost response");
        Equal(DriverRetryDisposition.DoNotRetry, call.Error.Retry, "never repeat a method automatically");
        Equal(DriverConnectionDisposition.Broken, call.Error.Connection, "connection must be rebuilt");
        Equal(DriverOperation.Execute, call.Error.Operation, "operation");

        var noSession = await client.CallAsync(IndustrialNodeId.String(1, "Lab"), IndustrialNodeId.String(1, "Add"));
        Equal("UA_CHANNEL_LOST", noSession.Error.Code, "nothing sent without a channel");
        Equal(DriverRetryDisposition.ReconnectAndRetry, noSession.Error.Retry, "safe to retry after reconnect");
    }

    private static async Task TestEventMonitoredItem()
    {
        await using var server = new SubscriptionServiceServer { EventSelectClauseResults = [0, 0x80600000] };
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        True((await client.ConnectAsync()).IsSuccess, "event connect");
        SetActiveSession(client);
        var alarmType = IndustrialNodeId.Numeric(0, 2915);
        var systemType = IndustrialNodeId.Numeric(0, 2130);
        var filter = new OpcUaEventFilter(OpcUaEventField.Property("Severity"), OpcUaEventField.ConditionId)
        {
            EventTypes = [alarmType, systemType], MinimumSeverity = 500
        };
        var created = await client.CreateEventMonitoredItemsAsync(500,
            new[] { new OpcUaEventMonitoredItemRequest(IndustrialNodeId.Numeric(0, 2253), 88, filter) });
        True(created.IsSuccess, created.IsSuccess ? "event item" : created.Error.ToUserMessage());
        Equal(600u, created.Value[0].MonitoredItemId, "monitored item id");
        Equal(2, created.Value[0].SelectClauseResults.Count, "select clause results");
        Equal(0x80600000u, created.Value[0].SelectClauseResults[1], "Bad_BrowseNameInvalid reported per clause");

        // MonitoredItemCreateRequest for events (OPC 10000-4 §5.12.2, §7.22.3, §7.7).
        var message = server.LastServiceRequest;
        var reader = new UaBinaryReader(message.AsSpan(SubscriptionServiceServer.RequestBodyOffset(message)));
        Equal(500u, reader.ReadUInt32(), "subscription id");
        _ = reader.ReadUInt32();
        Equal(1, reader.ReadInt32(), "item count");
        Equal(IndustrialNodeId.Numeric(0, 2253), reader.ReadNodeId(), "Server notifier");
        Equal(12u, reader.ReadUInt32(), "AttributeId EventNotifier");
        _ = reader.ReadString(); _ = reader.ReadQualifiedName();
        Equal(2u, reader.ReadUInt32(), "Reporting");
        Equal(88u, reader.ReadUInt32(), "client handle");
        Equal(0.0, reader.ReadDouble(), "events are not sampled");
        var extension = reader.ReadExtensionObject();
        Equal(IndustrialNodeId.Numeric(0, 727), extension.TypeId.NodeId, "EventFilter encoding id");
        Equal(1000u, reader.ReadUInt32(), "default event queue");

        var body = new UaBinaryReader(extension.EncodedBody.Span);
        Equal(2, body.ReadInt32(), "select clause count");
        Equal(IndustrialNodeId.Numeric(0, 2041), body.ReadNodeId(), "BaseEventType");
        Equal(1, body.ReadInt32(), "Severity path length");
        Equal(new IndustrialQualifiedName(0, "Severity"), body.ReadQualifiedName(), "Severity");
        Equal(13u, body.ReadUInt32(), "Value attribute");
        True(body.ReadString() is null, "no index range");
        Equal(IndustrialNodeId.Numeric(0, 2782), body.ReadNodeId(), "ConditionType");
        Equal(0, body.ReadInt32(), "ConditionId has an empty path");
        Equal(1u, body.ReadUInt32(), "NodeId attribute");
        _ = body.ReadString();

        // ContentFilter, pre-order: 0 And(1, 4), 1 Or(2, 3), 2 OfType(alarm), 3 OfType(system), 4 Severity >= 500.
        Equal(5, body.ReadInt32(), "element count");
        uint[] ExpectElementOperands(ref UaBinaryReader r, uint op, string label)
        {
            Equal(op, r.ReadUInt32(), label);
            var n = r.ReadInt32();
            var indexes = new uint[n];
            for (var i = 0; i < n; i++)
            {
                var operand = r.ReadExtensionObject();
                Equal(IndustrialNodeId.Numeric(0, 594), operand.TypeId.NodeId, $"{label} ElementOperand");
                indexes[i] = new UaBinaryReader(operand.EncodedBody.Span).ReadUInt32();
            }

            return indexes;
        }

        var and = ExpectElementOperands(ref body, 10, "And");
        True(and.SequenceEqual(new uint[] { 1, 4 }), "And(1, 4)");
        var or = ExpectElementOperands(ref body, 11, "Or");
        True(or.SequenceEqual(new uint[] { 2, 3 }), "Or(2, 3)");
        foreach (var type in new[] { alarmType, systemType })
        {
            Equal(14u, body.ReadUInt32(), "OfType");
            Equal(1, body.ReadInt32(), "OfType operand count");
            var literal = body.ReadExtensionObject();
            Equal(IndustrialNodeId.Numeric(0, 597), literal.TypeId.NodeId, "LiteralOperand");
            var literalReader = new UaBinaryReader(literal.EncodedBody.Span);
            Equal(type, OpcUaBinaryCodec.ReadVariant(ref literalReader).AsNodeId(), "event type literal");
        }

        Equal(4u, body.ReadUInt32(), "GreaterThanOrEqual");
        Equal(2, body.ReadInt32(), "severity operand count");
        Equal(IndustrialNodeId.Numeric(0, 603), body.ReadExtensionObject().TypeId.NodeId, "Severity operand");
        var minimum = body.ReadExtensionObject();
        var minimumReader = new UaBinaryReader(minimum.EncodedBody.Span);
        Equal((ushort)500, OpcUaBinaryCodec.ReadVariant(ref minimumReader).AsUInt16(), "minimum severity");
        Equal(0, body.Remaining, "no trailing filter bytes");

        var baseFields = OpcUaEventFilter.BaseEventFields().SelectClauses.Select(static f => f.BrowsePath[0].Name);
        True(baseFields.SequenceEqual(new[]
        {
            "EventId", "EventType", "SourceNode", "SourceName", "Time", "ReceiveTime", "Message", "Severity"
        }), "BaseEventFields order is the documented field index");

        var empty = await client.CreateEventMonitoredItemsAsync(500,
            new[] { new OpcUaEventMonitoredItemRequest(IndustrialNodeId.Numeric(0, 2253), 1, new OpcUaEventFilter()) });
        Equal("UA_EVENT_FILTER", empty.Error.Code, "a filter needs select clauses");
    }

    private static async Task TestEventNotifications()
    {
        await using var server = new SubscriptionServiceServer { PublishEvents = true };
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        True((await client.ConnectAsync()).IsSuccess, "events connect");
        SetActiveSession(client);
        var publish = client.PublishAsync(ReadOnlyMemory<OpcUaSubscriptionAcknowledgement>.Empty).AsTask();
        await server.PublishReceived.WaitAsync(TimeSpan.FromSeconds(2));
        server.ReleasePublish();
        var response = await publish;
        True(response.IsSuccess, response.IsSuccess ? "publish" : response.Error.ToUserMessage());
        Equal(1, response.Value.Values.Count, "data change still decoded");
        Equal(1, response.Value.Events.Count, "one event");
        var notification = response.Value.Events[0];
        Equal(88u, notification.ClientHandle, "event client handle");
        Equal(5, notification.Fields.Count, "event fields");
        True(notification.Fields[0].AsBytes().Span.SequenceEqual(new byte[] { 9, 9 }), "EventId");
        Equal(IndustrialNodeId.Numeric(0, 2915), notification.Fields[1].AsNodeId(), "EventType");
        Equal("Boiler", notification.Fields[2].AsString(), "SourceName");
        Equal((ushort)700, notification.Fields[3].AsUInt16(), "Severity");
        Equal(IndustrialNodeId.String(1, "Cond"), notification.Fields[4].AsNodeId(), "ConditionId");
        True(!response.Value.IsKeepAlive, "a message with events is not a keep-alive");
    }

    private static async Task TestConditionMethods()
    {
        await using var server = new SubscriptionServiceServer();
        await server.StartAsync();
        await using var client = CreateClient(server.Port);
        True((await client.ConnectAsync()).IsSuccess, "condition connect");
        SetActiveSession(client);
        var refresh = await client.RefreshConditionsAsync(500);
        True(refresh.IsSuccess, refresh.IsSuccess ? "refresh" : refresh.Error.ToUserMessage());
        var message = server.LastServiceRequest;
        var reader = new UaBinaryReader(message.AsSpan(SubscriptionServiceServer.RequestBodyOffset(message)));
        Equal(1, reader.ReadInt32(), "refresh call count");
        Equal(IndustrialNodeId.Numeric(0, 2782), reader.ReadNodeId(), "ConditionType object");
        Equal(IndustrialNodeId.Numeric(0, 3875), reader.ReadNodeId(), "ConditionRefresh method");
        Equal(1, reader.ReadInt32(), "refresh argument count");
        Equal(500u, OpcUaBinaryCodec.ReadVariant(ref reader).AsUInt32(), "SubscriptionId argument");

        var condition = IndustrialNodeId.String(1, "Cond");
        var ack = await client.AcknowledgeConditionAsync(condition, new byte[] { 9, 9 },
            new IndustrialLocalizedText("en", "seen"));
        True(ack.IsSuccess, ack.IsSuccess ? "acknowledge" : ack.Error.ToUserMessage());
        message = server.LastServiceRequest;
        reader = new UaBinaryReader(message.AsSpan(SubscriptionServiceServer.RequestBodyOffset(message)));
        _ = reader.ReadInt32();
        Equal(condition, reader.ReadNodeId(), "ConditionId object");
        Equal(IndustrialNodeId.Numeric(0, 9111), reader.ReadNodeId(), "Acknowledge method");
        Equal(2, reader.ReadInt32(), "acknowledge argument count");
        True(OpcUaBinaryCodec.ReadVariant(ref reader).AsBytes().Span.SequenceEqual(new byte[] { 9, 9 }), "EventId argument");
        Equal("seen", OpcUaBinaryCodec.ReadVariant(ref reader).AsLocalizedText().Text, "comment argument");

        var noEvent = await client.AcknowledgeConditionAsync(condition, ReadOnlyMemory<byte>.Empty,
            new IndustrialLocalizedText(null, ""));
        Equal("UA_CONDITION_EVENT_ID", noEvent.Error.Code, "EventId required");
    }

    private static async Task TestHistoryReadValidation()
    {
        await using var client = CreateClient(1);
        var node = new[] { new OpcUaHistoryReadNode(IndustrialNodeId.Numeric(2, 1)) };
        var noInterval = await client.HistoryReadRawAsync(node, new OpcUaHistoryReadRawRequest(DateTime.MinValue,
            DateTime.MinValue));
        Equal("UA_HISTORY_INTERVAL", noInterval.Error.Code, "no interval");
        var openWithoutCount = await client.HistoryReadRawAsync(node,
            new OpcUaHistoryReadRawRequest(DateTime.UtcNow, DateTime.MinValue));
        Equal("UA_HISTORY_INTERVAL", openWithoutCount.Error.Code, "open interval needs a count");
        var badTimestamps = await client.HistoryReadRawAsync(node, new OpcUaHistoryReadRawRequest(DateTime.UtcNow,
            DateTime.MinValue, 10, TimestampsToReturn: (OpcUaTimestampsToReturn)3));
        Equal("UA_HISTORY_TIMESTAMPS", badTimestamps.Error.Code, "Neither is invalid for HistoryRead");
        var noTimes = await client.HistoryReadAtTimeAsync(node, ReadOnlyMemory<DateTime>.Empty);
        Equal("UA_HISTORY_TIMES", noTimes.Error.Code, "AtTime needs times");
        var disconnected = await client.HistoryReadRawAsync(node,
            new OpcUaHistoryReadRawRequest(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow));
        Equal("UA_CHANNEL_LOST", disconnected.Error.Code, "no channel");
    }

    private static async Task TestIdleDispatcher()
    {
        await using var server = new SubscriptionServiceServer();
        await server.StartAsync();
        await using var client = CreateClient(server.Port, TimeSpan.FromMilliseconds(100));
        var connected = await client.ConnectAsync();
        True(connected.IsSuccess, "idle dispatcher connect");
        typeof(OpcUaTcpClient).GetProperty(nameof(OpcUaTcpClient.Session))!.SetValue(client,
            new OpcUaSessionInfo(IndustrialNodeId.Numeric(0, 9001), IndustrialNodeId.Numeric(0, 9002), 60_000, 65_536,
                "anonymous", true));
        await Task.Delay(300);
        True(client.IsConnected, "an idle receive loop must not close a healthy connection");
        var read = await client.ReadAsync(new OpcUaNodeDescriptor(IndustrialNodeId.Numeric(2, 7001)));
        True(read.IsSuccess && read.Value.Value.AsFloat64() == 12.5, "request after idle interval");
    }

    private static async Task TestSubscriptionSequenceMatrix()
    {
        var transport = new FakeSubscriptionTransport();
        transport.QueuePublish(Publish(500, 1));
        transport.QueuePublish(Publish(500, 1));
        transport.QueuePublish(Publish(500, 3));
        transport.SetRepublish(2, DriverResult<OpcUaPublishResponse>.Success(Publish(500, 2)));
        await using var subscription = new OpcUaSubscription(transport, 500,
            new OpcUaSubscriptionRuntimeOptions { NotificationBufferCapacity = 8, MaximumRepublishGap = 4 });
        subscription.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using (var notifications = subscription.ReadNotificationsAsync(timeout.Token).GetAsyncEnumerator())
        {
            True(await notifications.MoveNextAsync(), "sequence 1");
            Equal(1u, notifications.Current.SequenceNumber, "first sequence");
            True(await notifications.MoveNextAsync(), "republished sequence 2");
            Equal(2u, notifications.Current.SequenceNumber, "second sequence");
            True(await notifications.MoveNextAsync(), "sequence 3");
            Equal(3u, notifications.Current.SequenceNumber, "third sequence");
        }

        Equal(3, subscription.DeliveredNotifications, "duplicate is not delivered");
        Equal(1, transport.RepublishRequests.Count, "one republish request");
        Equal(2u, transport.RepublishRequests[0], "missing sequence requested");

        var wrapTransport = new FakeSubscriptionTransport();
        wrapTransport.QueuePublish(Publish(700, uint.MaxValue));
        wrapTransport.QueuePublish(Publish(700, 1));
        await using var wrap = new OpcUaSubscription(wrapTransport, 700,
            new OpcUaSubscriptionRuntimeOptions { NotificationBufferCapacity = 4 });
        wrap.Start();
        using var wrapTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var wrapped = wrap.ReadNotificationsAsync(wrapTimeout.Token).GetAsyncEnumerator();
        True(await wrapped.MoveNextAsync(), "max sequence");
        Equal(uint.MaxValue, wrapped.Current.SequenceNumber, "max sequence value");
        True(await wrapped.MoveNextAsync(), "wrapped sequence");
        Equal(1u, wrapped.Current.SequenceNumber, "wrapped sequence value");
        Equal(0, wrapTransport.RepublishRequests.Count, "wrap does not republish");
    }

    private static async Task TestSubscriptionRepublishFaults()
    {
        var gapTransport = new FakeSubscriptionTransport();
        gapTransport.QueuePublish(Publish(500, 10));
        gapTransport.QueuePublish(Publish(500, 13));
        gapTransport.SetRepublish(11, DriverResult<OpcUaPublishResponse>.Success(Publish(500, 11)));
        await AssertSubscriptionFault(gapTransport, 1, "UA_SEQUENCE_GAP_LIMIT");

        var wrongTransport = new FakeSubscriptionTransport();
        wrongTransport.QueuePublish(Publish(500, 20));
        wrongTransport.QueuePublish(Publish(500, 22));
        wrongTransport.SetRepublish(21, DriverResult<OpcUaPublishResponse>.Success(Publish(500, 99)));
        await AssertSubscriptionFault(wrongTransport, 4, "UA_REPUBLISH_SEQUENCE");

        var unavailableTransport = new FakeSubscriptionTransport();
        unavailableTransport.QueuePublish(Publish(500, 30));
        unavailableTransport.QueuePublish(Publish(500, 32));
        unavailableTransport.SetRepublish(31,
            DriverResult<OpcUaPublishResponse>.Failure(FakeError("UA_BAD_MESSAGE_NOT_AVAILABLE",
                DriverOperation.Republish)));
        await AssertSubscriptionFault(unavailableTransport, 4, "UA_BAD_MESSAGE_NOT_AVAILABLE");
    }

    private static async Task TestSubscriptionBackpressure()
    {
        var transport = new FakeSubscriptionTransport();
        transport.QueuePublish(Publish(500, 1));
        transport.QueuePublish(Publish(500, 2));
        transport.QueuePublish(Publish(500, 3));
        await using var subscription = new OpcUaSubscription(transport, 500,
            new OpcUaSubscriptionRuntimeOptions { NotificationBufferCapacity = 1 });
        subscription.Start();
        await WaitUntil(() => transport.PublishCalls >= 2, "second Publish request");
        await Task.Delay(100);
        Equal(2, transport.PublishCalls, "full notification buffer applies backpressure");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var notifications = subscription.ReadNotificationsAsync(timeout.Token).GetAsyncEnumerator();
        True(await notifications.MoveNextAsync(), "consume first notification");
        Equal(1u, notifications.Current.SequenceNumber, "first buffered sequence");
        True(await notifications.MoveNextAsync(), "consume second notification");
        Equal(2u, notifications.Current.SequenceNumber, "second buffered sequence");
        await WaitUntil(() => transport.PublishCalls >= 3, "third Publish request");
        var thirdAcks = transport.AcknowledgementBatches[2];
        True(
            thirdAcks.Any(static acknowledgement =>
                acknowledgement.SubscriptionId == 500 && acknowledgement.SequenceNumber == 1),
            "first sequence acknowledged only after consumer advances");
        True(transport.AcknowledgementBatches[0].Length == 0 && transport.AcknowledgementBatches[1].Length == 0,
            "unconsumed notification is not acknowledged");
    }

    private static async Task TestManagedSubscriptionRecoveryMatrix()
    {
        var plan = new OpcUaManagedSubscriptionPlan(OpcUaSubscriptionRequest.Default,
            [new OpcUaMonitoredItemRequest(new OpcUaNodeDescriptor(IndustrialNodeId.String(2, "Value")), 1)]);
        var transport = new FakeSubscriptionTransport();
        transport.QueuePublish(Publish(500, 1));
        transport.QueuePublish(
            DriverResult<OpcUaPublishResponse>.Failure(FakeError("UA_LINK_DOWN", DriverOperation.Publish)));
        transport.QueueRecreate(
            DriverResult<OpcUaSubscriptionInfo>.Failure(FakeError("UA_RECONNECT_1", DriverOperation.Connect)));
        transport.QueueRecreate(
            DriverResult<OpcUaSubscriptionInfo>.Failure(FakeError("UA_RECONNECT_2", DriverOperation.Connect)));
        transport.QueueRecreate(
            DriverResult<OpcUaSubscriptionInfo>.Success(new OpcUaSubscriptionInfo(900, 1000, 60, 10)));
        transport.QueuePublish(Publish(900, 1));
        await using var subscription = new OpcUaSubscription(transport, 500,
            new OpcUaSubscriptionRuntimeOptions
                { NotificationBufferCapacity = 4, MaximumReconnectAttempts = 3, ReconnectDelay = TimeSpan.Zero }, plan);
        subscription.Start();
        await WaitUntil(() => subscription.RecreationCount == 1, "managed subscription recreation");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var notifications = subscription.ReadNotificationsAsync(timeout.Token).GetAsyncEnumerator();
        True(await notifications.MoveNextAsync(), "old subscription notification");
        Equal(500u, notifications.Current.SubscriptionId, "old subscription id");
        True(await notifications.MoveNextAsync(), "new subscription notification");
        Equal(900u, notifications.Current.SubscriptionId, "new subscription id");
        Equal(3, transport.RecreateCalls, "recovery retries until success");
        True(
            transport.AcknowledgementBatches.SelectMany(static batch => batch)
                .All(static acknowledgement => acknowledgement.SubscriptionId != 500),
            "old subscription acknowledgement is not sent after recovery");

        var exhausted = new FakeSubscriptionTransport();
        exhausted.QueuePublish(
            DriverResult<OpcUaPublishResponse>.Failure(FakeError("UA_LINK_DOWN", DriverOperation.Publish)));
        exhausted.QueueRecreate(
            DriverResult<OpcUaSubscriptionInfo>.Failure(FakeError("UA_RECONNECT_FIRST", DriverOperation.Connect)));
        exhausted.QueueRecreate(
            DriverResult<OpcUaSubscriptionInfo>.Failure(FakeError("UA_RECONNECT_FINAL", DriverOperation.Connect)));
        await using var failed = new OpcUaSubscription(exhausted, 500,
            new OpcUaSubscriptionRuntimeOptions { MaximumReconnectAttempts = 2, ReconnectDelay = TimeSpan.Zero }, plan);
        failed.Start();
        var fault = await CaptureFault(failed);
        Equal("UA_RECONNECT_FINAL", fault.Code, "last recovery failure is retained");
        Equal(2, exhausted.RecreateCalls, "recovery attempt limit");
    }

    private static async Task TestMultipleSubscriptionRouting()
    {
        var transport = new FakeSubscriptionTransport();
        await using var first = new OpcUaSubscription(transport, 500,
            new OpcUaSubscriptionRuntimeOptions { NotificationBufferCapacity = 2 });
        await using var second = new OpcUaSubscription(transport, 600,
            new OpcUaSubscriptionRuntimeOptions { NotificationBufferCapacity = 2 });
        first.Start();
        await WaitUntil(() => transport.PublishCalls == 1, "first subscription Publish");
        second.Start();
        await WaitUntil(() => transport.PublishCalls == 2, "second subscription Publish");
        transport.QueuePublish(Publish(600, 1));
        transport.QueuePublish(Publish(500, 1));
        await Task.Delay(100);
        True(first.DeliveredNotifications == 1 && second.DeliveredNotifications == 1,
            $"both runtimes receive a notification; first={first.DeliveredNotifications}/{first.Fault?.Code ?? "none"}, second={second.DeliveredNotifications}/{second.Fault?.Code ?? "none"}, routes={string.Join(',', transport.RouteEvents)}");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var firstNotifications = first.ReadNotificationsAsync(timeout.Token).GetAsyncEnumerator();
        await using var secondNotifications = second.ReadNotificationsAsync(timeout.Token).GetAsyncEnumerator();
        True(await firstNotifications.MoveNextAsync(), "first routed notification");
        True(await secondNotifications.MoveNextAsync(), "second routed notification");
        Equal(500u, firstNotifications.Current.SubscriptionId, "first runtime receives its own subscription");
        Equal(600u, secondNotifications.Current.SubscriptionId, "second runtime receives its own subscription");
        True(first.Fault is null && second.Fault is null, "cross-routed Publish responses do not fault either runtime");
    }

    private static async Task TestSubscriptionCancellationLifecycle()
    {
        var transport = new FakeSubscriptionTransport();
        var subscription = new OpcUaSubscription(transport, 500, new OpcUaSubscriptionRuntimeOptions());
        subscription.Start();
        await WaitUntil(() => transport.PublishCalls == 1, "pending Publish before cancellation");
        using var consumerStop = new CancellationTokenSource();
        await using (var notifications = subscription.ReadNotificationsAsync(consumerStop.Token).GetAsyncEnumerator())
        {
            var pendingRead = notifications.MoveNextAsync().AsTask();
            consumerStop.Cancel();
            try
            {
                await pendingRead;
                throw new InvalidOperationException("Cancelled consumer read unexpectedly completed.");
            }
            catch (OperationCanceledException)
            {
            }
        }

        await subscription.DisposeAsync();
        True(subscription.Completion.IsCompletedSuccessfully, "dispose completes the pending Publish pump");
        True(subscription.Fault is null, "consumer cancellation and dispose are not subscription faults");
    }

    private static async Task AssertSubscriptionFault(IOpcUaSubscriptionRuntimeTransport transport, int maximumGap,
        string expectedCode)
    {
        await using var subscription = new OpcUaSubscription(transport, 500,
            new OpcUaSubscriptionRuntimeOptions { NotificationBufferCapacity = 8, MaximumRepublishGap = maximumGap });
        subscription.Start();
        var fault = await CaptureFault(subscription);
        Equal(expectedCode, fault.Code, "subscription fault code");
    }

    private static async Task<DriverError> CaptureFault(OpcUaSubscription subscription)
    {
        try
        {
            await subscription.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OpcUaSubscriptionFaultException)
        {
        }

        True(subscription.Fault is not null, "subscription fault captured");
        return subscription.Fault!.Value;
    }

    private static OpcUaPublishResponse Publish(uint subscriptionId, uint sequenceNumber)
    {
        return new OpcUaPublishResponse(subscriptionId, [], false, sequenceNumber,
            DateTime.UtcNow,
            [
                new OpcUaMonitoredValue(1,
                    new DriverSample(DriverValue.FromUInt32(sequenceNumber), DriverQuality.Good, DateTime.UtcNow,
                        DateTime.UtcNow))
            ], []);
    }

    private static DriverError FakeError(string code, DriverOperation operation)
    {
        return new DriverError(DriverErrorKind.Transport, code, code,
            Operation: operation, Protocol: OpcUaProtocol.Name, Endpoint: "opc.tcp://fake/",
            Retry: DriverRetryDisposition.ReconnectAndRetry,
            Connection: DriverConnectionDisposition.Broken);
    }

    private static async Task WaitUntil(Func<bool> condition, string message)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition()) await Task.Delay(10, timeout.Token);
        True(condition(), message);
    }

    private static OpcUaTcpClient CreateClient(int port, TimeSpan? publishRequestTimeout = null)
    {
        return new OpcUaTcpClient(new OpcUaTcpOptions
        {
            EndpointUrl = new Uri($"opc.tcp://127.0.0.1:{port}/ProxusTest"),
            ConnectTimeout = TimeSpan.FromSeconds(2), RequestTimeout = TimeSpan.FromSeconds(2),
            PublishRequestTimeout = publishRequestTimeout ?? TimeSpan.FromSeconds(2),
            ReceiveBufferSize = 65_536, SendBufferSize = 65_536
        });
    }

    private static async Task Run(string name, Func<Task> test)
    {
        _total++;
        try
        {
            await test();
            _passed++;
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL {name}: {ex}");
            Environment.ExitCode = 1;
            throw;
        }
    }

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual, string message) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, actual {actual}");
    }
}

internal sealed class FakeSubscriptionTransport : IOpcUaSubscriptionRuntimeTransport, IOpcUaSubscriptionRuntimeRouter
{
    private readonly List<OpcUaSubscriptionAcknowledgement[]> _acknowledgementBatches = [];
    private readonly object _gate = new();

    private readonly Channel<DriverResult<OpcUaPublishResponse>> _publishes =
        Channel.CreateUnbounded<DriverResult<OpcUaPublishResponse>>();

    private readonly Channel<DriverResult<OpcUaSubscriptionInfo>> _recreations =
        Channel.CreateUnbounded<DriverResult<OpcUaSubscriptionInfo>>();

    private readonly List<uint> _republishRequests = [];
    private readonly Dictionary<uint, DriverResult<OpcUaPublishResponse>> _republishes = new();
    private readonly List<string> _routeEvents = [];
    private readonly Dictionary<uint, OpcUaSubscription> _subscriptions = new();
    private int _publishCalls;
    private int _recreateCalls;
    public int PublishCalls => Volatile.Read(ref _publishCalls);
    public int RecreateCalls => Volatile.Read(ref _recreateCalls);

    public IReadOnlyList<uint> RepublishRequests
    {
        get
        {
            lock (_gate)
            {
                return [.. _republishRequests];
            }
        }
    }

    public IReadOnlyList<OpcUaSubscriptionAcknowledgement[]> AcknowledgementBatches
    {
        get
        {
            lock (_gate)
            {
                return [.. _acknowledgementBatches.Select(static batch => batch.ToArray())];
            }
        }
    }

    public IReadOnlyList<string> RouteEvents
    {
        get
        {
            lock (_gate)
            {
                return [.. _routeEvents];
            }
        }
    }

    public bool RegisterSubscriptionRuntime(uint subscriptionId, OpcUaSubscription subscription)
    {
        lock (_gate)
        {
            return _subscriptions.TryAdd(subscriptionId, subscription);
        }
    }

    public void UnregisterSubscriptionRuntime(uint subscriptionId, OpcUaSubscription subscription)
    {
        lock (_gate)
        {
            if (_subscriptions.TryGetValue(subscriptionId, out var current) && ReferenceEquals(current, subscription))
                _subscriptions.Remove(subscriptionId);
        }
    }

    public bool UpdateSubscriptionRuntime(uint previousSubscriptionId, uint revisedSubscriptionId,
        OpcUaSubscription subscription)
    {
        lock (_gate)
        {
            if (_subscriptions.TryGetValue(revisedSubscriptionId, out var collision) &&
                !ReferenceEquals(collision, subscription)) return false;
            if (_subscriptions.TryGetValue(previousSubscriptionId, out var current) &&
                ReferenceEquals(current, subscription))
                _subscriptions.Remove(previousSubscriptionId);
            _subscriptions[revisedSubscriptionId] = subscription;
            return true;
        }
    }

    public bool TryRoutePublishResponse(OpcUaSubscription source, OpcUaPublishResponse response)
    {
        lock (_gate)
        {
            var routed = _subscriptions.TryGetValue(response.SubscriptionId, out var target) &&
                         !ReferenceEquals(source, target) && target.TryRoute(response);
            _routeEvents.Add($"{source.SubscriptionId}->{response.SubscriptionId}:{routed}");
            return routed;
        }
    }

    public Uri Endpoint { get; } = new("opc.tcp://fake/");

    public ValueTask<DriverResult<OpcUaPublishResponse>> PublishAsync(
        ReadOnlyMemory<OpcUaSubscriptionAcknowledgement> acknowledgements, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _publishCalls);
        lock (_gate)
        {
            _acknowledgementBatches.Add(acknowledgements.ToArray());
        }

        return _publishes.Reader.ReadAsync(cancellationToken);
    }

    public ValueTask<DriverResult<OpcUaPublishResponse>> RepublishAsync(
        uint subscriptionId, uint retransmitSequenceNumber, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _republishRequests.Add(retransmitSequenceNumber);
            if (_republishes.TryGetValue(retransmitSequenceNumber, out var response))
                return ValueTask.FromResult(response);
        }

        return ValueTask.FromResult(DriverResult<OpcUaPublishResponse>.Failure(new DriverError(
            DriverErrorKind.Device, "UA_REPUBLISH_NOT_SCRIPTED", "Republish response was not scripted.",
            Operation: DriverOperation.Republish,
            Protocol: OpcUaProtocol.Name, Endpoint: Endpoint.ToString(), Retry: DriverRetryDisposition.DoNotRetry,
            Connection: DriverConnectionDisposition.Healthy)));
    }

    public ValueTask<DriverResult<OpcUaSubscriptionInfo>> RecreateManagedSubscriptionAsync(
        OpcUaManagedSubscriptionPlan plan, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _recreateCalls);
        return _recreations.Reader.ReadAsync(cancellationToken);
    }

    public void QueuePublish(OpcUaPublishResponse response)
    {
        QueuePublish(DriverResult<OpcUaPublishResponse>.Success(response));
    }

    public void QueuePublish(DriverResult<OpcUaPublishResponse> response)
    {
        _publishes.Writer.TryWrite(response);
    }

    public void QueueRecreate(DriverResult<OpcUaSubscriptionInfo> response)
    {
        _recreations.Writer.TryWrite(response);
    }

    public void SetRepublish(uint sequenceNumber, DriverResult<OpcUaPublishResponse> response)
    {
        lock (_gate)
        {
            _republishes[sequenceNumber] = response;
        }
    }
}

internal sealed class SubscriptionServiceServer : IAsyncDisposable
{
    private readonly bool _dropReadResponse;
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly TaskCompletionSource _publishReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _releasePublish = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private int _publishResponseCount;
    private Task? _task;

    public ConcurrentQueue<(uint TypeId, uint SequenceNumber)> Requests { get; } = new();
    public uint OpenSequenceNumber { get; private set; }

    public SubscriptionServiceServer(bool dropReadResponse = false)
    {
        _dropReadResponse = dropReadResponse;
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public Task PublishReceived => _publishReceived.Task;

    /// <summary>Raw bytes of the most recent TranslateBrowsePaths or HistoryRead request message.</summary>
    public byte[] LastServiceRequest { get; private set; } = [];

    /// <summary>When set, Publish responses also carry one EventNotificationList with one event.</summary>
    public bool PublishEvents { get; init; }

    /// <summary>When set, CreateMonitoredItems returns an EventFilterResult with these select clause codes.</summary>
    public uint[]? EventSelectClauseResults { get; init; }

    /// <summary>When set, Call requests get no response (the method outcome is unknown to the client).</summary>
    public bool DropCallResponse { get; init; }

    /// <summary>When set, Read requests are answered with a ServiceFault carrying this StatusCode.</summary>
    public uint? ReadServiceFault { get; init; }

    /// <summary>When set, the server closes the connection instead of answering a Read request.</summary>
    public bool CloseOnRead { get; init; }

    /// <summary>When set, the server accepts a new connection after each one ends (Session reactivation tests).</summary>
    public bool AcceptMore { get; init; }

    /// <summary>When set, ActivateSession requests get no response (as when the response is lost).</summary>
    public bool DropActivateResponse { get; set; }

    /// <summary>When set, ActivateSession requests are answered with a ServiceFault carrying this StatusCode.</summary>
    public uint? ActivateServiceFault { get; set; }

    /// <summary>The StatusCode of the ServiceFault that answers CreateSession (the test server builds no session).</summary>
    public uint CreateSessionServiceFault { get; set; } = 0x80560000u;

    /// <summary>Connections that ended.</summary>
    public int ConnectionsClosed => Volatile.Read(ref _connectionsClosed);

    private int _connectionsClosed;

    public async ValueTask DisposeAsync()
    {
        _listener.Stop();
        _releasePublish.TrySetResult();
        if (_task is not null)
            try
            {
                await _task;
            }
            catch (SocketException)
            {
            }
            catch (InvalidOperationException) when (AcceptMore)
            {
                // The accept loop met the stopped listener.
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException)
            {
            }
    }

    public void ReleasePublish()
    {
        _releasePublish.TrySetResult();
    }

    public Task StartAsync()
    {
        _listener.Start();
        _task = Task.Run(RunAsync);
        return Task.CompletedTask;
    }

    private async Task RunAsync()
    {
        do
        {
            try
            {
                await ServeConnectionAsync();
            }
            catch (IOException) when (AcceptMore)
            {
            }
            catch (EndOfStreamException) when (AcceptMore)
            {
            }
            finally
            {
                Interlocked.Increment(ref _connectionsClosed);
            }
        } while (AcceptMore);
    }

    private async Task ServeConnectionAsync()
    {
        using var client = await _listener.AcceptTcpClientAsync();
        using var stream = client.GetStream();
        _ = await ReadMessage(stream);
        using (var ack = new UaBinaryWriter())
        {
            ack.WriteBytes("ACKF"u8);
            ack.WriteUInt32(28);
            ack.WriteUInt32(0);
            ack.WriteUInt32(65_536);
            ack.WriteUInt32(65_536);
            ack.WriteUInt32(16 * 1024 * 1024);
            ack.WriteUInt32(4096);
            await stream.WriteAsync(ack.WrittenMemory);
        }

        var opn = await ReadMessage(stream);
        var open = ParseOpenRequest(opn);
        OpenSequenceNumber = open.SequenceNumber;
        await SendOpenResponseAsync(stream, open, 200);
        while (true)
        {
            var rawMessage = await ReadMessage(stream);
            if (rawMessage.AsSpan(0, 3).SequenceEqual("OPN"u8))
            {
                await SendOpenResponseAsync(stream, ParseOpenRequest(rawMessage), 201);
                continue;
            }

            var request = ParseRequest(rawMessage);
            Requests.Enqueue((request.TypeId, request.SequenceNumber));
            if (request.TypeId is OpcUaProtocol.TranslateBrowsePathsToNodeIdsRequestBinary
                or OpcUaProtocol.HistoryReadRequestBinary or OpcUaProtocol.CallRequestBinary
                or OpcUaProtocol.CreateMonitoredItemsRequestBinary)
                LastServiceRequest = rawMessage;
            if (DropCallResponse && request.TypeId == OpcUaProtocol.CallRequestBinary) continue;
            if (_dropReadResponse && request.TypeId == OpcUaProtocol.ReadRequestBinary) continue;
            if (CloseOnRead && request.TypeId == OpcUaProtocol.ReadRequestBinary) return;
            if (ReadServiceFault is { } faultStatus && request.TypeId == OpcUaProtocol.ReadRequestBinary)
            {
                await SendServiceFaultAsync(stream, request, faultStatus);
                continue;
            }

            if (DropActivateResponse && request.TypeId == OpcUaProtocol.ActivateSessionRequestBinary) continue;
            if (ActivateServiceFault is { } activateStatus && request.TypeId == OpcUaProtocol.ActivateSessionRequestBinary)
            {
                await SendServiceFaultAsync(stream, request, activateStatus);
                continue;
            }

            if (request.TypeId == OpcUaProtocol.CreateSessionRequestBinary)
            {
                await SendServiceFaultAsync(stream, request, CreateSessionServiceFault);
                continue;
            }
            if (request.TypeId == OpcUaProtocol.PublishRequestBinary)
            {
                _publishReceived.TrySetResult();
                if (Interlocked.Increment(ref _publishResponseCount) <= 2)
                    _ = SendPublishAfterReleaseAsync(stream, request);
                continue;
            }

            await SendResponseAsync(stream, request);
        }
    }

    private async Task SendServiceFaultAsync(Stream stream, ServiceRequest request, uint status)
    {
        using var writer = new UaBinaryWriter();
        writer.WriteBytes("MSGF"u8);
        writer.WriteUInt32(0);
        writer.WriteUInt32(100);
        writer.WriteUInt32(200);
        writer.WriteUInt32(2);
        writer.WriteUInt32(request.RequestId);
        writer.WriteNodeId(IndustrialNodeId.Numeric(0, OpcUaProtocol.ServiceFaultBinary));
        writer.WriteDateTime(DateTime.UtcNow);
        writer.WriteUInt32(request.Handle);
        writer.WriteUInt32(status);
        writer.WriteByte(0);
        writer.WriteInt32(0);
        writer.WriteExtensionObject(null);
        writer.PatchUInt32(4, (uint)writer.Position);
        await _writeGate.WaitAsync();
        try
        {
            await stream.WriteAsync(writer.WrittenMemory);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task SendPublishAfterReleaseAsync(Stream stream, ServiceRequest request)
    {
        await _releasePublish.Task;
        await SendResponseAsync(stream, request);
    }

    private async Task SendResponseAsync(Stream stream, ServiceRequest request)
    {
        var responseType = request.TypeId switch
        {
            OpcUaProtocol.CreateSubscriptionRequestBinary => OpcUaProtocol.CreateSubscriptionResponseBinary,
            OpcUaProtocol.CreateMonitoredItemsRequestBinary => OpcUaProtocol.CreateMonitoredItemsResponseBinary,
            OpcUaProtocol.ModifyMonitoredItemsRequestBinary => OpcUaProtocol.ModifyMonitoredItemsResponseBinary,
            OpcUaProtocol.SetMonitoringModeRequestBinary => OpcUaProtocol.SetMonitoringModeResponseBinary,
            OpcUaProtocol.DeleteMonitoredItemsRequestBinary => OpcUaProtocol.DeleteMonitoredItemsResponseBinary,
            OpcUaProtocol.DeleteSubscriptionsRequestBinary => OpcUaProtocol.DeleteSubscriptionsResponseBinary,
            OpcUaProtocol.ReadRequestBinary => OpcUaProtocol.ReadResponseBinary,
            OpcUaProtocol.PublishRequestBinary => OpcUaProtocol.PublishResponseBinary,
            OpcUaProtocol.RepublishRequestBinary => OpcUaProtocol.RepublishResponseBinary,
            OpcUaProtocol.ActivateSessionRequestBinary => OpcUaProtocol.ActivateSessionResponseBinary,
            OpcUaProtocol.TranslateBrowsePathsToNodeIdsRequestBinary =>
                OpcUaProtocol.TranslateBrowsePathsToNodeIdsResponseBinary,
            OpcUaProtocol.HistoryReadRequestBinary => OpcUaProtocol.HistoryReadResponseBinary,
            OpcUaProtocol.CallRequestBinary => OpcUaProtocol.CallResponseBinary,
            _ => throw new InvalidDataException($"Unexpected test service {request.TypeId}.")
        };
        using var writer = new UaBinaryWriter();
        writer.WriteBytes("MSGF"u8);
        writer.WriteUInt32(0);
        writer.WriteUInt32(100);
        writer.WriteUInt32(200);
        writer.WriteUInt32(2);
        writer.WriteUInt32(request.RequestId);
        writer.WriteNodeId(IndustrialNodeId.Numeric(0, responseType));
        WriteResponseHeader(writer, request.Handle);
        switch (request.TypeId)
        {
            case OpcUaProtocol.CreateSubscriptionRequestBinary:
                writer.WriteUInt32(500);
                writer.WriteDouble(1000);
                writer.WriteUInt32(60);
                writer.WriteUInt32(10);
                break;
            case OpcUaProtocol.CreateMonitoredItemsRequestBinary:
                writer.WriteInt32(1);
                writer.WriteUInt32(0);
                writer.WriteUInt32(600);
                writer.WriteDouble(1000);
                writer.WriteUInt32(1);
                if (EventSelectClauseResults is { } codes)
                {
                    using var filterResult = new UaBinaryWriter(64);
                    filterResult.WriteInt32(codes.Length);
                    foreach (var code in codes) filterResult.WriteUInt32(code);
                    filterResult.WriteInt32(0);
                    filterResult.WriteInt32(0);
                    filterResult.WriteInt32(0);
                    writer.WriteExtensionObject(new DriverExtensionObject(
                        new IndustrialExpandedNodeId(IndustrialNodeId.Numeric(0, OpcUaProtocol.EventFilterResultBinary)),
                        DriverExtensionObjectEncoding.Binary, filterResult.WrittenSpan));
                }
                else
                {
                    writer.WriteExtensionObject(null);
                }

                writer.WriteInt32(0);
                break;
            case OpcUaProtocol.ModifyMonitoredItemsRequestBinary:
                writer.WriteInt32(1);
                writer.WriteUInt32(0);
                writer.WriteDouble(1000);
                writer.WriteUInt32(1);
                writer.WriteExtensionObject(null);
                writer.WriteInt32(0);
                break;
            case OpcUaProtocol.SetMonitoringModeRequestBinary:
            case OpcUaProtocol.DeleteMonitoredItemsRequestBinary:
            case OpcUaProtocol.DeleteSubscriptionsRequestBinary:
                writer.WriteInt32(1);
                writer.WriteUInt32(0);
                writer.WriteInt32(0);
                break;
            case OpcUaProtocol.ReadRequestBinary:
                writer.WriteInt32(1);
                OpcUaBinaryCodec.WriteDataValue(writer,
                    new DriverSample(DriverValue.FromFloat64(12.5), DriverQuality.Good, DateTime.UtcNow,
                        DateTime.UtcNow));
                writer.WriteInt32(0);
                break;
            case OpcUaProtocol.PublishRequestBinary: WritePublishBody(writer, PublishEvents); break;
            case OpcUaProtocol.TranslateBrowsePathsToNodeIdsRequestBinary:
                // Path 0 resolves to i=2258 (Server_ServerStatus_CurrentTime); path 1 matches nothing.
                writer.WriteInt32(2);
                writer.WriteUInt32(0);
                writer.WriteInt32(1);
                writer.WriteExpandedNodeId(new IndustrialExpandedNodeId(IndustrialNodeId.Numeric(0, 2258)));
                writer.WriteUInt32(uint.MaxValue);
                writer.WriteUInt32(0x806F0000); // Bad_NoMatch
                writer.WriteInt32(0);
                writer.WriteInt32(0);
                break;
            case OpcUaProtocol.HistoryReadRequestBinary:
                WriteHistoryReadBody(writer, LastServiceRequest);
                break;
            case OpcUaProtocol.CallRequestBinary:
                WriteCallBody(writer, LastServiceRequest);
                break;
            case OpcUaProtocol.RepublishRequestBinary: WriteNotificationMessage(writer, PublishEvents); break;
            case OpcUaProtocol.ActivateSessionRequestBinary:
                writer.WriteNullByteString();
                writer.WriteInt32(0);
                writer.WriteInt32(0);
                break;
        }

        writer.PatchUInt32(4, (uint)writer.Position);
        await _writeGate.WaitAsync();
        try
        {
            if (request.TypeId == OpcUaProtocol.ReadRequestBinary)
                await WriteChunkedMessageAsync(stream, writer.WrittenMemory);
            else await stream.WriteAsync(writer.WrittenMemory);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private static async Task SendOpenResponseAsync(Stream stream, ServiceRequest request, uint tokenId)
    {
        using var writer = new UaBinaryWriter();
        writer.WriteBytes("OPNF"u8);
        writer.WriteUInt32(0);
        writer.WriteUInt32(100);
        writer.WriteString(OpcUaProtocol.SecurityPolicyNone);
        writer.WriteNullByteString();
        writer.WriteNullByteString();
        writer.WriteUInt32(1);
        writer.WriteUInt32(request.RequestId);
        writer.WriteNodeId(IndustrialNodeId.Numeric(0, OpcUaProtocol.OpenSecureChannelResponseBinary));
        WriteResponseHeader(writer, request.Handle);
        writer.WriteUInt32(0);
        writer.WriteUInt32(100);
        writer.WriteUInt32(tokenId);
        writer.WriteDateTime(DateTime.UtcNow);
        writer.WriteUInt32(1_200);
        writer.WriteNullByteString();
        writer.PatchUInt32(4, (uint)writer.Position);
        await stream.WriteAsync(writer.WrittenMemory);
    }

    private static async Task WriteChunkedMessageAsync(Stream stream, ReadOnlyMemory<byte> message)
    {
        const int payloadOffset = 24;
        var firstPayloadLength = 5;
        var first = new byte[payloadOffset + firstPayloadLength];
        message.Span[..payloadOffset].CopyTo(first);
        first[3] = (byte)'C';
        BinaryPrimitives.WriteUInt32LittleEndian(first.AsSpan(4, 4), (uint)first.Length);
        message.Span.Slice(payloadOffset, firstPayloadLength).CopyTo(first.AsSpan(payloadOffset));
        var second = new byte[payloadOffset + message.Length - payloadOffset - firstPayloadLength];
        message.Span[..payloadOffset].CopyTo(second);
        second[3] = (byte)'F';
        BinaryPrimitives.WriteUInt32LittleEndian(second.AsSpan(4, 4), (uint)second.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(second.AsSpan(16, 4), 3);
        message.Span[(payloadOffset + firstPayloadLength)..].CopyTo(second.AsSpan(payloadOffset));
        await stream.WriteAsync(first);
        await stream.WriteAsync(second);
    }

    private static void WritePublishBody(UaBinaryWriter writer, bool includeEvent = false)
    {
        writer.WriteUInt32(500);
        writer.WriteInt32(1);
        writer.WriteUInt32(1);
        writer.WriteBoolean(false);
        WriteNotificationMessage(writer, includeEvent);
        writer.WriteInt32(1);
        writer.WriteUInt32(0x807A0000);
        writer.WriteInt32(0);
    }

    private static void WriteNotificationMessage(UaBinaryWriter writer, bool includeEvent = false)
    {
        using var notification = new UaBinaryWriter();
        notification.WriteInt32(1);
        notification.WriteUInt32(77);
        OpcUaBinaryCodec.WriteDataValue(notification,
            new DriverSample(DriverValue.FromFloat64(42.5), DriverQuality.Good, DateTime.UtcNow, DateTime.UtcNow));
        notification.WriteInt32(0);
        writer.WriteUInt32(1);
        writer.WriteDateTime(DateTime.UtcNow);
        writer.WriteInt32(includeEvent ? 2 : 1);
        writer.WriteExtensionObject(new DriverExtensionObject(
            new IndustrialExpandedNodeId(IndustrialNodeId.Numeric(0, OpcUaProtocol.DataChangeNotificationBinary)),
            DriverExtensionObjectEncoding.Binary, notification.WrittenSpan));
        if (!includeEvent) return;

        // EventNotificationList (i=916): one EventFieldList for client handle 88.
        using var events = new UaBinaryWriter();
        events.WriteInt32(1);
        events.WriteUInt32(88);
        events.WriteInt32(5);
        OpcUaBinaryCodec.WriteVariant(events, DriverValue.FromBytes(new byte[] { 9, 9 }));
        OpcUaBinaryCodec.WriteVariant(events, DriverValue.FromNodeId(IndustrialNodeId.Numeric(0, 2915)));
        OpcUaBinaryCodec.WriteVariant(events, DriverValue.FromString("Boiler"));
        OpcUaBinaryCodec.WriteVariant(events, DriverValue.FromUInt16(700));
        OpcUaBinaryCodec.WriteVariant(events, DriverValue.FromNodeId(IndustrialNodeId.String(1, "Cond")));
        writer.WriteExtensionObject(new DriverExtensionObject(
            new IndustrialExpandedNodeId(IndustrialNodeId.Numeric(0, OpcUaProtocol.EventNotificationListBinary)),
            DriverExtensionObjectEncoding.Binary, events.WrittenSpan));
    }

    private static ServiceRequest ParseRequest(byte[] message)
    {
        var reader = new UaBinaryReader(message.AsSpan(8));
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        var sequenceNumber = reader.ReadUInt32();
        var requestId = reader.ReadUInt32();
        var typeId = reader.ReadNodeId();
        _ = reader.ReadNodeId();
        _ = reader.ReadDateTime();
        var handle = reader.ReadUInt32();
        return new ServiceRequest(requestId, handle, checked((uint)typeId.NumericIdentifier), sequenceNumber);
    }

    private static ServiceRequest ParseOpenRequest(byte[] message)
    {
        var reader = new UaBinaryReader(message.AsSpan(8));
        _ = reader.ReadUInt32();
        _ = reader.ReadString();
        _ = reader.ReadByteString();
        _ = reader.ReadByteString();
        var sequenceNumber = reader.ReadUInt32();
        var requestId = reader.ReadUInt32();
        var typeId = reader.ReadNodeId();
        _ = reader.ReadNodeId();
        _ = reader.ReadDateTime();
        var handle = reader.ReadUInt32();
        return new ServiceRequest(requestId, handle, checked((uint)typeId.NumericIdentifier), sequenceNumber);
    }

    /// <summary>Returns the service body of a request message, positioned after its RequestHeader.</summary>
    internal static int RequestBodyOffset(byte[] message)
    {
        var reader = new UaBinaryReader(message.AsSpan(8));
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadNodeId();
        _ = reader.ReadNodeId();
        _ = reader.ReadDateTime();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadString();
        _ = reader.ReadUInt32();
        _ = reader.ReadExtensionObject();
        return message.Length - reader.Remaining;
    }

    // ns=1;s=Add(Int32, Int32) -> Int32 sum. Wrong argument types give Bad_InvalidArgument with a per-argument
    // Bad_TypeMismatch; any other method gives Bad_MethodInvalid.
    private static void WriteCallBody(UaBinaryWriter writer, byte[] request)
    {
        var reader = new UaBinaryReader(request.AsSpan(RequestBodyOffset(request)));
        var count = reader.ReadInt32();
        writer.WriteInt32(count);
        for (var i = 0; i < count; i++)
        {
            _ = reader.ReadNodeId();
            var method = reader.ReadNodeId();
            var inputCount = reader.ReadInt32();
            var inputs = new DriverValue[inputCount];
            for (var a = 0; a < inputCount; a++) inputs[a] = OpcUaBinaryCodec.ReadVariant(ref reader);
            if (method.Equals(IndustrialNodeId.Numeric(0, 3875)) || method.Equals(IndustrialNodeId.Numeric(0, 9111)))
            {
                writer.WriteUInt32(0);
                writer.WriteInt32(0);
                writer.WriteInt32(0);
                writer.WriteInt32(0);
                continue;
            }

            if (!method.Equals(IndustrialNodeId.String(1, "Add")))
            {
                writer.WriteUInt32(0x80750000); // Bad_MethodInvalid
                writer.WriteInt32(0);
                writer.WriteInt32(0);
                writer.WriteInt32(0);
                continue;
            }

            var typed = inputs.Length == 2 && inputs.All(static v => v.Type == IndustrialDataType.Int32);
            writer.WriteUInt32(typed ? 0u : 0x80AB0000); // Bad_InvalidArgument
            writer.WriteInt32(inputs.Length);
            foreach (var input in inputs) writer.WriteUInt32(input.Type == IndustrialDataType.Int32 ? 0u : 0x80740000);
            writer.WriteInt32(0);
            if (typed)
            {
                writer.WriteInt32(1);
                OpcUaBinaryCodec.WriteVariant(writer, DriverValue.FromInt32(inputs[0].AsInt32() + inputs[1].AsInt32()));
            }
            else
            {
                writer.WriteInt32(0);
            }
        }

        writer.WriteInt32(0);
    }

    // First read of a node returns two values and continuation point {1,2,3}; the follow-up with that
    // continuation point returns one value and none. A release request returns no data.
    private static void WriteHistoryReadBody(UaBinaryWriter writer, byte[] request)
    {
        var reader = new UaBinaryReader(request.AsSpan(RequestBodyOffset(request)));
        _ = reader.ReadExtensionObject();
        _ = reader.ReadUInt32();
        var release = reader.ReadBoolean();
        var count = reader.ReadInt32();
        writer.WriteInt32(count);
        for (var i = 0; i < count; i++)
        {
            _ = reader.ReadNodeId();
            _ = reader.ReadString();
            _ = reader.ReadQualifiedName();
            var continuation = reader.ReadByteString();
            writer.WriteUInt32(0);
            var first = continuation is null || continuation.Length == 0;
            if (release || !first)
                writer.WriteNullByteString();
            else
                writer.WriteByteString(new byte[] { 1, 2, 3 });
            if (release)
            {
                writer.WriteExtensionObject(null);
                continue;
            }

            using var data = new UaBinaryWriter(256);
            var values = first ? new[] { 1.5, 2.5 } : new[] { 3.5 };
            data.WriteInt32(values.Length);
            for (var v = 0; v < values.Length; v++)
                OpcUaBinaryCodec.WriteDataValue(data, new DriverSample(DriverValue.FromFloat64(values[v]),
                    DriverQuality.Good, new DateTime(2026, 1, 1, 0, 0, v + (first ? 0 : 2), DateTimeKind.Utc),
                    new DateTime(2026, 1, 1, 0, 0, v + (first ? 0 : 2), DateTimeKind.Utc)));
            writer.WriteExtensionObject(new DriverExtensionObject(
                new IndustrialExpandedNodeId(IndustrialNodeId.Numeric(0, OpcUaProtocol.HistoryDataBinary)),
                DriverExtensionObjectEncoding.Binary, data.WrittenSpan));
        }

        writer.WriteInt32(0);
    }

    private static void WriteResponseHeader(UaBinaryWriter writer, uint handle)
    {
        writer.WriteDateTime(DateTime.UtcNow);
        writer.WriteUInt32(handle);
        writer.WriteUInt32(0);
        writer.WriteByte(0);
        writer.WriteInt32(0);
        writer.WriteExtensionObject(null);
    }

    private static async Task<byte[]> ReadMessage(Stream stream)
    {
        var header = new byte[8];
        await stream.ReadExactlyAsync(header);
        var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4));
        var message = new byte[length];
        header.CopyTo(message, 0);
        if (length > 8) await stream.ReadExactlyAsync(message.AsMemory(8));
        return message;
    }

    private readonly record struct ServiceRequest(uint RequestId, uint Handle, uint TypeId, uint SequenceNumber);
}

internal enum ServerMode
{
    Normal,
    TransportError,
    InvalidAckBuffer
}

internal sealed class HandshakeServer : IAsyncDisposable
{
    private readonly bool _fragmentWrites;
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly ServerMode _mode;
    private Task? _task;

    public HandshakeServer(ServerMode mode, bool fragmentWrites)
    {
        _mode = mode;
        _fragmentWrites = fragmentWrites;
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public async ValueTask DisposeAsync()
    {
        _listener.Stop();
        if (_task is not null)
            try
            {
                await _task;
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException)
            {
            }
    }

    public Task StartAsync()
    {
        _listener.Start();
        _task = Task.Run(RunAsync);
        return Task.CompletedTask;
    }

    private async Task RunAsync()
    {
        using var client = await _listener.AcceptTcpClientAsync();
        using var stream = client.GetStream();
        var hello = await ReadMessage(stream);
        TrueType(hello, "HEL"u8);
        if (_mode == ServerMode.TransportError)
        {
            using var e = new UaBinaryWriter();
            e.WriteBytes("ERRF"u8);
            e.WriteUInt32(0);
            e.WriteUInt32(0x80800000);
            e.WriteString("Bad endpoint for test");
            e.PatchUInt32(4, (uint)e.Position);
            await Write(stream, e.WrittenMemory);
            return;
        }

        using (var ack = new UaBinaryWriter())
        {
            ack.WriteBytes("ACKF"u8);
            ack.WriteUInt32(28);
            ack.WriteUInt32(0);
            ack.WriteUInt32(_mode == ServerMode.InvalidAckBuffer ? 1024u : 65_536u);
            ack.WriteUInt32(_mode == ServerMode.InvalidAckBuffer ? 1024u : 65_536u);
            ack.WriteUInt32(16 * 1024 * 1024);
            ack.WriteUInt32(4096);
            await Write(stream, ack.WrittenMemory);
        }

        if (_mode != ServerMode.Normal) return;
        var opn = await ReadMessage(stream);
        TrueType(opn, "OPN"u8);
        var r = new UaBinaryReader(opn.AsSpan(8));
        Equal0(r.ReadUInt32());
        var policy = r.ReadString();
        if (policy != OpcUaProtocol.SecurityPolicyNone) throw new InvalidDataException("policy");
        _ = r.ReadByteString();
        _ = r.ReadByteString();
        _ = r.ReadUInt32();
        var requestId = r.ReadUInt32();
        var typeId = r.ReadNodeId();
        if (typeId.NumericIdentifier != OpcUaProtocol.OpenSecureChannelRequestBinary)
            throw new InvalidDataException("opn type");
        _ = r.ReadNodeId();
        _ = r.ReadDateTime();
        var handle = r.ReadUInt32();
        _ = r.ReadUInt32();
        _ = r.ReadString();
        _ = r.ReadUInt32();
        _ = r.ReadExtensionObject();
        _ = r.ReadUInt32();
        _ = r.ReadUInt32();
        _ = r.ReadUInt32();
        _ = r.ReadByteString();
        _ = r.ReadUInt32();
        using var w = new UaBinaryWriter();
        w.WriteBytes("OPNF"u8);
        w.WriteUInt32(0);
        w.WriteUInt32(100);
        w.WriteString(OpcUaProtocol.SecurityPolicyNone);
        w.WriteNullByteString();
        w.WriteNullByteString();
        w.WriteUInt32(77);
        w.WriteUInt32(requestId);
        w.WriteNodeId(IndustrialNodeId.Numeric(0, OpcUaProtocol.OpenSecureChannelResponseBinary));
        w.WriteDateTime(DateTime.UtcNow);
        w.WriteUInt32(handle);
        w.WriteUInt32(0);
        w.WriteByte(0);
        w.WriteInt32(0);
        w.WriteExtensionObject(null);
        w.WriteUInt32(0);
        w.WriteUInt32(100);
        w.WriteUInt32(200);
        w.WriteDateTime(DateTime.UtcNow);
        w.WriteUInt32(600_000);
        w.WriteNullByteString();
        w.PatchUInt32(4, (uint)w.Position);
        await Write(stream, w.WrittenMemory);
        try
        {
            await stream.ReadExactlyAsync(new byte[1]);
        }
        catch (EndOfStreamException)
        {
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task<byte[]> ReadMessage(Stream stream)
    {
        var h = new byte[8];
        await stream.ReadExactlyAsync(h);
        var n = (int)BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(4));
        var b = new byte[n];
        h.CopyTo(b, 0);
        if (n > 8) await stream.ReadExactlyAsync(b.AsMemory(8));
        return b;
    }

    private async Task Write(Stream stream, ReadOnlyMemory<byte> data)
    {
        if (!_fragmentWrites)
        {
            await stream.WriteAsync(data);
            return;
        }

        for (var i = 0; i < data.Length;)
        {
            var n = Math.Min(i % 5 + 1, data.Length - i);
            await stream.WriteAsync(data.Slice(i, n));
            i += n;
            await Task.Yield();
        }
    }

    private static void TrueType(byte[] b, ReadOnlySpan<byte> t)
    {
        if (!b.AsSpan(0, 3).SequenceEqual(t)) throw new InvalidDataException("message type");
    }

    private static void Equal0(uint v)
    {
        if (v != 0) throw new InvalidDataException("expected zero");
    }
}
