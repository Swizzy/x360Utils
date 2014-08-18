namespace x360Utils.NAND {
    using System;
    using System.IO;
    using x360Utils.Common;

    public class Smc {
        public enum JtagPinPresets {
            Normal,
            AudClamp,
            AudClampEject
        }

        public enum JtagPins {
            None = -1,
            ArgonData = 0x83,
            XenonTms = 0xC1,
            Db1F1 = 0xC0,
            AudClamp = 0xCC,
            TrayOpen = 0xCF
        }

        public enum Types {
            Unkown = -1,
            Retail = 0,
            Glitch = 1,
            Jtag = 2,
            Cygnos = 3,
            RJtag = 4,
            RJtagCygnos = 5, // Probably not going to work... but meh...
            TxGlitch = 6,
        }

        public Smc(byte[] data) { Data = data; }

        public Smc(NANDReader reader) {
            reader.Seek(0x78, SeekOrigin.Begin);
            var size = BitOperations.Swap(BitConverter.ToUInt32(reader.ReadBytes(4), 0));
            //reader.Seek(0x7C, SeekOrigin.Begin);
            reader.Seek(BitOperations.Swap(BitConverter.ToUInt32(reader.ReadBytes(4), 0)), SeekOrigin.Begin);
            Data = reader.ReadBytes((int)size);
        }

        public byte[] Data { get; private set; }

        public bool Encrypted {
            get {
                if(Data == null)
                    throw new NullReferenceException("Data can't be null!");
                return !VerifyDecrypted();
            }
        }

        public Version Version {
            get {
                if(Data == null)
                    throw new NullReferenceException("Data can't be null!");
                if(Encrypted)
                    throw new InvalidOperationException("You must decrypt first!");
                return new Version((Data[0x100] & 0xF0) >> 0x4, Data[0x100] & 0x0F, Data[0x101], Data[0x102]);
            }
        }

        public string VersionString {
            get {
                var ver = Version;
                return string.Format("{0}.{1} ({2}.{3:D2})", ver.Major, ver.Minor, ver.Build, ver.Revision);
            }
        }

        public string Motherboard {
            get {
                switch(Version.Major) {
                    case 1:
                        return "Xenon";
                    case 2:
                        return "Zephyr";
                    case 3:
                        return "Falcon/Opus";
                    case 4:
                        return "Jasper";
                    case 5:
                        return "Trinity";
                    case 6:
                        return "Corona";
                    case 7:
                        return "Winchester";
                    default:
                        return string.Format("Unknown revision: {0}", Version.Major);
                }
            }
        }

        public Types Type { get; private set; }

        public JtagPins Tms { get { return GetTms(); } set { SetTms(value); } }

        public JtagPins Tdi { get { return GetTdi(); } set { SetTdi(value); } }

        public JtagPins Tdi0 { get { return GetTdi(0); } set { SetTdi(value, 0); } }

        public JtagPins Tdi1 { get { return GetTdi(1); } set { SetTdi(value, 1); } }

        public JtagPins Tdi2 { get { return GetTdi(2); } set { SetTdi(value, 2); } }

        public JtagPins Tdi3 { get { return GetTdi(3); } set { SetTdi(value, 3); } }

        public bool UnconditionalJtagBoot { get; private set; }

        public bool DmaReadHack { get; private set; }

        public bool GpuJtagHack { get; private set; }

        public bool PciMaskBug { get; private set; }

        public bool PncCharge { get; private set; }

        public bool PncNoCharge { get; private set; }

        public void SetJtagPins(JtagPinPresets preset) {
            if(Data == null)
                throw new NullReferenceException("Data can't be null!");
            if(Encrypted)
                throw new InvalidOperationException("You must decrypt first!");
            if(!CheckIsJtag())
                throw new NotSupportedException("These patches are only available on JTAG type SMC's");
            JtagPins tms, tdi;
            switch(preset) {
                case JtagPinPresets.Normal:
                    tms = Motherboard.Equals("Xenon") ? JtagPins.XenonTms : JtagPins.ArgonData;
                    tdi = JtagPins.Db1F1;
                    break;
                case JtagPinPresets.AudClamp:
                    tms = JtagPins.AudClamp;
                    tdi = JtagPins.Db1F1;
                    break;
                case JtagPinPresets.AudClampEject:
                    tms = JtagPins.AudClamp;
                    tdi = JtagPins.TrayOpen;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("preset");
            }
            try {
                Tms = tms;
            }
            catch {}
            Tdi = tdi;
        }

        private bool VerifyDecrypted() { return BitOperations.DataIsZero(Data, Data.Length - 4, 4); }

        public void Analyze() {
            if(Data == null)
                throw new NullReferenceException("Data can't be null!");
            if(Encrypted) {
                Decrypt();
                if(!VerifyDecrypted())
                    throw new Exception("Decryption failed!");
            }
            Type = ScanForPatchesAndGetSmcType();
        }

        public void DisablePncCharge() {
            if(Data == null)
                throw new NullReferenceException("Data can't be null!");
            if(Encrypted)
                throw new InvalidOperationException("You must decrypt first!");
            if(!CheckIsJtag())
                throw new NotSupportedException("this is only available for JTAG type SMC's");
            var patched = false;
            for(var i = 0; i < Data.Length - 8; i++) {
                if(Data[i] != 0xD0)
                    continue;
                if(Data[i + 1] != 0x00 || Data[i + 2] != 0x02 || Data[i + 5] != 0xD2 || Data[i + 7] != 0x02)
                    continue;
                Main.SendInfo(Main.VerbosityLevels.High, "Disable PNC Charge patch applied @ 0x{0:X}", i);
                Data[i + 6] = 0x04;
                patched = true;
            }
            if(!patched)
                throw new Exception("Not patched");
        }

        public void EnablePncCharge() {
            if(Data == null)
                throw new NullReferenceException("Data can't be null!");
            if(Encrypted)
                throw new InvalidOperationException("You must decrypt first!");
            if(!CheckIsJtag())
                throw new NotSupportedException("this is only available for JTAG type SMC's");
            throw new NotImplementedException();
        }

        public void UnconditionalBoot() {
            if(Data == null)
                throw new NullReferenceException("Data can't be null!");
            if(Encrypted)
                throw new InvalidOperationException("You must decrypt first!");
            if(!CheckIsJtag())
                throw new NotSupportedException("this is only available for JTAG type SMC's");
            var patched = false;
            for(var i = 0; i < Data.Length - 7; i++) {
                if(Data[i] != 0xC0)
                    continue;
                if(Data[i + 1] != 0x07 || Data[i + 2] != 0x78 || Data[i + 4] != 0xE6 || Data[i + 5] != 0xB4 || Data[i + 6] != 0x00)
                    continue;
                Main.SendInfo(Main.VerbosityLevels.High, "Unconditional boot patch applied @ 0x{0:X}", i);
                Data[i + 2] = 0x00;
                Data[i + 3] = 0xE5;
                Data[i + 4] = 0x3D;
                Data[i + 6] = 0x82;
                patched = true;
            }
            if(!patched)
                throw new Exception("Not patched");
        }

        public void FixPciMaskBug() {
            if(Encrypted)
                throw new InvalidOperationException("You must decrypt first!");
            if(!CheckIsJtag())
                throw new NotSupportedException("this is only available for JTAG type SMC's");
            for(var i = 0; i < Data.Length - 8; i++) {
                if(Data[i] != 0x24)
                    continue;
                if(Data[i + 1] != 0x07 || Data[i + 2] != 0xD0 || Data[i + 3] != 0xE0 || Data[i + 4] != 0xF8)
                    continue;
                Main.SendInfo(Main.VerbosityLevels.High, "Found and fixed PCI Mask Bug @ 0x{0:X}", i);
                Data[i + 2] = 0xF8;
                Data[i + 3] = 0xD0;
                Data[i + 4] = 0xE0;
            }
        }

        public void Decrypt() {
            if(!Encrypted)
                return; // No need to run this shit again now is there?
            byte[] key = {
                             0x42, 0x75, 0x4E, 0x79
                         };
            for(var i = 0; i < Data.Length; i++) {
                var num1 = Data[i];
                var num2 = num1 * 0xFB;
                Data[i] = Convert.ToByte(num1 ^ (key[i & 3] & 0xFF));
                key[(i + 1) & 3] += (byte)num2;
                key[(i + 2) & 3] += Convert.ToByte(num2 >> 8);
            }
        }

        public void Encrypt() {
            if(Encrypted)
                return; // No need to run this shit again now is there?
            byte[] key = {
                             0x42, 0x75, 0x4E, 0x79
                         };
            for(var i = 0; i < Data.Length; i++) {
                var num2 = Data[i] ^ (key[i & 3] & 0xff);
                var num3 = num2 * 0xFB;
                Data[i] = Convert.ToByte(num2);
                key[(i + 1) & 3] = (byte)(key[(i + 1) & 3] + (byte)num3);
                key[(i + 2) & 3] = (byte)(key[(i + 2) & 3] + Convert.ToByte(num3 >> 8));
            }
        }

        public bool CheckIsJtag() {
            switch(Type) {
                case Types.Unkown:
                case Types.Retail:
                case Types.Glitch:
                case Types.TxGlitch:
                    return false;
                case Types.Jtag:
                case Types.Cygnos:
                case Types.RJtag:
                case Types.RJtagCygnos:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public bool CheckIsGlitchPatched() {
            switch(Type) {
                case Types.Unkown:
                case Types.Retail:
                case Types.Jtag:
                case Types.Cygnos:
                    return false;
                case Types.Glitch:
                case Types.TxGlitch:
                case Types.RJtag:
                case Types.RJtagCygnos:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void GlitchPatch() {
            var patched = false;
            for(var i = 0; i < Data.Length - 8; i++) {
                if(Data[i] != 0x05 || Data[i + 2] != 0xE5 || Data[i + 4] != 0xB4 || Data[i + 5] != 0x05)
                    continue;
                Data[i] = 0x00;
                Data[i + 1] = 0x00;
                patched = true;
                Main.SendInfo(Main.VerbosityLevels.High, "SMC Glitch Patched @ offset: 0x{0:X}", i);
            }
            if(!patched)
                throw new Exception("Glitch patching failed!");
        }

        #region Private Methods

        private Types ScanForPatchesAndGetSmcType() {
            var ret = Types.Unkown;
            var glitchPatched = false;
            var retail = false;
            for(var i = 0; i < Data.Length - 10; i++) {
                switch(Data[i]) {
                    case 0x05:
                        /* Retail (No Glitch) */
                        if(Data[i + 2] == 0xE5 && Data[i + 4] == 0xB4 && Data[i + 5] == 0x05) {
                            retail = true;
                            glitchPatched = false; // Not properly glitch patched...
                            Main.SendInfo(Main.VerbosityLevels.High, "Found Retail (No Glitch) bytes @ 0x{0:X}", i);
                        }
                        break;
                    case 0x00:
                        /* Glitch */
                        if(Data[i + 1] == 0x00 && Data[i + 2] == 0xE5 && Data[i + 4] == 0xB4 && Data[i + 5] == 0x05) {
                            glitchPatched = true;
                            Main.SendInfo(Main.VerbosityLevels.High, "Found Glitch bytes @ 0x{0:X}", i);
                        }
                        break;
                    case 0x78:
                        /* Cygnos */
                        if(Data[i + 1] == 0xBA && Data[i + 2] == 0xB6) {
                            ret = Types.Cygnos;
                            Main.SendInfo(Main.VerbosityLevels.High, "Found Cygnos bytes @ 0x{0:X}", i);
                        }
                        break;
                    case 0xD0:
                        /* JTAG */
                        if(Data[i + 1] == 0x00 && Data[i + 2] == 0x00 && Data[i + 3] == 0x1B) {
                            Main.SendInfo(Main.VerbosityLevels.High, "Found JTAG bytes @ 0x{0:X}", i);
                            ret = Types.Jtag;
                        }
                        /* GPU JTAG Hack */
                        if(Data[i + 1] == 0xE0 && Data[i + 2] == 0xD8 && Data[i + 3] == 0xFC && Data[i + 4] == 0x78 && Data[i + 6] == 0x76) {
                            Main.SendInfo(Main.VerbosityLevels.High, "Found GPU JTAG Hack @ 0x{0:X}", i);
                            GpuJtagHack = true;
                        }
                        /* PNC No Charge */
                        if(Data[i + 1] == 0x00 && Data[i + 2] == 0x02 && Data[i + 5] == 0xD2 && Data[i + 6] == 0x04 && Data[i + 7] == 0x02) {
                            Main.SendInfo(Main.VerbosityLevels.High, "Found PNC No Charge patch @ 0x{0:X}", i);
                            PncNoCharge = true;
                        }
                        break;
                    case 0x53:
                        /* TxGlitch */
                        if(Data[i + 1] == 0x8F && Data[i + 2] == 0xFD && Data[i + 3] == 0x22) {
                            Main.SendInfo(Main.VerbosityLevels.High, "Found TX Glitch bytes @ 0x{0:X}", i);
                            ret = Types.TxGlitch;
                        }
                        break;
                    case 0xC0:
                        /* Unconditional Boot (JTAG) */
                        if(Data[i + 1] == 0x07 && Data[i + 2] == 0x00 && Data[i + 3] == 0xE5 && Data[i + 4] == 0x3D && Data[i + 5] == 0xB4 && Data[i + 6] == 0x82) {
                            UnconditionalJtagBoot = true;
                            Main.SendInfo(Main.VerbosityLevels.High, "Unconditional boot patch found @ 0x{0:X}", i);
                        }
                        break;
                    case 0xB4:
                        /* DMA Read Hack (JTAG) */
                        if(Data[i + 1] == 0x04 && Data[i + 2] == 0x03 && Data[i + 3] == 0x02 && Data[i + 6] == 0x02) {
                            Main.SendInfo(Main.VerbosityLevels.High, "Found DMA Read Hack @ 0x{0:X}", i);
                            DmaReadHack = true;
                        }
                        break;
                    case 0x24:
                        /* PCI Mask Bug */
                        if(Data[i + 1] == 0x07 && Data[i + 2] == 0xD0 && Data[i + 3] == 0xE0 && Data[i + 4] == 0xF8) {
                            Main.SendInfo(Main.VerbosityLevels.High, "Found PCI Mask Bug @ 0x{0:X}", i);
                            PciMaskBug = true;
                        }
                        break;
                    case 0x20:
                        /* PNC Charge*/
                        if(Data[i + 1] == 0x15 && Data[i + 2] == 0x0B && Data[i + 3] == 0xC0 && Data[i + 4] == 0x00 && Data[i + 5] == 0x78 && Data[i + 7] == 0x76 && Data[i + 8] == 0x00) {
                            Main.SendInfo(Main.VerbosityLevels.High, "Found PNC Charge patch @ 0x{0:X}", i);
                            PncCharge = true;
                        }
                        break;
                    case 0xD2:
                        /* TMS */
                        if(Data[i + 1] == Data[i + 9] && Data[i + 2] == 0x74 && Data[i + 4] == 0xD5 && Data[i + 5] == 0xE0 && Data[i + 6] == 0xFD && Data[i + 7] == 0x22 && Data[i + 8] == 0xC2) {
                            Main.SendInfo(Main.VerbosityLevels.High, "TMS Found @ offset: 0x{0:X} TMS Value: {1} (0x{2:X2})\n", i, (JtagPins)Data[i + 1], Data[i + 1]);
                            Tms = (JtagPins)Data[i + 1];
                            
                        }
                        break;
                }
            }
            if(!glitchPatched || retail)
                return ret == Types.Unkown && retail ? Types.Retail : ret;
            switch(ret) {
                case Types.Jtag:
                    Main.SendInfo(Main.VerbosityLevels.Medium, "This SMC has both Glitch and JTAG patches");
                    return Types.RJtag;
                case Types.Cygnos:
                    Main.SendInfo(Main.VerbosityLevels.Medium, "This SMC has both Glitch and Cygnos patches");
                    return Types.RJtagCygnos;
                case Types.TxGlitch:
                    return Types.TxGlitch;
                default:
                    return Types.Glitch;
            }
        }

        private void SetTms(JtagPins tms) {
            if(Encrypted)
                throw new InvalidOperationException("You must decrypt first!");
            if(!CheckIsJtag())
                throw new NotSupportedException("TMS is only available on JTAG type SMC's");
            var patched = false;
            for(var i = 0; i < Data.Length; i++) {
                if(Data[i] != 0xD2 || Data[i + 1] != Data[i + 9] || Data[i + 2] != 0x74 || Data[i + 4] != 0xD5 || Data[i + 5] != 0xE0 || Data[i + 6] != 0xFD || Data[i + 7] != 0x22 ||
                   Data[i + 8] != 0xC2)
                    continue;
                Data[i + 1] = (byte)tms;
                Data[i + 9] = (byte)tms;
                patched = true;
                Main.SendInfo(Main.VerbosityLevels.High, "TMS Patched @ offset: 0x{0:X} TMS Value: {1} (0x{2:X2})\n", i, tms, tms);
            }
            if(!patched)
                throw new Exception("Unable to find TMS in the SMC code!");
        }

        private void SetTdi(JtagPins tdi, int num = -1) {
            if(Encrypted)
                throw new InvalidOperationException("You must decrypt first!");
            if(!CheckIsJtag())
                throw new NotSupportedException("TDI is only available on JTAG type SMC's");
            var patched = false;
            switch(num) {
                case -1:
                    Main.SendInfo(Main.VerbosityLevels.Low, "Patching TDI in all 4 places...");
                    for(var i = 0; i <= 3; i++) {
                        try {
                            SetTdi(tdi, i);
                        }
                        catch {}
                    }
                    break;
                case 0:
                    for(var i = 0; i < Data.Length; i++) {
                        if(Data[i] != 0x92 || Data[i + 4] != 0xDF || Data[i + 5] != 0xF8 || Data[i + 6] != 0x22)
                            continue;
                        Data[i + 1] = (byte)tdi;
                        patched = true;
                        Main.SendInfo(Main.VerbosityLevels.High, "TDI{1} Patched @ offset: 0x{0:X} TDI Value: {2} (0x{3:X2})", i, num, tdi, tdi);
                    }
                    break;
                case 1:
                    for(var i = 0; i < Data.Length; i++) {
                        if(Data[i] != 0xC2 || Data[i + 2] != 0x74 || Data[i + 3] != 0x02 || Data[i + 8] != 0x74 || Data[i + 8] != 0x02)
                            continue;
                        Data[i + 1] = (byte)tdi;
                        patched = true;
                        Main.SendInfo(Main.VerbosityLevels.High, "TDI{1} Patched @ offset: 0x{0:X} TDI Value: {2} (0x{3:X2})", i, num, tdi, tdi);
                    }
                    break;
                case 2:
                    for(var i = 0; i < Data.Length; i++) {
                        if(Data[i] != 0x7F || Data[i + 1] != 0x01 || Data[i + 4] != 0xC2 || Data[i + 6] != 0x74 || Data[i + 7] != 0x01)
                            continue;
                        Data[i + 1] = (byte)tdi;
                        patched = true;
                        Main.SendInfo(Main.VerbosityLevels.High, "TDI{1} Patched @ offset: 0x{0:X} TDI Value: {2} (0x{3:X2})\n", i, num, tdi, tdi);
                    }
                    break;
                case 3:
                    for(var i = 0; i < Data.Length; i++) {
                        if(Data[i] != 0x76 || Data[i + 2] != 0x78 || Data[i + 4] != 0x76 || Data[i + 6] != 0xD2)
                            continue;
                        Data[i + 1] = (byte)tdi;
                        patched = true;
                        Main.SendInfo(Main.VerbosityLevels.High, "TDI{1} Patched @ offset: 0x{0:X} TDI Value: {2} (0x{3:X2})", i, num, tdi, tdi);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("num", "num must be between -1 and 3! (-1 = auto patch all with the same TDI data)");
            }
            if(!patched)
                throw new Exception("Unable to find TMS in the SMC code!");
        }

        private JtagPins GetTdi(int num = -1) {
            if(Encrypted)
                throw new InvalidOperationException("You must decrypt first!");
            if(!CheckIsJtag())
                throw new NotSupportedException("TDI is only available on JTAG type SMC's");
            switch(num) {
                case -1:
                    var ret = JtagPins.None;
                    for(var i = 0; i <= 3; i++) {
                        try {
                            switch(ret) {
                                case JtagPins.None:
                                    ret = GetTdi(i);
                                    break;
                                default:
                                    if(GetTdi(i) != ret)
                                        throw new InvalidOperationException("TDI0 isn't the same as one of the others...");
                                    break;
                            }
                        }
                        catch(Exception ex) {
                            if(ex is InvalidOperationException)
                                throw;
                        }
                    }
                    return ret;
                case 0:
                    for(var i = 0; i < Data.Length; i++) {
                        if(Data[i] != 0x92 || Data[i + 4] != 0xDF || Data[i + 5] != 0xF8 || Data[i + 6] != 0x22)
                            continue;

                        Main.SendInfo(Main.VerbosityLevels.High, "TDI0 Found @ offset: 0x{0:X} TDI Value: {1} (0x{2:X2})", i, (JtagPins)Data[i + 1], Data[i + 1]);
                        return (JtagPins)Data[i + 1];
                    }
                    break;
                case 1:
                    for(var i = 0; i < Data.Length; i++) {
                        if(Data[i] != 0xC2 || Data[i + 2] != 0x74 || Data[i + 3] != 0x02 || Data[i + 8] != 0x74 || Data[i + 8] != 0x02)
                            continue;

                        Main.SendInfo(Main.VerbosityLevels.High, "TDI1 Found @ offset: 0x{0:X} TDI Value: {1} (0x{2:X2})", i, (JtagPins)Data[i + 1], Data[i + 1]);
                        return (JtagPins)Data[i + 1];
                    }
                    break;
                case 2:
                    for(var i = 0; i < Data.Length; i++) {
                        if(Data[i] != 0x7F || Data[i + 1] != 0x01 || Data[i + 4] != 0xC2 || Data[i + 6] != 0x74 || Data[i + 7] != 0x01)
                            continue;

                        Main.SendInfo(Main.VerbosityLevels.High, "TDI2 Found @ offset: 0x{0:X} TDI Value: {1} (0x{2:X2})", i, (JtagPins)Data[i + 5], Data[i + 5]);
                        return (JtagPins)Data[i + 5];
                    }
                    break;
                case 3:
                    for(var i = 0; i < Data.Length; i++) {
                        if(Data[i] != 0x76 || Data[i + 2] != 0x78 || Data[i + 4] != 0x76 || Data[i + 6] != 0xD2)
                            continue;

                        Main.SendInfo(Main.VerbosityLevels.High, "TDI3 Found @ offset: 0x{0:X} TDI Value: {1} (0x{2:X2})", i, (JtagPins)Data[i + 7], Data[i + 7]);
                        return (JtagPins)Data[i + 7];
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("num", "should be between -1, 0, 1, 2 or 3 (-1 is to get \"one for all\"");
            }
            throw new Exception(string.Format("TDI{0} was not found in this SMC code!", num));
        }

        #endregion
    }
}