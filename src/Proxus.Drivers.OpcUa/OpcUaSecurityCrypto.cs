using System.Security.Cryptography;

namespace Proxus.Drivers.OpcUa;

internal sealed class OpcUaSymmetricKeySet : IDisposable
{
    internal OpcUaSymmetricKeySet(byte[] signingKey, byte[] encryptionKey, byte[] initializationVector)
    {
        SigningKey = signingKey;
        EncryptionKey = encryptionKey;
        InitializationVector = initializationVector;
    }

    internal byte[] SigningKey { get; }
    internal byte[] EncryptionKey { get; }
    internal byte[] InitializationVector { get; }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(SigningKey);
        CryptographicOperations.ZeroMemory(EncryptionKey);
        CryptographicOperations.ZeroMemory(InitializationVector);
    }
}

internal static class OpcUaSecurityCrypto
{
    internal static (OpcUaSymmetricKeySet Client, OpcUaSymmetricKeySet Server) DeriveAes256Sha256RsaPssKeys(
        ReadOnlySpan<byte> clientNonce,
        ReadOnlySpan<byte> serverNonce)
    {
        return DeriveKeys(clientNonce, serverNonce, OpcUaSecurityPolicies.Aes256Sha256RsaPss);
    }

    internal static (OpcUaSymmetricKeySet Client, OpcUaSymmetricKeySet Server) DeriveKeys(
        ReadOnlySpan<byte> clientNonce,
        ReadOnlySpan<byte> serverNonce,
        OpcUaSecurityPolicyProfile policy)
    {
        if (clientNonce.Length != policy.NonceLength || serverNonce.Length != policy.NonceLength)
            throw new CryptographicException(
                $"Both nonces must be exactly {policy.NonceLength} bytes for {policy.Uri}.");

        return (
            DeriveKeySet(serverNonce, clientNonce, policy),
            DeriveKeySet(clientNonce, serverNonce, policy));
    }

    internal static void PHashSha256(ReadOnlySpan<byte> secret, ReadOnlySpan<byte> seed, Span<byte> destination)
    {
        if (secret.IsEmpty)
            throw new ArgumentException("The PRF secret must not be empty.", nameof(secret));
        if (seed.IsEmpty)
            throw new ArgumentException("The PRF seed must not be empty.", nameof(seed));

        Span<byte> a = stackalloc byte[32];
        HMACSHA256.HashData(secret, seed, a);
        var input = new byte[32 + seed.Length];
        Span<byte> block = stackalloc byte[32];
        try
        {
            var written = 0;
            while (written < destination.Length)
            {
                a.CopyTo(input);
                seed.CopyTo(input.AsSpan(32));
                HMACSHA256.HashData(secret, input, block);
                var count = Math.Min(block.Length, destination.Length - written);
                block[..count].CopyTo(destination[written..]);
                written += count;
                HMACSHA256.HashData(secret, a, a);
                CryptographicOperations.ZeroMemory(block);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(a);
            CryptographicOperations.ZeroMemory(block);
            CryptographicOperations.ZeroMemory(input);
        }
    }

    internal static byte[] SignRsaPssSha256(RSA privateKey, ReadOnlySpan<byte> data)
    {
        return SignRsaSha256(privateKey, data, OpcUaSecurityPolicies.Aes256Sha256RsaPss);
    }

    internal static bool VerifyRsaPssSha256(RSA publicKey, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        return VerifyRsaSha256(publicKey, data, signature, OpcUaSecurityPolicies.Aes256Sha256RsaPss);
    }

    internal static byte[] SignRsaSha256(RSA privateKey, ReadOnlySpan<byte> data,
        OpcUaSecurityPolicyProfile policy)
    {
        ValidateRsaKey(privateKey, policy);
        return privateKey.SignData(data, HashAlgorithmName.SHA256, policy.AsymmetricSignaturePadding);
    }

    internal static bool VerifyRsaSha256(RSA publicKey, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature,
        OpcUaSecurityPolicyProfile policy)
    {
        ValidateRsaKey(publicKey, policy);
        return publicKey.VerifyData(data, signature, HashAlgorithmName.SHA256, policy.AsymmetricSignaturePadding);
    }

    internal static byte[] EncryptRsaOaepSha256(RSA publicKey, ReadOnlySpan<byte> plaintext)
    {
        return EncryptRsa(publicKey, plaintext, OpcUaSecurityPolicies.Aes256Sha256RsaPss);
    }

    internal static byte[] EncryptRsa(RSA publicKey, ReadOnlySpan<byte> plaintext,
        OpcUaSecurityPolicyProfile policy)
    {
        ValidateRsaKey(publicKey, policy);
        var cipherBlockSize = publicKey.KeySize / 8;
        var plainBlockSize = cipherBlockSize -
                             2 * policy.AsymmetricEncryptionHashLength - 2;
        if (plainBlockSize <= 0)
            throw new CryptographicException($"The RSA key is too small for {policy.AsymmetricEncryptionAlgorithmUri}.");
        if (plaintext.IsEmpty)
            return [];

        var blockCount = checked((plaintext.Length + plainBlockSize - 1) / plainBlockSize);
        var ciphertext = new byte[checked(blockCount * cipherBlockSize)];
        for (var offset = 0; offset < plaintext.Length; offset += plainBlockSize)
        {
            var length = Math.Min(plainBlockSize, plaintext.Length - offset);
            var block = publicKey.Encrypt(plaintext.Slice(offset, length), policy.AsymmetricEncryptionPadding);
            block.CopyTo(ciphertext, offset / plainBlockSize * cipherBlockSize);
            CryptographicOperations.ZeroMemory(block);
        }

        return ciphertext;
    }

    internal static byte[] DecryptRsaOaepSha256(RSA privateKey, ReadOnlySpan<byte> ciphertext)
    {
        return DecryptRsa(privateKey, ciphertext, OpcUaSecurityPolicies.Aes256Sha256RsaPss);
    }

    internal static byte[] DecryptRsa(RSA privateKey, ReadOnlySpan<byte> ciphertext,
        OpcUaSecurityPolicyProfile policy)
    {
        ValidateRsaKey(privateKey, policy);
        var cipherBlockSize = privateKey.KeySize / 8;
        if (ciphertext.Length % cipherBlockSize != 0)
            throw new CryptographicException("RSA-OAEP ciphertext must contain complete RSA blocks.");
        if (ciphertext.IsEmpty)
            return [];

        using var output = new MemoryStream(ciphertext.Length);
        for (var offset = 0; offset < ciphertext.Length; offset += cipherBlockSize)
        {
            var block = privateKey.Decrypt(ciphertext.Slice(offset, cipherBlockSize),
                policy.AsymmetricEncryptionPadding);
            output.Write(block);
            CryptographicOperations.ZeroMemory(block);
        }

        return output.ToArray();
    }

    internal static byte[] ComputeHmacSha256(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
    {
        return HMACSHA256.HashData(key, data);
    }

    internal static bool VerifyHmacSha256(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, ReadOnlySpan<byte> expected)
    {
        Span<byte> actual = stackalloc byte[32];
        HMACSHA256.HashData(key, data, actual);
        var valid = expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
        CryptographicOperations.ZeroMemory(actual);
        return valid;
    }

    internal static byte[] EncryptAes256Cbc(ReadOnlySpan<byte> key, ReadOnlySpan<byte> initializationVector,
        ReadOnlySpan<byte> plaintext, OpcUaSecurityPolicyProfile policy)
    {
        ValidateAesInputs(key, initializationVector, plaintext, policy);
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = [.. key];
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        return aes.EncryptCbc(plaintext, initializationVector, PaddingMode.None);
    }

    internal static byte[] DecryptAes256Cbc(ReadOnlySpan<byte> key, ReadOnlySpan<byte> initializationVector,
        ReadOnlySpan<byte> ciphertext, OpcUaSecurityPolicyProfile policy)
    {
        ValidateAesInputs(key, initializationVector, ciphertext, policy);
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = [.. key];
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        return aes.DecryptCbc(ciphertext, initializationVector, PaddingMode.None);
    }

    private static OpcUaSymmetricKeySet DeriveKeySet(
        ReadOnlySpan<byte> secret,
        ReadOnlySpan<byte> seed,
        OpcUaSecurityPolicyProfile policy)
    {
        var totalLength = checked(policy.SymmetricSignatureKeyLength + policy.SymmetricEncryptionKeyLength +
                                  policy.SymmetricInitializationVectorLength);
        var material = new byte[totalLength];
        PHashSha256(secret, seed, material);
        try
        {
            var signingKey = material[..policy.SymmetricSignatureKeyLength].ToArray();
            var encryptionOffset = policy.SymmetricSignatureKeyLength;
            var encryptionKey = material.AsSpan(encryptionOffset, policy.SymmetricEncryptionKeyLength).ToArray();
            var ivOffset = encryptionOffset + policy.SymmetricEncryptionKeyLength;
            var iv = material.AsSpan(ivOffset, policy.SymmetricInitializationVectorLength).ToArray();
            return new OpcUaSymmetricKeySet(signingKey, encryptionKey, iv);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(material);
        }
    }

    private static void ValidateRsaKey(AsymmetricAlgorithm key, OpcUaSecurityPolicyProfile policy)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.KeySize < policy.MinimumRsaKeySize || key.KeySize > policy.MaximumRsaKeySize)
            throw new CryptographicException(
                $"RSA key size must be between {policy.MinimumRsaKeySize} and {policy.MaximumRsaKeySize} bits for {policy.Uri}.");
    }

    /// <summary>Checks AES inputs against the negotiated policy's key, IV and block sizes.</summary>
    private static void ValidateAesInputs(ReadOnlySpan<byte> key, ReadOnlySpan<byte> initializationVector,
        ReadOnlySpan<byte> data, OpcUaSecurityPolicyProfile policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (key.Length != policy.SymmetricEncryptionKeyLength)
            throw new CryptographicException(
                $"{policy.Uri} requires a {policy.SymmetricEncryptionKeyLength}-byte AES key.");
        if (initializationVector.Length != policy.SymmetricInitializationVectorLength)
            throw new CryptographicException(
                $"{policy.Uri} requires a {policy.SymmetricInitializationVectorLength}-byte AES-CBC initialization vector.");
        if (data.Length % policy.SymmetricBlockSize != 0)
            throw new CryptographicException(
                $"AES-CBC input must contain complete {policy.SymmetricBlockSize}-byte blocks; OPC UA padding is applied by the message layer.");
    }
}
