using System;
using System.Linq;
using System.Text;

namespace OwO_Maker.Core
{
    /// <summary>
    /// Decoding/validation of a NosTale character name read from client memory.
    /// The client stores it as a Delphi AnsiString: 4-byte length prefix at (ptr - 4),
    /// ASCII payload at ptr. A garbage length or non-printable payload means the
    /// pointer/offset was wrong (client version drift) — callers must treat that as
    /// "name unavailable", not as a name.
    /// </summary>
    public static class CharacterName
    {
        public const int MaxLength = 32;

        public static bool TryDecode(int length, byte[] data, out string name)
        {
            name = string.Empty;

            if (length < 1 || length > MaxLength)
                return false;

            if (data == null || data.Length < length)
                return false;

            var candidate = Encoding.ASCII.GetString(data, 0, length);

            if (!IsPlausible(candidate))
                return false;

            name = candidate;
            return true;
        }

        /// <summary>
        /// Same as TryDecode, but for a Delphi UnicodeString (UTF-16, length prefix counts
        /// characters). Newer Delphi compilers store strings this way.
        /// </summary>
        public static bool TryDecodeUtf16(int length, byte[] data, out string name)
        {
            name = string.Empty;

            if (length < 1 || length > MaxLength)
                return false;

            if (data == null || data.Length < length * 2)
                return false;

            var candidate = Encoding.Unicode.GetString(data, 0, length * 2);

            if (!IsPlausible(candidate))
                return false;

            name = candidate;
            return true;
        }

        public static bool IsPlausible(string name)
        {
            return !string.IsNullOrEmpty(name)
                && name.Length <= MaxLength
                && name.All(c => c > ' ' && c < (char)0x7F);
        }
    }
}
