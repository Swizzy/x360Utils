#region

using System;

#endregion

namespace x360Utils.NAND {
    public class SMC {
        #region SMCTypes enum

        public enum SMCTypes {
            Unkown = -1,
            Retail = 0,
            Glitch = 1,
            Jtag = 2,
            Cygnos = 3,
            RJtag = 4,
            RJtagCygnos = 5, // Probably not going to work... but meh...
        }

        #endregion

        #region TMSTDIValues enum

        public enum TMSTDIValues : byte {
            ArgonData = 0x83,
            DB1F1 = 0xC0,
            AudClamp = 0xCC,
            TrayOpen = 0xCF
        }

        #endregion

        private static void DecryptCheck(ref byte[] data) {
            if (!Cryptography.VerifySMCDecrypted(ref data))
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotDecrypted);
        }

        public string GetVersion(ref byte[] smcdata) {
            DecryptCheck(ref smcdata);
            return string.Format("{0}.{1} ({2}.{3:D2})", (smcdata[0x100] & 0xF0) >> 0x4, smcdata[0x100] & 0x0F,
                                 smcdata[0x101], smcdata[0x102]);
        }

        public SMCTypes GetType(ref byte[] smcdata) {
            var ret = SMCTypes.Unkown;
            var glitchPatched = false;
            var retail = false;
            for (var i = 0; i < smcdata.Length - 6; i++) {
                switch (smcdata[i]) {
                    case 0x05:
                        if ((smcdata[i + 2] == 0xE5) && (smcdata[i + 4] == 0xB4) && (smcdata[i + 5] == 0x05)) {
                            retail = true;
                            glitchPatched = false; // Not properly glitch patched...
                            Debug.SendDebug("Found Retail bytes @ 0x{0:X}\n", i);
                        }
                        break;
                    case 0x00:
                        /* Glitch */
                        if ((smcdata[i + 1] == 0x00) && (smcdata[i + 2] == 0xE5) && (smcdata[i + 4] == 0xB4) &&
                            (smcdata[i + 5] == 0x05)) {
                            glitchPatched = true;
                            Debug.SendDebug("Found Glitch bytes @ 0x{0:X}\n", i);
                        }
                        break;
                    case 0x78:
                        /* Cygnos */
                        if ((smcdata[i + 1] == 0xBA) && (smcdata[i + 2] == 0xB6)) {
                            ret = SMCTypes.Cygnos;
                            Debug.SendDebug("Found Cygnos bytes @ 0x{0:X}\n", i);
                        }
                        break;
                    case 0xD0:
                        /* JTAG */
                        if ((smcdata[i + 1] == 0x00) && (smcdata[i + 2] == 0x00) && (smcdata[i + 3] == 0x1B)) {
                            Debug.SendDebug("Found JTAG bytes @ 0x{0:X}\n", i);
                            ret = SMCTypes.Jtag;
                        }
                        break;
                }
            }
            if (glitchPatched && !retail) {
                switch (ret) {
                    case SMCTypes.Jtag:
                        Debug.SendDebug("Image has both Glitch and JTAG patches\n");
                        return SMCTypes.RJtag;
                    case SMCTypes.Cygnos:
                        Debug.SendDebug("Image has both Glitch and Cygnos patches\n");
                        return SMCTypes.RJtagCygnos;
                    default:
                        return SMCTypes.Glitch;
                }
            }
            return ret == SMCTypes.Unkown && retail ? SMCTypes.Retail : ret;
        }

        public bool GlitchPatch(ref byte[] smcdata) {
            DecryptCheck(ref smcdata);
            var patched = false;
            for (var i = 0; i < smcdata.Length - 8; i++) {
                if (smcdata[i] != 0x05 || smcdata[i + 2] != 0xE5 || smcdata[i + 4] != 0xB4 || smcdata[i + 5] != 0x05)
                    continue;
                smcdata[i] = 0x00;
                smcdata[i + 1] = 0x00;
                patched = true;
                Debug.SendDebug("SMC Patched @ offset: 0x{0:X}\n", i);
            }
            return patched;
        }

        public bool CheckGlitchPatch(ref byte[] smcdata) {
            DecryptCheck(ref smcdata);
            var patched = false;
            var found = false;
            for (var i = 0; i < smcdata.Length - 8; i++) {
                if (smcdata[i + 2] != 0xE5 || smcdata[i + 4] != 0xB4 || smcdata[i + 5] != 0x05)
                    continue;
                if (smcdata[i] == 0x00 && smcdata[i + 1] == 0x00) {
                    Debug.SendDebug("Glitch patch found @ offset: 0x{0:X}\n", i);
                    patched = true;
                }
                found = true;
            }
            if (!found)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound);
            return patched;
        }

        public byte GetTMS(ref byte[] smcdata) {
            DecryptCheck(ref smcdata);
            for (var i = 0; i < smcdata.Length; i++) {
                if (smcdata[i] != 0xD2)
                    continue;
                if (smcdata[i + 1] != smcdata[i + 9] || smcdata[i + 2] != 0x74 || smcdata[i + 4] != 0xD5 || smcdata[i + 5] != 0xE0 || smcdata[i + 6] != 0xFD || smcdata[i + 7] != 0x22 || smcdata[i + 8] != 0xC2)
                    continue;
                Debug.SendDebug("TMS Found @ offset: 0x{0:X} TMS Value: 0x{1} ({2:X2})\n", i, (TMSTDIValues) smcdata[i + 1], smcdata[i + 1]);
                return smcdata[i + 1];
            }
            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound);
        }

        public byte GetTDI(ref byte[] smcdata, int num) {
            DecryptCheck(ref smcdata);
            switch (num) {
                case 0:
                    for (var i = 0; i < smcdata.Length; i++) {
                        if (smcdata[i] != 0x92 || smcdata[i + 4] != 0xDF || smcdata[i + 5] != 0xF8 ||
                            smcdata[i + 6] != 0x22)
                            continue;
                        Debug.SendDebug("TDI0 Found @ offset: 0x{0:X} TDI Value: 0x{1} ({2:X2})\n", i,
                                        (TMSTDIValues) smcdata[i + 1], smcdata[i + 1]);
                        return smcdata[i + 1];
                    }
                    break;
                case 1:
                    for (var i = 0; i < smcdata.Length; i++) {
                        if (smcdata[i] != 0xC2 || smcdata[i + 2] != 0x74 || smcdata[i + 3] != 0x02 ||
                            smcdata[i + 8] != 0x74 || smcdata[i + 8] != 0x02)
                            continue;
                        Debug.SendDebug("TDI1 Found @ offset: 0x{0:X} TDI Value: 0x{1} ({2:X2})\n", i,
                                        (TMSTDIValues) smcdata[i + 1], smcdata[i + 1]);
                        return smcdata[i + 1];
                    }
                    break;
                case 2:
                    for (var i = 0; i < smcdata.Length; i++) {
                        if (smcdata[i] != 0x7F || smcdata[i + 1] != 0x01 || smcdata[i + 4] != 0xC2 ||
                            smcdata[i + 6] != 0x74 || smcdata[i + 7] != 0x01)
                            continue;
                        Debug.SendDebug("TDI2 Found @ offset: 0x{0:X} TDI Value: 0x{1} ({2:X2})\n", i,
                                        (TMSTDIValues) smcdata[i + 5], smcdata[i + 5]);
                        return smcdata[i + 5];
                    }
                    break;
                case 3:
                    for (var i = 0; i < smcdata.Length; i++) {
                        if (smcdata[i] != 0x76 || smcdata[i + 2] != 0x78 || smcdata[i + 4] != 0x76 ||
                            smcdata[i + 6] != 0xD2)
                            continue;
                        Debug.SendDebug("TDI3 Found @ offset: 0x{0:X} TDI Value: 0x{1} ({2:X2})\n", i,
                                        (TMSTDIValues) smcdata[i + 7], smcdata[i + 7]);
                        return smcdata[i + 7];
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("num", "num must be between 0 and 3!");
            }
            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound);
        }

        public bool SetTMS(ref byte[] smcdata, byte tms) {
            DecryptCheck(ref smcdata);
            var patched = false;
            for (var i = 0; i < smcdata.Length; i++) {
                if (smcdata[i] != 0xD2 || smcdata[i + 1] != smcdata[i + 9] || smcdata[i + 2] != 0x74 ||
                    smcdata[i + 4] != 0xD5 || smcdata[i + 5] != 0xE0 || smcdata[i + 6] != 0xFD || smcdata[i + 7] != 0x22 ||
                    smcdata[i + 8] != 0xC2)
                    continue;
                smcdata[i + 1] = tms;
                smcdata[i + 9] = tms;
                patched = true;
                Debug.SendDebug("TMS Patched @ offset: 0x{0:X} TMS Value: 0x{1} ({2:X2})\n", i, (TMSTDIValues) tms, tms);
            }
            return patched;
        }

        public bool SetTDI(ref byte[] smcdata, byte tdi, int num = -1) {
            DecryptCheck(ref smcdata);
            var patched = false;
            switch (num) {
                case -1:
                    Debug.SendDebug("Patching TDI in all 4 places...\n");
                    var ret = false;
                    for(var i = 0; i < 4; i++)
                        if(SetTDI(ref smcdata, tdi, i))
                            ret = true;
                    return ret;
                case 0:
                    for (var i = 0; i < smcdata.Length; i++) {
                        if (smcdata[i] != 0x92 || smcdata[i + 4] != 0xDF || smcdata[i + 5] != 0xF8 ||
                            smcdata[i + 6] != 0x22)
                            continue;
                        smcdata[i + 1] = tdi;
                        patched = true;
                        Debug.SendDebug("TDI{1} Patched @ offset: 0x{0:X} TDI Value: 0x{2} ({3:X2})\n", i, num,
                                        (TMSTDIValues) tdi, tdi);
                    }
                    break;
                case 1:
                    for (var i = 0; i < smcdata.Length; i++) {
                        if (smcdata[i] != 0xC2 || smcdata[i + 2] != 0x74 || smcdata[i + 3] != 0x02 ||
                            smcdata[i + 8] != 0x74 || smcdata[i + 8] != 0x02)
                            continue;
                        smcdata[i + 1] = tdi;
                        patched = true;
                        Debug.SendDebug("TDI{1} Patched @ offset: 0x{0:X} TDI Value: 0x{2} ({3:X2})\n", i, num,
                                        (TMSTDIValues) tdi, tdi);
                    }
                    break;
                case 2:
                    for (var i = 0; i < smcdata.Length; i++) {
                        if (smcdata[i] != 0x7F || smcdata[i + 1] != 0x01 || smcdata[i + 4] != 0xC2 ||
                            smcdata[i + 6] != 0x74 || smcdata[i + 7] != 0x01)
                            continue;
                        smcdata[i + 1] = tdi;
                        patched = true;
                        Debug.SendDebug("TDI{1} Patched @ offset: 0x{0:X} TDI Value: 0x{2} ({3:X2})\n", i, num,
                                        (TMSTDIValues) tdi, tdi);
                    }
                    break;
                case 3:
                    for (var i = 0; i < smcdata.Length; i++) {
                        if (smcdata[i] != 0x76 || smcdata[i + 2] != 0x78 || smcdata[i + 4] != 0x76 ||
                            smcdata[i + 6] != 0xD2)
                            continue;
                        smcdata[i + 1] = tdi;
                        patched = true;
                        Debug.SendDebug("TDI{1} Patched @ offset: 0x{0:X} TDI Value: 0x{2} ({3:X2})\n", i, num,
                                        (TMSTDIValues) tdi, tdi);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("num",
                                                          "num must be between -1 and 3! (-1 = auto patch all with the same TDI data)");
            }
            return patched;
        }
    }
}