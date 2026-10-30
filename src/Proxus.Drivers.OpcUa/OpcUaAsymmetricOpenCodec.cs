using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Proxus.Drivers.OpcUa;

internal readonly record struct OpcUaDecodedAsymmetricOpenMessage(
    uint ChannelId,
    uint SequenceNumber,
    uint RequestId,
    byte[] Body,
    byte[] SenderCertificate,
    byte[] ReceiverCertificateThumbprint,
    string SecurityPolicyUri);

internal static class OpcUaAsymmetricOpenCodec
{
    internal static byte[] Encode(
        uint channelId,
        uint sequenceNumber,
        uint requestId,
        ReadOnlySpan<byte> body,
        X509Certificate2 senderCertificate,
        X509Certificate2 receiverCertificate)
    {
        return Encode(channelId, sequenceNumber, requestId, body, senderCertificate, receiverCertificate,
            OpcUaSecurityPolicies.Aes256Sha256RsaPss);
    }

    internal static byte[] Encode(
        uint channelId,
        uint sequenceNumber,
        uint requestId,
        ReadOnlySpan<byte> body,
        X509Certificate2 senderCertificate,
        X509Certificate2 receiverCertificate,
        OpcUaSecurityPolicyProfile policy)
    {
        using var senderKey = senderCertificate.GetRSAPrivateKey()
                              ?? throw new CryptographicException(
                                  "The OPN sender certificate requires an RSA private key.");
        using var receiverKey = receiverCertificate.GetRSAPublicKey()
                                ?? throw new CryptographicException(
                                    "The OPN receiver certificate requires an RSA public key.");
        ValidateKeySize(senderKey.KeySize, policy);
        ValidateKeySize(receiverKey.KeySize, policy);

        using var security = new UaBinaryWriter(1024);
        security.WriteString(policy.Uri);
        security.WriteByteString(senderCertificate.RawDataMemory.Span);
        security.WriteByteString(receiverCertificate.GetCertHash(HashAlgorithmName.SHA1));

        using var plain = new UaBinaryWriter(body.Length + 512);
        plain.WriteUInt32(sequenceNumber);
        plain.WriteUInt32(requestId);
        plain.WriteBytes(body);
        var cipherBlockSize = receiverKey.KeySize / 8;
        var plainBlockSize = cipherBlockSize - 2 * policy.AsymmetricEncryptionHashLength - 2;
        WritePadding(plain, senderKey.KeySize / 8, plainBlockSize, cipherBlockSize);

        var encryptedSize = checked((plain.Position + senderKey.KeySize / 8) / plainBlockSize * cipherBlockSize);
        var finalSize = checked(12 + security.Position + encryptedSize);
        using var signed = new UaBinaryWriter(finalSize);
        signed.WriteBytes("OPNF"u8);
        signed.WriteUInt32(checked((uint)finalSize));
        signed.WriteUInt32(channelId);
        signed.WriteBytes(security.WrittenSpan);
        signed.WriteBytes(plain.WrittenSpan);
        var signature = OpcUaSecurityCrypto.SignRsaSha256(senderKey, signed.WrittenSpan, policy);
        plain.WriteBytes(signature);
        CryptographicOperations.ZeroMemory(signature);
        if (plain.Position % plainBlockSize != 0)
            throw new CryptographicException("OPN plaintext is not aligned to the RSA-OAEP plaintext block size.");
        var encrypted = OpcUaSecurityCrypto.EncryptRsa(receiverKey, plain.WrittenSpan, policy);
        try
        {
            using var message = new UaBinaryWriter(finalSize);
            message.WriteBytes(signed.WrittenSpan[..(12 + security.Position)]);
            message.WriteBytes(encrypted);
            if (message.Position != finalSize)
                throw new CryptographicException("OPN encrypted size calculation did not match the encoded message.");
            return message.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encrypted);
        }
    }

    internal static OpcUaDecodedAsymmetricOpenMessage Decode(
        ReadOnlySpan<byte> message,
        X509Certificate2 receiverCertificate,
        X509Certificate2 expectedSenderCertificate)
    {
        return Decode(message, receiverCertificate, expectedSenderCertificate,
            OpcUaSecurityPolicies.Aes256Sha256RsaPss);
    }

    internal static OpcUaDecodedAsymmetricOpenMessage Decode(
        ReadOnlySpan<byte> message,
        X509Certificate2 receiverCertificate,
        X509Certificate2 expectedSenderCertificate,
        OpcUaSecurityPolicyProfile policy)
    {
        if (message.Length < 12 || !message[..4].SequenceEqual("OPNF"u8))
            throw new InvalidDataException("Expected a final OPC UA OpenSecureChannel chunk.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(message[4..8]) != message.Length)
            throw new InvalidDataException("OPN message length does not match its header.");
        var channelId = BinaryPrimitives.ReadUInt32LittleEndian(message[8..12]);
        var reader = new UaBinaryReader(message[12..]);
        var policyUri = reader.ReadString();
        var senderBytes = reader.ReadByteString() ??
                          throw new InvalidDataException("Secure OPN requires a sender certificate.");
        var receiverThumbprint = reader.ReadByteString() ??
                                 throw new InvalidDataException(
                                     "Secure OPN requires a receiver certificate thumbprint.");
        if (!string.Equals(policyUri, policy.Uri, StringComparison.Ordinal))
            throw new InvalidDataException($"Unexpected OPN SecurityPolicyUri '{policyUri}'.");
        var expectedThumbprint = receiverCertificate.GetCertHash(HashAlgorithmName.SHA1);
        if (!CryptographicOperations.FixedTimeEquals(receiverThumbprint, expectedThumbprint))
            throw new CryptographicException(
                "OPN receiver certificate thumbprint does not match the configured application certificate.");
        if (!SameCertificate(senderBytes, expectedSenderCertificate.RawDataMemory.Span))
            throw new CryptographicException(
                "OPN sender certificate does not match the configured server certificate.");

        var encryptedOffset = checked(12 + reader.Position);
        using var receiverKey = receiverCertificate.GetRSAPrivateKey()
                                ?? throw new CryptographicException(
                                    "The OPN receiver certificate requires an RSA private key.");
        using var senderKey = expectedSenderCertificate.GetRSAPublicKey()
                              ?? throw new CryptographicException(
                                  "The OPN sender certificate requires an RSA public key.");
        ValidateKeySize(receiverKey.KeySize, policy);
        ValidateKeySize(senderKey.KeySize, policy);
        var cipherBlockSize = receiverKey.KeySize / 8;
        if ((message.Length - encryptedOffset) % cipherBlockSize != 0)
            throw new InvalidDataException("OPN ciphertext contains an incomplete RSA block.");
        var decrypted = OpcUaSecurityCrypto.DecryptRsa(receiverKey, message[encryptedOffset..], policy);
        try
        {
            var signatureSize = senderKey.KeySize / 8;
            if (decrypted.Length <= signatureSize)
                throw new InvalidDataException("OPN payload is shorter than its asymmetric signature.");
            var signedPlainLength = decrypted.Length - signatureSize;
            var signature = decrypted.AsSpan(signedPlainLength, signatureSize);
            var signedData = new byte[encryptedOffset + signedPlainLength];
            try
            {
                message[..encryptedOffset].CopyTo(signedData);
                decrypted.AsSpan(0, signedPlainLength).CopyTo(signedData.AsSpan(encryptedOffset));
                if (!OpcUaSecurityCrypto.VerifyRsaSha256(senderKey, signedData, signature, policy))
                    throw new CryptographicException("OPN RSA signature verification failed.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(signedData);
            }

            var unpaddedLength = RemovePadding(decrypted.AsSpan(0, signedPlainLength), cipherBlockSize);
            if (unpaddedLength < 8) throw new InvalidDataException("OPN plaintext does not contain a SequenceHeader.");
            var sequenceNumber = BinaryPrimitives.ReadUInt32LittleEndian(decrypted.AsSpan(0, 4));
            var requestId = BinaryPrimitives.ReadUInt32LittleEndian(decrypted.AsSpan(4, 4));
            var body = decrypted.AsSpan(8, unpaddedLength - 8).ToArray();
            return new OpcUaDecodedAsymmetricOpenMessage(channelId, sequenceNumber, requestId, body, senderBytes,
                receiverThumbprint, policy.Uri);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decrypted);
        }
    }

    internal static byte[] ToUnsecuredEnvelope(OpcUaDecodedAsymmetricOpenMessage decoded)
    {
        using var writer = new UaBinaryWriter(decoded.Body.Length + decoded.SenderCertificate.Length + 128);
        writer.WriteBytes("OPNF"u8);
        writer.WriteUInt32(0);
        writer.WriteUInt32(decoded.ChannelId);
        writer.WriteString(decoded.SecurityPolicyUri);
        writer.WriteByteString(decoded.SenderCertificate);
        writer.WriteByteString(decoded.ReceiverCertificateThumbprint);
        writer.WriteUInt32(decoded.SequenceNumber);
        writer.WriteUInt32(decoded.RequestId);
        writer.WriteBytes(decoded.Body);
        writer.PatchUInt32(4, checked((uint)writer.Position));
        return writer.ToArray();
    }

    private static void WritePadding(UaBinaryWriter writer, int signatureSize, int plainBlockSize, int cipherBlockSize)
    {
        var extraPaddingBytes = cipherBlockSize > 256 ? 2 : 1;
        var remainder = (writer.Position + signatureSize + extraPaddingBytes) % plainBlockSize;
        var paddingValue = remainder == 0 ? 0 : plainBlockSize - remainder;
        for (var index = 0; index <= paddingValue; index++) writer.WriteByte((byte)(paddingValue & 0xFF));
        if (cipherBlockSize > 256) writer.WriteByte((byte)(paddingValue >> 8));
    }

    private static int RemovePadding(ReadOnlySpan<byte> plaintext, int cipherBlockSize)
    {
        if (plaintext.IsEmpty) throw new InvalidDataException("OPN plaintext has no padding.");
        var paddingValue = (int)plaintext[^1];
        var trailerSize = 1;
        if (cipherBlockSize > 256)
        {
            if (plaintext.Length < 2) throw new InvalidDataException("OPN plaintext has incomplete extended padding.");
            paddingValue = plaintext[^2] | (plaintext[^1] << 8);
            trailerSize = 2;
        }

        var paddingLength = checked(paddingValue + trailerSize);
        if (paddingLength > plaintext.Length)
            throw new InvalidDataException("OPN padding exceeds the plaintext length.");
        var lowByte = (byte)(paddingValue & 0xFF);
        var repeatedLength = paddingValue + 1;
        var repeated = plaintext.Slice(plaintext.Length - paddingLength, repeatedLength);
        foreach (var value in repeated)
            if (value != lowByte)
                throw new InvalidDataException("OPN padding bytes are inconsistent.");
        return plaintext.Length - paddingLength;
    }

    private static bool SameCertificate(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        Span<byte> leftHash = stackalloc byte[32];
        Span<byte> rightHash = stackalloc byte[32];
        SHA256.HashData(left, leftHash);
        SHA256.HashData(right, rightHash);
        return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }

    private static void ValidateKeySize(int keySize, OpcUaSecurityPolicyProfile policy)
    {
        if (keySize < policy.MinimumRsaKeySize || keySize > policy.MaximumRsaKeySize)
            throw new CryptographicException(
                $"RSA key size {keySize} is outside the {policy.MinimumRsaKeySize}..{policy.MaximumRsaKeySize} range.");
    }
}
