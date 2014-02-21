namespace x360Utils.CPUKey {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    internal sealed class FUSE {
        public readonly UInt64[] FUSELines = new UInt64[12];

        public FUSE(string fuseFile) : this(File.ReadAllLines(fuseFile)) { }

        public FUSE(ICollection<string> fuseLines) {
            if(fuseLines.Count < 12)
                throw new ArgumentException("fuseLines must be 12 or more lines!");
            foreach(var fuseLine in fuseLines) {
                if(fuseLine.Length < 10)
                    continue;
                if(!fuseLine.Substring(0, 7).Equals("fuseset", StringComparison.CurrentCultureIgnoreCase))
                    continue;
                int index;
                if(!int.TryParse(fuseLine.Substring(8, 2), out index))
                    throw new ArgumentException("Bad fuseset index!");
                if(!UInt64.TryParse(fuseLine.Substring(11), NumberStyles.HexNumber, null, out FUSELines[index]))
                    throw new ArgumentException("Bad fuseset data!");
            }
        }

        public string CPUKey {
            get {
                var i = 0;
                while(true) {
                    string cpukey;
                    switch(i) {
                        case 0:
                            cpukey = (FUSELines[3] | FUSELines[4]).ToString("X16") + (FUSELines[5] | FUSELines[6]).ToString("X16");
                            break;
                        case 1:
                            cpukey = FUSELines[3].ToString("X16") + FUSELines[5].ToString("X16");
                            break;
                        case 2:
                            cpukey = FUSELines[3].ToString("X16") + FUSELines[6].ToString("X16");
                            break;
                        case 3:
                            cpukey = FUSELines[4].ToString("X16") + FUSELines[5].ToString("X16");
                            break;
                        case 4:
                            cpukey = FUSELines[4].ToString("X16") + FUSELines[6].ToString("X16");
                            break;
                        default:
                            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.NoValidKeyFound);
                    }
                    try {
                        CpukeyUtils.VerifyCpuKey(cpukey);
                        return cpukey;
                    }
                    catch(X360UtilsException ex) {
                        if(ex.ErrorCode != X360UtilsException.X360UtilsErrors.InvalidKeyECD || ex.ErrorCode != X360UtilsException.X360UtilsErrors.InvalidKeyHamming)
                            throw; // Dafuq?
                    }
                    i++;
                }
            }
        }

        public int CBLDV {
            get {
                var tmp = FUSELines[2].ToString("X16");
                return tmp.LastIndexOf("F", StringComparison.Ordinal);
            }
        }

        public int CFLDV {
            get {
                var ret = 0;
                UInt64 l1 = FUSELines[7], l2 = FUSELines[8];
                if(l1 == 0 && l2 == 0)
                    return 0; // LDV == 0! no need to check ;)
                for(var i = 0; i < 16; i++) {
                    if((l1 & 0xF) == 0xF)
                        ret++; // Increment LDV
                    l1 = l1 >> 8; // Move to the next 8 bits
                    if((l2 & 0xF) == 0xF)
                        ret++; // Increment LDV
                    l2 = l2 >> 8; // Move to the next 8 bits
                }
                return ret;
            }
        }

        public bool Retail {
            get { return FUSELines[1] == 0x0F0F0F0F0F0F0FF0; }
        }

        public bool Devkit {
            get { return FUSELines[1] == 0x0F0F0F0F0F0F0F0F; }
        }

        public bool Unlocked {
            get { return (FUSELines[0] >> 62) == 0xC; }
        }

        public bool UsesEeprom {
            get { return ((FUSELines[0] >> 60) & 0xC) == 0xC; }
        }

        public bool Secure {
            get { return ((FUSELines[0] >> 58) & 0xC) == 0xC; }
        }

        public bool Invalid {
            get { return ((FUSELines[0] >> 56) & 0xC) == 0xC; }
        }

        public bool ReservedOk {
            get { return ((FUSELines[0] & 0xFFFFFFFFFFFFFF) == 0xFFFFFFFFFFFFFF); }
        }

        public UInt64 EepromKey1 {
            get { return FUSELines[8]; }
        }

        public UInt64 EepromKey2 {
            get { return FUSELines[9]; }
        }

        public UInt64 EepromHash1 {
            get { return FUSELines[10]; }
        }

        public UInt64 EepromHash2 {
            get { return FUSELines[11]; }
        }
    }
}