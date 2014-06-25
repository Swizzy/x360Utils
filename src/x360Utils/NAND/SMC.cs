namespace x360Utils.NAND {
    using System;

    public sealed class SMC {
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

        public enum TMSTDIValues: byte {
            None = 0x00,
            ArgonData = 0x83,
            DB1F1 = 0xC0,
            AudClamp = 0xCC,
            TrayOpen = 0xCF
        }

        #endregion

        public JTAGSMCPatches JTAGPatches = new JTAGSMCPatches();

        private static void DecryptCheck(ref byte[] data) {
            if(!Cryptography.VerifySmcDecrypted(ref data))
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotDecrypted);
        }

        public string GetVersion(ref byte[] smcdata) {
            DecryptCheck(ref smcdata);
            return string.Format("{0}.{1} ({2}.{3:D2})", (smcdata[0x100] & 0xF0) >> 0x4, smcdata[0x100] & 0x0F, smcdata[0x101], smcdata[0x102]);
        }

        public string GetMotherBoardFromVersion(ref byte[] smcdata) {
            DecryptCheck(ref smcdata);
            switch((smcdata[0x100] & 0xF0) >> 0x4) {
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
                    //case 7:
                    //    return "Winchester";
                default:
                    throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound, "The version doesn't match any known motherboard version... :(");
            }
        }

        public SMCTypes GetType(ref byte[] smcdata) {
            var ret = SMCTypes.Unkown;
            var glitchPatched = false;
            var retail = false;
            for(var i = 0; i < smcdata.Length - 6; i++) {
                switch(smcdata[i]) {
                    case 0x05:
                        if((smcdata[i + 2] == 0xE5) && (smcdata[i + 4] == 0xB4) && (smcdata[i + 5] == 0x05)) {
                            retail = true;
                            glitchPatched = false; // Not properly glitch patched...
                            if(Main.VerifyVerbosityLevel(1))
                                Main.SendInfo("Found Retail bytes @ 0x{0:X}\n", i);
                        }
                        break;
                    case 0x00:
                        /* Glitch */
                        if((smcdata[i + 1] == 0x00) && (smcdata[i + 2] == 0xE5) && (smcdata[i + 4] == 0xB4) && (smcdata[i + 5] == 0x05)) {
                            glitchPatched = true;
                            if(Main.VerifyVerbosityLevel(1))
                                Main.SendInfo("Found Glitch bytes @ 0x{0:X}\n", i);
                        }
                        break;
                    case 0x78:
                        /* Cygnos */
                        if((smcdata[i + 1] == 0xBA) && (smcdata[i + 2] == 0xB6)) {
                            ret = SMCTypes.Cygnos;
                            if(Main.VerifyVerbosityLevel(1))
                                Main.SendInfo("Found Cygnos bytes @ 0x{0:X}\n", i);
                        }
                        break;
                    case 0xD0:
                        /* JTAG */
                        if((smcdata[i + 1] == 0x00) && (smcdata[i + 2] == 0x00) && (smcdata[i + 3] == 0x1B)) {
                            if(Main.VerifyVerbosityLevel(1))
                                Main.SendInfo("Found JTAG bytes @ 0x{0:X}\n", i);
                            ret = SMCTypes.Jtag;
                        }
                        break;
                }
            }
            if(glitchPatched && !retail) {
                switch(ret) {
                    case SMCTypes.Jtag:
                        if(Main.VerifyVerbosityLevel(1))
                            Main.SendInfo("Image has both Glitch and JTAG patches\n");
                        return SMCTypes.RJtag;
                    case SMCTypes.Cygnos:
                        if(Main.VerifyVerbosityLevel(1))
                            Main.SendInfo("Image has both Glitch and Cygnos patches\n");
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
            for(var i = 0; i < smcdata.Length - 8; i++) {
                if(smcdata[i] != 0x05 || smcdata[i + 2] != 0xE5 || smcdata[i + 4] != 0xB4 || smcdata[i + 5] != 0x05)
                    continue;
                smcdata[i] = 0x00;
                smcdata[i + 1] = 0x00;
                patched = true;
                if(Main.VerifyVerbosityLevel(1))
                    Main.SendInfo("SMC Patched @ offset: 0x{0:X}\n", i);
            }
            return patched;
        }

        public bool CheckGlitchPatch(ref byte[] smcdata) {
            DecryptCheck(ref smcdata);
            var patched = false;
            var found = false;
            for(var i = 0; i < smcdata.Length - 8; i++) {
                if(smcdata[i + 2] != 0xE5 || smcdata[i + 4] != 0xB4 || smcdata[i + 5] != 0x05)
                    continue;
                if(smcdata[i] == 0x00 && smcdata[i + 1] == 0x00) {
                    if(Main.VerifyVerbosityLevel(1))
                        Main.SendInfo("Glitch patch found @ offset: 0x{0:X}\n", i);
                    patched = true;
                }
                found = true;
            }
            if(!found)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound);
            return patched;
        }

        #region Nested type: JTAGSMCPatches

        public sealed class JTAGSMCPatches {
            public static void AnalyseSMC(ref byte[] smcdata, bool verbose = false) {
                Main.SendInfo("\r\nDMA Read Hack: {0}", FindDMAReadHack(ref smcdata) ? "Yes" : "No");
                Main.SendInfo("\r\nGPU JTAG Hack: {0}", FindGPUJtagHack(ref smcdata) ? "Yes" : "No");
                Main.SendInfo("\r\nPCI Mask Bug: {0}", FindAndFixPCIMaskBug(ref smcdata) ? "Yes (Fixed if data is saved)" : "No");
                Main.SendInfo("\r\nPlay 'n' Charge Patch: {0}", FindPNCCharge(ref smcdata) ? "Yes" : "No");
                Main.SendInfo("\r\nPlay 'n' Charge when off Disabled: {0}", FindPNCNoCharge(ref smcdata) ? "Yes" : "No");
                Main.SendInfo("\r\nUnconditional Boot Patch: {0}", FindUnconditionalBoot(ref smcdata) ? "Yes" : "No");
                TMSTDIValues tms = TMSTDIValues.None, tdi = TMSTDIValues.None;
                try {
                    tms = (TMSTDIValues)GetTMS(ref smcdata);
                    if(verbose)
                        Main.SendInfo("\r\nTMS Patch: {0}", tms);
                }
                catch(X360UtilsException ex) {
                    if(ex.ErrorCode == X360UtilsException.X360UtilsErrors.DataNotFound && verbose)
                        Main.SendInfo("\r\nTMS Patch: Not Found!");
                    else if(verbose)
                        throw;
                }
                for(var i = 0; i < 4; i++) {
                    try {
                        if(tdi == TMSTDIValues.None) {
                            tdi = (TMSTDIValues)GetTDI(ref smcdata, i);
                            if(verbose)
                                Main.SendInfo("\r\nTDI{1} Patch: {0}", tdi, i);
                        }
                        else if(verbose)
                            Main.SendInfo("\r\nTDI{1} Patch: {0}", (TMSTDIValues)GetTDI(ref smcdata, i), i);
                    }
                    catch(X360UtilsException ex) {
                        if(ex.ErrorCode == X360UtilsException.X360UtilsErrors.DataNotFound && verbose)
                            Main.SendInfo("\r\nTDI{0} Patch: Not Found!", i);
                        else if(verbose)
                            throw;
                    }
                }
                Debug.SendDebug("TMS: {0} TDI: {1}", tms, tdi);
                switch(tms) {
                    case TMSTDIValues.None:
                        if(tdi == TMSTDIValues.DB1F1)
                            Main.SendInfo("\r\nTMS & TDI Matches Xenon \"Normal\" Patchset");
                        else {
                            Main.SendInfo("\r\nUnknown TMS & TDI Patchset!");
                            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.UnkownPatchset);
                        }
                        break;
                    case TMSTDIValues.ArgonData:
                        if(tdi == TMSTDIValues.DB1F1)
                            Main.SendInfo("\r\n TMS & TDI Matches Zephyr, Falcon & Jasper \"Normal\" Patchset");
                        else {
                            Main.SendInfo("\r\nUnknown TMS & TDI Patchset!");
                            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.UnkownPatchset);
                        }
                        break;
                    case TMSTDIValues.DB1F1:
                        Main.SendInfo("\r\nUnknown TMS & TDI Patchset!");
                        throw new X360UtilsException(X360UtilsException.X360UtilsErrors.UnkownPatchset);
                    case TMSTDIValues.AudClamp:
                        switch(tdi) {
                            case TMSTDIValues.DB1F1:
                                Main.SendInfo("\r\n TMS & TDI Matches Zephyr, Falcon & Jasper \"Aud_Clamp\" Patchset");
                                break;
                            case TMSTDIValues.TrayOpen:
                                Main.SendInfo("\r\n TMS & TDI Matches Zephyr, Falcon & Jasper \"Aud_Clamp + Eject\" Patchset");
                                break;
                            default:
                                Main.SendInfo("\r\nUnknown TMS & TDI Patchset!");
                                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.UnkownPatchset);
                        }
                        break;
                    case TMSTDIValues.TrayOpen:
                        Main.SendInfo("\r\nUnknown TMS & TDI Patchset!");
                        throw new X360UtilsException(X360UtilsException.X360UtilsErrors.UnkownPatchset);
                    default:
                        Main.SendInfo("\r\nUnknown TMS value: {0:X2}", tms);
                        throw new ArgumentOutOfRangeException();
                }
            }

            private static bool FindUnconditionalBoot(ref byte[] smcdata) {
                DecryptCheck(ref smcdata);
                for(var i = 0; i < smcdata.Length - 7; i++) {
                    if(smcdata[i] != 0xC0)
                        continue;
                    if(smcdata[i + 1] != 0x07 || smcdata[i + 2] != 0x00 || smcdata[i + 3] != 0xE5 || smcdata[i + 4] != 0x3D || smcdata[i + 5] != 0xB4 || smcdata[i + 6] != 0x82)
                        continue;
                    if(Main.VerifyVerbosityLevel(1))
                        Main.SendInfo("Unconditional boot patch found @ 0x{0:X}", i);
                    return true;
                }
                return false;
            }

            public static byte GetTMS(ref byte[] smcdata) {
                DecryptCheck(ref smcdata);
                for(var i = 0; i < smcdata.Length; i++) {
                    if(smcdata[i] != 0xD2)
                        continue;
                    if(smcdata[i + 1] != smcdata[i + 9] || smcdata[i + 2] != 0x74 || smcdata[i + 4] != 0xD5 || smcdata[i + 5] != 0xE0 || smcdata[i + 6] != 0xFD || smcdata[i + 7] != 0x22 ||
                       smcdata[i + 8] != 0xC2)
                        continue;
                    if(Main.VerifyVerbosityLevel(1))
                        Main.SendInfo("TMS Found @ offset: 0x{0:X} TMS Value: 0x{1} ({2:X2})\n", i, (TMSTDIValues)smcdata[i + 1], smcdata[i + 1]);
                    return smcdata[i + 1];
                }
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound);
            }

            public static byte GetTDI(ref byte[] smcdata, int num) {
                DecryptCheck(ref smcdata);
                switch(num) {
                    case 0:
                        for(var i = 0; i < smcdata.Length; i++) {
                            if(smcdata[i] != 0x92 || smcdata[i + 4] != 0xDF || smcdata[i + 5] != 0xF8 || smcdata[i + 6] != 0x22)
                                continue;
                            if(Main.VerifyVerbosityLevel(1))
                                Main.SendInfo("TDI0 Found @ offset: 0x{0:X} TDI Value: 0x{1} ({2:X2})\n", i, (TMSTDIValues)smcdata[i + 1], smcdata[i + 1]);
                            return smcdata[i + 1];
                        }
                        break;
                    case 1:
                        for(var i = 0; i < smcdata.Length; i++) {
                            if(smcdata[i] != 0xC2 || smcdata[i + 2] != 0x74 || smcdata[i + 3] != 0x02 || smcdata[i + 8] != 0x74 || smcdata[i + 8] != 0x02)
                                continue;
                            if(Main.VerifyVerbosityLevel(1))
                                Main.SendInfo("TDI1 Found @ offset: 0x{0:X} TDI Value: 0x{1} ({2:X2})\n", i, (TMSTDIValues)smcdata[i + 1], smcdata[i + 1]);
                            return smcdata[i + 1];
                        }
                        break;
                    case 2:
                        for(var i = 0; i < smcdata.Length; i++) {
                            if(smcdata[i] != 0x7F || smcdata[i + 1] != 0x01 || smcdata[i + 4] != 0xC2 || smcdata[i + 6] != 0x74 || smcdata[i + 7] != 0x01)
                                continue;
                            if(Main.VerifyVerbosityLevel(1))
                                Main.SendInfo("TDI2 Found @ offset: 0x{0:X} TDI Value: 0x{1} ({2:X2})\n", i, (TMSTDIValues)smcdata[i + 5], smcdata[i + 5]);
                            return smcdata[i + 5];
                        }
                        break;
                    case 3:
                        for(var i = 0; i < smcdata.Length; i++) {
                            if(smcdata[i] != 0x76 || smcdata[i + 2] != 0x78 || smcdata[i + 4] != 0x76 || smcdata[i + 6] != 0xD2)
                                continue;
                            if(Main.VerifyVerbosityLevel(1))
                                Main.SendInfo("TDI3 Found @ offset: 0x{0:X} TDI Value: 0x{1} ({2:X2})\n", i, (TMSTDIValues)smcdata[i + 7], smcdata[i + 7]);
                            return smcdata[i + 7];
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("num", "num must be between 0 and 3!");
                }
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound);
            }

            public static bool FindDMAReadHack(ref byte[] smcdata) {
                DecryptCheck(ref smcdata);
                for(var i = 0; i < smcdata.Length - 6; i++) {
                    if(smcdata[i] != 0xB4)
                        continue;
                    if(smcdata[i + 1] != 0x04 || smcdata[i + 2] != 0x03 || smcdata[i + 3] != 0x02 || smcdata[i + 6] != 0x02)
                        continue;
                    if(Main.VerifyVerbosityLevel(1))
                        Main.SendInfo("Found DMA Read Hack @ 0x{0:X}", i);
                    return true;
                }
                return false;
            }

            public static bool FindGPUJtagHack(ref byte[] smcdata) {
                DecryptCheck(ref smcdata);
                for(var i = 0; i < smcdata.Length - 6; i++) {
                    if(smcdata[i] != 0xD0)
                        continue;
                    if(smcdata[i + 1] != 0xE0 || smcdata[i + 2] != 0xD8 || smcdata[i + 3] != 0xFC || smcdata[i + 4] != 0x78 || smcdata[i + 6] != 0x76)
                        continue;
                    if(Main.VerifyVerbosityLevel(1))
                        Main.SendInfo("Found GPU JTAG Hack @ 0x{0:X}", i);
                    return true;
                }
                return false;
            }

            public static bool FindAndFixPCIMaskBug(ref byte[] smcdata) {
                DecryptCheck(ref smcdata);
                for(var i = 0; i < smcdata.Length - 8; i++) {
                    if(smcdata[i] != 0x24)
                        continue;
                    if(smcdata[i + 1] != 0x07 || smcdata[i + 2] != 0xD0 || smcdata[i + 3] != 0xE0 || smcdata[i + 4] != 0xF8)
                        continue;
                    if(Main.VerifyVerbosityLevel(1))
                        Main.SendInfo("Found PCI Mask Bug @ 0x{0:X}", i);
                    smcdata[i + 2] = 0xF8;
                    smcdata[i + 3] = 0xD0;
                    smcdata[i + 4] = 0xE0;
                    return true;
                }
                return false;
            }

            public static bool FindPNCCharge(ref byte[] smcdata) {
                DecryptCheck(ref smcdata);
                for(var i = 0; i < smcdata.Length - 8; i++) {
                    if(smcdata[i] != 0x20)
                        continue;
                    if(smcdata[i + 1] != 0x15 || smcdata[i + 2] != 0x0B || smcdata[i + 3] != 0xC0 || smcdata[i + 4] != 0x00 || smcdata[i + 5] != 0x78 || smcdata[i + 7] != 0x76 ||
                       smcdata[i + 8] != 0x00)
                        continue;
                    if(Main.VerifyVerbosityLevel(1))
                        Main.SendInfo("Found PNC Charge patch @ 0x{0:X}", i);
                    return true;
                }
                return false;
            }

            public static bool FindPNCNoCharge(ref byte[] smcdata) {
                DecryptCheck(ref smcdata);
                for(var i = 0; i < smcdata.Length - 8; i++) {
                    if(smcdata[i] != 0xD0)
                        continue;
                    if(smcdata[i + 1] == 0x00 && smcdata[i + 2] == 0x02 && smcdata[i + 5] == 0xD2 && smcdata[i + 6] == 0x04 && smcdata[i + 7] == 0x02) {
                        if(Main.VerifyVerbosityLevel(1))
                            Main.SendInfo("Found PNC No Charge patch @ 0x{0:X}", i);
                        return true;
                    }
                }
                return false;
            }

            public static bool SetTMS(ref byte[] smcdata, byte tms) {
                DecryptCheck(ref smcdata);
                var patched = false;
                for(var i = 0; i < smcdata.Length; i++) {
                    if(smcdata[i] != 0xD2 || smcdata[i + 1] != smcdata[i + 9] || smcdata[i + 2] != 0x74 || smcdata[i + 4] != 0xD5 || smcdata[i + 5] != 0xE0 || smcdata[i + 6] != 0xFD ||
                       smcdata[i + 7] != 0x22 || smcdata[i + 8] != 0xC2)
                        continue;
                    smcdata[i + 1] = tms;
                    smcdata[i + 9] = tms;
                    patched = true;
                    if(Main.VerifyVerbosityLevel(1))
                        Main.SendInfo("TMS Patched @ offset: 0x{0:X} TMS Value: 0x{1} ({2:X2})\n", i, (TMSTDIValues)tms, tms);
                }
                return patched;
            }

            public static bool SetTDI(ref byte[] smcdata, byte tdi, int num = -1) {
                DecryptCheck(ref smcdata);
                var patched = false;
                switch(num) {
                    case -1:
                        Main.SendInfo("Patching TDI in all 4 places...\n");
                        var ret = false;
                        for(var i = 0; i < 4; i++) {
                            if(SetTDI(ref smcdata, tdi, i))
                                ret = true;
                        }
                        return ret;
                    case 0:
                        for(var i = 0; i < smcdata.Length; i++) {
                            if(smcdata[i] != 0x92 || smcdata[i + 4] != 0xDF || smcdata[i + 5] != 0xF8 || smcdata[i + 6] != 0x22)
                                continue;
                            smcdata[i + 1] = tdi;
                            patched = true;
                            if(Main.VerifyVerbosityLevel(1))
                                Main.SendInfo("TDI{1} Patched @ offset: 0x{0:X} TDI Value: 0x{2} ({3:X2})\n", i, num, (TMSTDIValues)tdi, tdi);
                        }
                        break;
                    case 1:
                        for(var i = 0; i < smcdata.Length; i++) {
                            if(smcdata[i] != 0xC2 || smcdata[i + 2] != 0x74 || smcdata[i + 3] != 0x02 || smcdata[i + 8] != 0x74 || smcdata[i + 8] != 0x02)
                                continue;
                            smcdata[i + 1] = tdi;
                            patched = true;
                            if(Main.VerifyVerbosityLevel(1))
                                Main.SendInfo("TDI{1} Patched @ offset: 0x{0:X} TDI Value: 0x{2} ({3:X2})\n", i, num, (TMSTDIValues)tdi, tdi);
                        }
                        break;
                    case 2:
                        for(var i = 0; i < smcdata.Length; i++) {
                            if(smcdata[i] != 0x7F || smcdata[i + 1] != 0x01 || smcdata[i + 4] != 0xC2 || smcdata[i + 6] != 0x74 || smcdata[i + 7] != 0x01)
                                continue;
                            smcdata[i + 1] = tdi;
                            patched = true;
                            if(Main.VerifyVerbosityLevel(1))
                                Main.SendInfo("TDI{1} Patched @ offset: 0x{0:X} TDI Value: 0x{2} ({3:X2})\n", i, num, (TMSTDIValues)tdi, tdi);
                        }
                        break;
                    case 3:
                        for(var i = 0; i < smcdata.Length; i++) {
                            if(smcdata[i] != 0x76 || smcdata[i + 2] != 0x78 || smcdata[i + 4] != 0x76 || smcdata[i + 6] != 0xD2)
                                continue;
                            smcdata[i + 1] = tdi;
                            patched = true;
                            if(Main.VerifyVerbosityLevel(1))
                                Main.SendInfo("TDI{1} Patched @ offset: 0x{0:X} TDI Value: 0x{2} ({3:X2})\n", i, num, (TMSTDIValues)tdi, tdi);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("num", "num must be between -1 and 3! (-1 = auto patch all with the same TDI data)");
                }
                return patched;
            }

            public static void DisablePNCCharge(ref byte[] smcdata) {
                DecryptCheck(ref smcdata);
                for(var i = 0; i < smcdata.Length - 8; i++) {
                    if(smcdata[i] != 0xD0)
                        continue;
                    if(smcdata[i + 1] != 0x00 || smcdata[i + 2] != 0x02 || smcdata[i + 5] != 0xD2 || smcdata[i + 7] != 0x02)
                        continue;
                    if(Main.VerifyVerbosityLevel(1))
                        Main.SendInfo("Disable PNC Charge patch applied @ 0x{0:X}", i);
                    smcdata[i + 6] = 0x04;
                    return;
                }
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound);
            }

            public static void UnconditionalBoot(ref byte[] smcdata) {
                DecryptCheck(ref smcdata);
                for(var i = 0; i < smcdata.Length - 7; i++) {
                    if(smcdata[i] != 0xC0)
                        continue;
                    if(smcdata[i + 1] != 0x07 || smcdata[i + 2] != 0x78 || smcdata[i + 4] != 0xE6 || smcdata[i + 5] != 0xB4 || smcdata[i + 6] != 0x00)
                        continue;
                    if(Main.VerifyVerbosityLevel(1))
                        Main.SendInfo("Unconditional boot patch applied @ 0x{0:X}", i);
                    smcdata[i + 2] = 0x00;
                    smcdata[i + 3] = 0xE5;
                    smcdata[i + 4] = 0x3D;
                    smcdata[i + 6] = 0x82;
                    return;
                }
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound);
            }
        }

        #endregion
    }
}