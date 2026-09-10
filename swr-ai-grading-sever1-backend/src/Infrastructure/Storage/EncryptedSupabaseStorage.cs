using System.Security.Cryptography;
using Application.Interfaces;

namespace Infrastructure.Storage;

/// <summary>
/// Wraps an ISupabaseStorage implementation to encrypt file bytes at rest.
/// Even someone with direct Supabase dashboard / service-role access sees only
/// ciphertext, not the original document — decryption only happens here,
/// server-side, in response to an authorized download request.
///
/// Storage blob layout: [12-byte nonce][ciphertext][16-byte auth tag]
/// </summary>
public sealed class EncryptedSupabaseStorage : ISupabaseStorage
{
    private const int NonceSize = 12; // AES-GCM standard nonce size
    private const int TagSize = 16;   // AES-GCM standard tag size

    private readonly ISupabaseStorage _inner;
    private readonly byte[] _key;

    public EncryptedSupabaseStorage(SupabaseStorage inner)
    {
        _inner = inner;

        var keyBase64 = Environment.GetEnvironmentVariable("SUPABASE_STORAGE_ENCRYPTION_KEY")
            ?? throw new InvalidOperationException("SUPABASE_STORAGE_ENCRYPTION_KEY is not set.");

        _key = Convert.FromBase64String(keyBase64);
        if (_key.Length != 32)
            throw new InvalidOperationException(
                "SUPABASE_STORAGE_ENCRYPTION_KEY must decode to exactly 32 bytes (AES-256).");
    }

    public async Task UploadAsync(string path, Stream content, string contentType, CancellationToken ct = default)
    {
        using var plaintextStream = new MemoryStream();
        await content.CopyToAsync(plaintextStream, ct);
        var plaintext = plaintextStream.ToArray();

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using (var aesGcm = new AesGcm(_key, TagSize))
        {
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        // nonce || ciphertext || tag
        var blob = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, blob, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, blob, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, blob, NonceSize + ciphertext.Length, TagSize);

        using var blobStream = new MemoryStream(blob);
        await _inner.UploadAsync(path, blobStream, contentType, ct);
    }

    public Task<SupabaseFileMetadata> GetMetadataAsync(string path, CancellationToken ct = default) =>
        // Metadata (filename/content-type) is stored as a label alongside the encrypted
        // blob and isn't itself sensitive content, so it passes through unmodified.
        // Note: FileSize here reflects the encrypted blob (28 bytes larger than the
        // original plaintext, from the nonce + auth tag overhead), not the exact
        // original file size.
        _inner.GetMetadataAsync(path, ct);

    public async Task<StoredFileDownload> DownloadAsync(string path, string fileName, CancellationToken ct = default)
    {
        var encrypted = await _inner.DownloadAsync(path, fileName, ct);

        using var blobStream = new MemoryStream();
        await encrypted.Content.CopyToAsync(blobStream, ct);
        await encrypted.Content.DisposeAsync();
        var blob = blobStream.ToArray();

        if (blob.Length < NonceSize + TagSize)
            throw new InvalidOperationException($"Stored object at '{path}' is too short to be a valid encrypted blob.");

        var nonce = blob[..NonceSize];
        var tag = blob[^TagSize..];
        var ciphertext = blob[NonceSize..^TagSize];
        var plaintext = new byte[ciphertext.Length];

        using (var aesGcm = new AesGcm(_key, TagSize))
        {
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        return new StoredFileDownload
        {
            Content = new MemoryStream(plaintext),
            ContentType = encrypted.ContentType,
            FileName = encrypted.FileName
        };
    }

    public Task DeleteAsync(string path, CancellationToken ct = default) =>
        _inner.DeleteAsync(path, ct);
}