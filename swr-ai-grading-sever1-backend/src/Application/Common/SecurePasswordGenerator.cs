using System.Security.Cryptography;

namespace Application.Common;

public static class SecurePasswordGenerator
{
    public static string Generate(int length = 10)
    {
        const string upper = "ABCDEFGHJKLMNOPQRSTUVWXYZ";
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string special = "@#$%&*";
        const string all = upper + lower + digits + special;

        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);

        var chars = new char[length];
        // Ensure at least one of each category
        chars[0] = upper[bytes[0] % upper.Length];
        chars[1] = lower[bytes[1] % lower.Length];
        chars[2] = digits[bytes[2] % digits.Length];
        chars[3] = special[bytes[3] % special.Length];

        for (var i = 4; i < length; i++)
            chars[i] = all[bytes[i] % all.Length];

        // Shuffle
        var shuffleBytes = new byte[length];
        RandomNumberGenerator.Fill(shuffleBytes);
        for (var i = length - 1; i > 0; i--)
        {
            var j = shuffleBytes[i] % (i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
