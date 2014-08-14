namespace x360Utils.CPUKey {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    public sealed class Fuse {
        public readonly UInt64[] FUSELines = new UInt64[12];

        public Fuse(string fuseFile): this(File.ReadAllLines(fuseFile)) { }

        public Fuse(ICollection<string> fuseLines) {
            if(fuseLines.Count < 12)
                throw new ArgumentException("fuseLines must be 12 or more lines!");
            var line = 0;
            foreach(var fuseLine in fuseLines) {
                if(fuseLine.Length < 10)
                    continue;
                if(!fuseLine.Substring(0, 7).Equals("fuseset", StringComparison.CurrentCultureIgnoreCase))
                    continue;
                int index;
                if(!int.TryParse(fuseLine.Substring(8, 2), out index))
                    throw new ArgumentException(string.Format("Bad fuseset index on line {0}", line));
                if(!UInt64.TryParse(fuseLine.Substring(11), NumberStyles.HexNumber, null, out FUSELines[index]))
                    throw new ArgumentException(string.Format("Bad fuseset data on line {0}", line));
                line++;
            }
        }

        public string CPUKey {
            get {
                if(CheckKey(3, 4, 5, 6))
                    return (FUSELines[3] | FUSELines[4]).ToString("X16") + (FUSELines[5] | FUSELines[6]).ToString("X16");
                if(CheckKey(3, 5))
                    return FUSELines[3].ToString("X16") + FUSELines[5].ToString("X16");
                if(CheckKey(4, 5))
                    return FUSELines[4].ToString("X16") + FUSELines[5].ToString("X16");
                if(CheckKey(3, 6))
                    return FUSELines[3].ToString("X16") + FUSELines[6].ToString("X16");
                if(CheckKey(4, 6))
                    return FUSELines[4].ToString("X16") + FUSELines[6].ToString("X16");
                throw new CpuKeyException(CpuKeyException.ExceptionTypes.NoValidKeyFound);
            }
        }

        public int CBLDV {
            get {
                var tmp = FUSELines[2].ToString("X16").LastIndexOf("F", StringComparison.Ordinal);
                return tmp >= 0 ? tmp + 1 : 0;
            }
        }

        public int CFLDV {
            get {
                UInt64 l1 = FUSELines[7], l2 = FUSELines[8];
                if(l1 == 0 && l2 == 0)
                    return 0; // LDV == 0! no need to check ;)
                var ret = 0;
                for(var i = 0; i < sizeof(UInt64) * 2; i++) {
                    if((l1 & 0xF) == 0xF)
                        ret++; // Increment LDV
                    l1 = l1 >> 4; // Move to the next 4 bits
                    if((l2 & 0xF) == 0xF)
                        ret++; // Increment LDV
                    l2 = l2 >> 4; // Move to the next 4 bits
                }
                return ret;
            }
        }

        public bool FatRetail { get { return FUSELines[1] == 0x0F0F0F0F0F0F0FF0; } }

        public bool Devkit { get { return FUSELines[1] == 0x0F0F0F0F0F0F0F0F; } }

        public bool Testkit { get { return FUSELines[1] == 0x0F0F0F0F0F0FF00F; } }

        public bool SlimRetail { get { return FUSELines[1] == 0x0F0F0F0F0F0FF0F0; } }

        public bool Unlocked { get { return (FUSELines[0] >> 62) == 0x3; } }

        public bool UsesEeprom { get { return ((FUSELines[0] >> 60) & 0x3) == 0x3; } }

        public bool Secure { get { return ((FUSELines[0] >> 58) & 0x3) == 0x3; } }

        public bool Invalid { get { return ((FUSELines[0] >> 56) & 0x3) == 0x3; } }

        public bool ReservedOk { get { return ((FUSELines[0] & 0xFFFFFFFFFFFFFF) == 0xFFFFFFFFFFFFFF); } }

        public UInt64 EepromKey1 { get { return FUSELines[8]; } }

        public UInt64 EepromKey2 { get { return FUSELines[9]; } }

        public UInt64 EepromHash1 { get { return FUSELines[10]; } }

        public UInt64 EepromHash2 { get { return FUSELines[11]; } }

        private bool CheckKey(int index0, int index1, int index2 = -1, int index3 = -1) {
            if(index0 >= 0 && index1 >= 0 && index2 >= 0 && index3 >= 0) {
                try {
                    CpukeyUtils.VerifyCpuKey(FUSELines[index0] | FUSELines[index1], FUSELines[index2] | FUSELines[index3]);
                    return true;
                }
                catch(CpuKeyException) {}
            }
            else if(index0 >= 0 && index1 >= 0) {
                try {
                    CpukeyUtils.VerifyCpuKey(FUSELines[index0], FUSELines[index1]);
                    return true;
                }
                catch(CpuKeyException) {}
            }
            else
                throw new ArgumentOutOfRangeException();
            return false;
        }
    }
}