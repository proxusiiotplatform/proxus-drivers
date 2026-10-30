using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Proxus.Drivers.OpcUa;

internal static class OpcUaSymmetricMessageCodec
{
    internal static byte[] Encode(
        ReadOnlySpan<byte> messageType,
        uint channelId,
        uint tokenId,
        uint sequenceNumber,
        uint requestId,
        ReadOnlySpan<byte> body,
        OpcUaMessageSecurityMode mode,
        OpcUaSymmetricKeySet keys)
    {
        return Encode(messageType, channelId, tokenId, sequenceNumber, requestId, body, mode, keys,
            OpcUaSecurityPolicies.Aes256Sha256RsaPss);
    }

    internal static byte[] Encode(
        ReadOnlySpan<byte> messageType,
        uint channelId,
        uint tokenId,
        uint sequenceNumber,
        uint requestId,
        ReadOnlySpan<byte> body,
        OpcUaMessageSecurityMode mode,
        OpcUaSymmetricKeySet keys,
        OpcUaSecurityPolicyProfile policy)
    {
        if (messageType.Length != 4 ||
            (!messageType[..3].SequenceEqual("MSG"u8) && !messageType[..3].SequenceEqual("CLO"u8)))
            throw new ArgumentException("Symmetric SecureConversation requires a four-byte MSG or CLO message type.",
                nameof(messageType));
        if (messageType[3] is not ((byte)'F' or (byte)'C'))
            throw new ArgumentException("Symmetric SecureConversation chunk type must be C or F.", nameof(messageType));
        if (mode is not (OpcUaMessageSecurityMode.Sign or OpcUaMessageSecurityMode.SignAndEncrypt))
            throw new ArgumentOutOfRangeException(nameof(mode));

        using var plain = new UaBinaryWriter(body.Length + 64);
        plain.WriteUInt32(sequenceNumber);
        plain.WriteUInt32(requestId);
        plain.WriteBytes(body);
        if (mode == OpcUaMessageSecurityMode.SignAndEncrypt) WritePadding(plain, policy);
        var protectedLength =
            checked(plain.Position + policy.SymmetricSignatureLength);
        var finalSize = checked(16 + protectedLength);
        using var signed = new UaBinaryWriter(finalSize);
        signed.WriteBytes(messageType);
        signed.WriteUInt32(checked((uint)finalSize));
        signed.WriteUInt32(channelId);
        signed.WriteUInt32(tokenId);
        signed.WriteBytes(plain.WrittenSpan);
        var signature = OpcUaSecurityCrypto.ComputeHmacSha256(keys.SigningKey, signed.WrittenSpan);
        plain.WriteBytes(signature);
        CryptographicOperations.ZeroMemory(signature);

        var protectedBytes = mode == OpcUaMessageSecurityMode.SignAndEncrypt
            ? OpcUaSecurityCrypto.EncryptAes256Cbc(keys.EncryptionKey, keys.InitializationVector, plain.WrittenSpan,
                policy)
            : [.. plain.WrittenSpan];
        try
        {
            using var result = new UaBinaryWriter(finalSize);
            result.WriteBytes(signed.WrittenSpan[..16]);
            result.WriteBytes(protectedBytes);
            return result.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
    }

    internal static byte[] DecodeToUnsecuredEnvelope(
        ReadOnlySpan<byte> message,
        OpcUaMessageSecurityMode mode,
        OpcUaSymmetricKeySet keys)
    {
        return DecodeToUnsecuredEnvelope(message, mode, keys, OpcUaSecurityPolicies.Aes256Sha256RsaPss);
    }

    internal static byte[] DecodeToUnsecuredEnvelope(
        ReadOnlySpan<byte> message,
        OpcUaMessageSecurityMode mode,
        OpcUaSymmetricKeySet keys,
        OpcUaSecurityPolicyProfile policy)
    {
        if (message.Length < 16 || (!message[..3].SequenceEqual("MSG"u8) && !message[..3].SequenceEqual("CLO"u8)))
            throw new InvalidDataException("Expected a symmetric OPC UA SecureConversation chunk.");
        if (message[3] is not ((byte)'F' or (byte)'C'))
            throw new InvalidDataException("SecureConversation chunk type must be C or F.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(message[4..8]) != message.Length)
            throw new InvalidDataException("SecureConversation message length does not match its header.");
        if (mode is not (OpcUaMessageSecurityMode.Sign or OpcUaMessageSecurityMode.SignAndEncrypt))
            throw new ArgumentOutOfRangeException(nameof(mode));

        var protectedBytes = mode == OpcUaMessageSecurityMode.SignAndEncrypt
            ? OpcUaSecurityCrypto.DecryptAes256Cbc(keys.EncryptionKey, keys.InitializationVector, message[16..],
                policy)
            : [.. message[16..]];
        try
        {
            var signatureLength = policy.SymmetricSignatureLength;
            if (protectedBytes.Length < 8 + signatureLength)
                throw new InvalidDataException(
                    "SecureConversation payload is shorter than its SequenceHeader and signature.");
            var signedPlainLength = protectedBytes.Length - signatureLength;
            var signedData = new byte[16 + signedPlainLength];
            try
            {
                message[..16].CopyTo(signedData);
                protectedBytes.AsSpan(0, signedPlainLength).CopyTo(signedData.AsSpan(16));
                if (!OpcUaSecurityCrypto.VerifyHmacSha256(keys.SigningKey, signedData,
                        protectedBytes.AsSpan(signedPlainLength)))
                    throw new CryptographicException("SecureConversation HMAC-SHA256 verification failed.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(signedData);
            }

            var unpaddedLength = mode == OpcUaMessageSecurityMode.SignAndEncrypt
                ? RemovePadding(protectedBytes.AsSpan(0, signedPlainLength))
                : signedPlainLength;
            if (unpaddedLength < 8)
                throw new InvalidDataException("SecureConversation plaintext has no SequenceHeader.");
            using var result = new UaBinaryWriter(message.Length);
            result.WriteBytes(message[..4]);
            result.WriteUInt32(0);
            result.WriteBytes(message.Slice(8, 8));
            result.WriteBytes(protectedBytes.AsSpan(0, unpaddedLength));
            result.PatchUInt32(4, checked((uint)result.Position));
            return result.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
    }

    private static void WritePadding(UaBinaryWriter writer, OpcUaSecurityPolicyProfile policy)
    {
        var remainder = (writer.Position + policy.SymmetricSignatureLength + 1) % policy.SymmetricBlockSize;
        var paddingValue = remainder == 0 ? 0 : policy.SymmetricBlockSize - remainder;
        for (var index = 0; index <= paddingValue; index++) writer.WriteByte((byte)paddingValue);
    }

    // Part 6 allows non-minimal padding (up to 256 bytes), so only plaintext bounds and byte consistency are checked.
    private static int RemovePadding(ReadOnlySpan<byte> plaintext)
    {
        if (plaintext.IsEmpty) throw new InvalidDataException("SecureConversation plaintext has no padding.");
        var paddingValue = plaintext[^1];
        var paddingLength = paddingValue + 1;
        if (paddingLength > plaintext.Length)
            throw new InvalidDataException("SecureConversation padding exceeds plaintext length.");
        foreach (var value in plaintext[^paddingLength..])
            if (value != paddingValue)
                throw new InvalidDataException("SecureConversation padding bytes are inconsistent.");
        return plaintext.Length - paddingLength;
    }
}
