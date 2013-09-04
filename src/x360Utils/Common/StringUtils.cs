namespace x360Utils.Common {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.RegularExpressions;

    public sealed class StringUtils {
        private static readonly char[] HexCharTable = new[] {
                                                            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'
                                                            };

        public string ArrayToHex(IList<byte> value, int i = 0) {
            var c = new char[value.Count * 2];
            for(var p = 0; i < value.Count;) {
                var d = value[i++];
                c[p++] = HexCharTable[d / 0x10];
                c[p++] = HexCharTable[d % 0x10];
            }
            return new string(c);
        }

        public byte[] HexToArray(string input) {
            if(string.IsNullOrEmpty(input))
                throw new ArgumentException("Input can't be nothing!");
            if(input.Length % 2 > 0)
                throw new ArgumentException("Input must be dividable by 2!");
            if(!StringIsHex(input))
                throw new FormatException("Input must be in hex format!");
            var ret = new byte[input.Length / 2];
            for(var i = 0; i < input.Length; i += 2)
                ret[i / 2] = byte.Parse(input.Substring(i, 2), NumberStyles.HexNumber);
            return ret;
        }

        public bool StringIsHex(string input) {
            return Regex.IsMatch(input, "^[0-9A-Fa-f]+$");
        }
    }
}