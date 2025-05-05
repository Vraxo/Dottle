using System.Security.Cryptography;
using System.Text;

namespace Dottle.Services;

public class EncryptionService
{
    private const int KeySize = 256; // AES key size in bits
    private const int NonceSize = 12; // AES-GCM nonce size in bytes (96 bits)
    private const int TagSize = 16; // AES-GCM auth tag size in bytes (128 bits)
    private const int SaltSize = 16; // PBKDF2 salt size in bytes
    private const int Iterations = 350000; // PBKDF2 iteration count
    private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA512;

    public byte[] Encrypt(string plainText, string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithm,
            KeySize / 8);

        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipherText = new byte[plainBytes.Length];
        byte[] tag = new byte[TagSize];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Encrypt(nonce, plainBytes, cipherText, tag);

        // Combine salt, nonce, tag, and ciphertext for storage
        // Format: [Salt (16 bytes)][Nonce (12 bytes)][Tag (16 bytes)][Ciphertext (variable)]
        byte[] encryptedData = new byte[SaltSize + NonceSize + TagSize + cipherText.Length];
        Buffer.BlockCopy(salt, 0, encryptedData, 0, SaltSize);
        Buffer.BlockCopy(nonce, 0, encryptedData, SaltSize, NonceSize);
        Buffer.BlockCopy(tag, 0, encryptedData, SaltSize + NonceSize, TagSize);
        Buffer.BlockCopy(cipherText, 0, encryptedData, SaltSize + NonceSize + TagSize, cipherText.Length);

        // Securely clear sensitive byte arrays
        Array.Clear(key, 0, key.Length);
        Array.Clear(plainBytes, 0, plainBytes.Length);

        return encryptedData;
    }

    public string? Decrypt(byte[] encryptedData, string password)
    {
        if (encryptedData == null || encryptedData.Length < SaltSize + NonceSize + TagSize)
        {
            // Data is too short to contain required parts
            return null;
        }

        try
        {
            byte[] salt = new byte[SaltSize];
            Buffer.BlockCopy(encryptedData, 0, salt, 0, SaltSize);

            byte[] key = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                HashAlgorithm,
                KeySize / 8);

            byte[] nonce = new byte[NonceSize];
            Buffer.BlockCopy(encryptedData, SaltSize, nonce, 0, NonceSize);

            byte[] tag = new byte[TagSize];
            Buffer.BlockCopy(encryptedData, SaltSize + NonceSize, tag, 0, TagSize);

            int cipherTextLength = encryptedData.Length - SaltSize - NonceSize - TagSize;
            byte[] cipherText = new byte[cipherTextLength];
            Buffer.BlockCopy(encryptedData, SaltSize + NonceSize + TagSize, cipherText, 0, cipherTextLength);

            byte[] plainBytes = new byte[cipherTextLength];

            using var aesGcm = new AesGcm(key, TagSize);
            aesGcm.Decrypt(nonce, cipherText, tag, plainBytes);

            // Securely clear sensitive byte arrays
            Array.Clear(key, 0, key.Length);
            Array.Clear(cipherText, 0, cipherText.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            // Decryption failed - likely wrong password or corrupted data
            return null;
        }
        catch (Exception)
        {
            // Handle other potential exceptions during decryption
            return null;
        }
    }
}