#region

using System;
using System.Globalization;
using x360Utils.Common;

#endregion

namespace x360Utils.NAND {
    using System.Collections.Generic;
    using System.Text;
    using System.Text.RegularExpressions;

    public sealed class SMCConfig {
        #region SMCConfigFans enum

        public enum SMCConfigFans {
            CPU = 0x11,
            GPU = 0x12
        }

        #endregion

        #region SMCConfigTemps enum

        public enum SMCConfigTemps {
            CPU = 0x29,
            CPUMax = 0x2C,
            GPU = 0x2A,
            GPUMax = 0x2D,
            RAM = 0x2B,
            RAMMax = 0x2E
        }

        #endregion

        private static uint CalculateSMCCheckSum(IList<byte> smcConfig) {
            uint i, len, sum = 0;
            for (i = 0, len = 252; i < len; i++)
                sum += (uint) smcConfig[(int) (i + 0x10)] & 0xFF;
            return (~sum & 0xFFFF);
        }

        public void VerifySMCConfigChecksum(byte[] smcconfigdata) {
            var checkSum = BitConverter.ToUInt16(smcconfigdata, 0);
            var calculatedCheckSum = CalculateSMCCheckSum(smcconfigdata);
            if (checkSum == calculatedCheckSum)
                return;
            if (Main.VerifyVerbosityLevel(1))
                Main.SendInfo("ERROR: SMC_Config Checksums don't match! Expected: {0:X4} Calculated: {1:X4}", checkSum, calculatedCheckSum);
            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.BadChecksum);
        }

        public string GetTempString(ref byte[] smcconfigdata, SMCConfigTemps temp) {
            return string.Format("{0}°C", smcconfigdata[(int) temp]);
        }

        public string GetFanSpeed(ref byte[] smcconfigdata, SMCConfigFans fan) {
            switch (smcconfigdata[(int) fan] & 128) {
                case 0:
                case 127:
                    return "AUTO";
                default:
                    return string.Format("{0}%", smcconfigdata[(int) fan] & 127);
            }
        }

        public string GetVideoRegion(ref byte[] smcconfigdata) {
            return
                Translators.TranslateVideoRegion(string.Format("0x{0:X}{1:X2}", smcconfigdata[0x22A],
                                                               smcconfigdata[0x22B]));
        }

        public string GetGameRegion(ref byte[] smcconfigdata, bool includebytes = false) {
            return
                Translators.TranslateGameRegion(
                    string.Format("0x{0:X2}{1:X2}", smcconfigdata[0x22C], smcconfigdata[0x22D]), includebytes);
        }

        public string GetDVDRegion(ref byte[] smcconfigdata) {
            return Translators.TranslateDVDRegion(smcconfigdata[0x237].ToString(CultureInfo.InvariantCulture));
        }

        public string GetCheckSum(ref byte[] smcconfigdata) {
            return string.Format("0x{0:X2}{1:X2}", smcconfigdata[0], smcconfigdata[1]);
        }

        public string GetMACAdress(ref byte[] smcconfigdata) {
            return string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}", smcconfigdata[0x220], smcconfigdata[0x221],
                                 smcconfigdata[0x222], smcconfigdata[0x223], smcconfigdata[0x224], smcconfigdata[0x225]);
        }

        private static string TranslateResetCode(char code) {
            switch(code) {
                case 'A':
                case 'a':
                case 'X':
                case 'x':
                case 'Y':
                case 'y':
                    return string.Format("{0} Button", code.ToString().ToUpper());
                case 'D':
                case 'd':
                    return "D-PAD Down";
                case 'U':
                case 'u':
                    return "D-PAD Up";
                case 'L':
                case 'l':
                    return "D-PAD Left";
                case 'R':
                case 'r':
                    return "D-PAD Right";
                default:
                    throw new ArgumentOutOfRangeException("code", "The reset code is invalid!");
            }
        }

        private static IEnumerable<char> GetResetCodeArray(string codeline) {
            if (VerifyResetCodeLine(codeline))
                return codeline.ToCharArray();
            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataInvalid);
        }

        private static bool VerifyResetCodeLine(string codeline) {
            return Regex.IsMatch(codeline, "[AXYDULRaxydulr]{4}");
        }

        public string GetResetCode(ref byte[] smcconfigdata, bool translate = false) {
            var codeline = Encoding.ASCII.GetString(smcconfigdata, 0x238, 4);
            if(!translate) {
                if (VerifyResetCodeLine(codeline))
                    return codeline;
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataInvalid);
            }
            var ret = "";
            foreach (var code in GetResetCodeArray(codeline)) {
                ret += TranslateResetCode(code);
                ret += ", ";
            }
            return ret.Substring(0, ret.Length - 2);
        }
    }
}