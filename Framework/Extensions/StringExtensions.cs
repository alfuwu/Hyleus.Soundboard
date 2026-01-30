using System.Linq;
using System.Numerics;
using System.Text;

namespace Hyleus.Soundboard.Framework.Extensions;
public static class StringExtensions {

    public static byte[] ToBytes(this string str, Encoding encoding = null) => (encoding ?? Encoding.ASCII).GetBytes(str);

    /// <summary>
    /// Computes a 64-bit FNV-1a hash for the given string.
    /// Same input -> same ulong. Uses UTF-8 bytes.
    /// </summary>
    public static ulong ToUInt64(this string str) {
        const ulong offset = 1469598103934665603UL;   // FNV offset basis
        const ulong prime = 1099511628211UL;          // FNV prime

        ulong hash = offset;
        byte[] bytes = str.Normalize().ToBytes(Encoding.UTF8);

        unchecked {
            foreach (byte b in bytes) {
                hash ^= b;
                hash *= prime;
            }
        }

        return hash;
    }
    
    public static string Encode(this string s, int alphabetSize = 6666) {
        byte[] bytes = s.ToBytes(Encoding.UTF8);
        BigInteger big = new(bytes, true, true);
        StringBuilder sb = new();

        if (big.IsZero)
            sb.Append((char)(33 + alphabetSize));

        while (big > 0) {
            int remainder = (int)(big % alphabetSize);
            sb.Insert(0, (char)(remainder + 33 + alphabetSize));
            big /= alphabetSize;
        }
        return sb.ToString();
    }

    public static string Decode(this string s, int alphabetSize = 6666) {
        BigInteger big = new(0);
        foreach (char c in s)
            big = big * alphabetSize + (c - 33 - alphabetSize);
        return big.ToByteArray(true, true).Decode(Encoding.UTF8);
    }
}
