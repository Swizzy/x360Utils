#region



#endregion

namespace x360Utils.Common {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;

    public static class StringUtils {
        private static readonly char[] HexCharTable = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

        public static string ArrayToHex(IList<byte> value, int i = 0, int count = -1) {
            var c = new char[value.Count * 2];
            if(count == -1)
                count = value.Count - i;
            else
                count = count + i;
            for(var p = 0; i < count;) {
                var d = value[i++];
                c[p++] = HexCharTable[d / 0x10];
                c[p++] = HexCharTable[d % 0x10];
            }
            return new string(c);
        }

        public static byte[] HexToArray(string input) {
            if(string.IsNullOrEmpty(input))
                throw new ArgumentException("Input can't be nothing!");
            input = StripNonHex(input);
            if(input.Length % 2 > 0)
                throw new ArgumentException("Input must be dividable by 2!");
            var ret = new byte[input.Length / 2];
            for(var i = 0; i < input.Length; i += 2)
                ret[i / 2] = byte.Parse(input.Substring(i, 2), NumberStyles.HexNumber);
            return ret;
        }

        public static bool StringIsHex(string input) { return Regex.IsMatch(input, "^[0-9A-Fa-f]+$"); }

        public static string StripNonHex(string input) { return Regex.Replace(StripHexIdentifier(input), "[^A-Fa-f0-9]", ""); }

        public static string StripHexIdentifier(string input) { return Regex.Replace(input, "^0x", ""); }

        public static bool IsIPv4(string input) { return Regex.IsMatch(input, "^([0-9]{1,3}\\.){3}[0-9]{1,3}$"); }

        public static string GetAciiString(ref byte[] data, int offset, int length, bool trim = false) { return trim ? Encoding.ASCII.GetString(data, offset, length).Trim() : Encoding.ASCII.GetString(data, offset, length); }
    }
}