namespace x360Utils.NAND {
    using System;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;
    using x360Utils.Common;

    public class SmcConfig {
        #region SMCConfigFans enum

        public enum SmcConfigFans {
            Cpu = 0x11,
            Gpu = 0x12
        }

        #endregion

        #region SMCConfigTemps enum

        public enum SmcConfigTemps {
            Cpu = 0x29,
            CpuMax = 0x2C,
            Gpu = 0x2A,
            GpuMax = 0x2D,
            Ram = 0x2B,
            RamMax = 0x2E
        }

        #endregion

        public readonly byte[] Data;
        private readonly bool _valid;

        public SmcConfig(byte[] data) {
            Data = data;
            _valid = VerifySmcConfigChecksum();
        }

        public string FanSettings {
            get {
                var cpu = GetFanSpeed(SmcConfigFans.Cpu);
                var gpu = GetFanSpeed(SmcConfigFans.Gpu);
                return cpu.Equals(gpu) ? cpu : string.Format("CPUFan: {0} / GPUFan: {1}", cpu, gpu);
            }
        }

        public string TempSettings { get { return string.Format("CPU: {0} GPU: {1} RAM: {2}", CpuTemps, GpuTemps, RamTemps); } }

        public string CpuTemp { get { return GetTempString(SmcConfigTemps.Cpu); } }

        public string CpuMaxTemp { get { return GetTempString(SmcConfigTemps.CpuMax); } }

        public string CpuTemps { get { return string.Format("{0} / {1}", CpuTemp, CpuMaxTemp); } }

        public string GpuTemp { get { return GetTempString(SmcConfigTemps.Gpu); } }

        public string GpuMaxTemp { get { return GetTempString(SmcConfigTemps.GpuMax); } }

        public string GpuTemps { get { return string.Format("{0} / {1}", GpuTemp, GpuMaxTemp); } }

        public string RamTemp { get { return GetTempString(SmcConfigTemps.Ram); } }

        public string RamMaxTemp { get { return GetTempString(SmcConfigTemps.RamMax); } }

        public string RamTemps { get { return string.Format("{0} / {1}", RamTemp, RamMaxTemp); } }

        public string CpuFanSpeed { get { return GetFanSpeed(SmcConfigFans.Cpu); } }

        public string GpuFanSpeed { get { return GetFanSpeed(SmcConfigFans.Gpu); } }

        public string VideoRegion { get { return Translators.TranslateVideoRegion(VideoRegionHex); } }

        public string VideoRegionHex {
            get {
                if(!_valid)
                    throw new InvalidOperationException();
                return string.Format("0x{0:X2}{1:X2}", Data[0x22C], Data[0x22D]);
            }
        }

        public string DvdRegionTranslated { get { return Translators.TranslateDVDRegion(DvdRegion); } }

        public string DvdRegion {
            get {
                if(!_valid)
                    throw new InvalidOperationException();
                return Data[0x237].ToString(CultureInfo.InvariantCulture);
            }
        }

        public string GameRegionHex {
            get {
                if(!_valid)
                    throw new InvalidOperationException();
                return string.Format("0x{0:X2}{1:X2}", Data[0x22C], Data[0x22D]);
            }
        }

        public string GameRegion { get { return Translators.TranslateGameRegion(GameRegionHex); } }

        public string MacAddress {
            get {
                if(!_valid)
                    throw new InvalidOperationException();
                return string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}", Data[0x220], Data[0x221], Data[0x222], Data[0x223], Data[0x224], Data[0x225]);
            }
        }

        public string ResetCode {
            get {
                if(!_valid)
                    throw new InvalidOperationException();
                return Encoding.ASCII.GetString(Data, 0x238, 4);
            }
        }

        public string ResetCodeReadable {
            get {
                if(!VerifyResetCodeLine())
                    return string.Format("!ERROR! {0}", ResetCode);
                var ret = "";
                foreach(var c in ResetCode)
                    ret = string.Format("{0} {1}", ret, TranslateResetCode(c));
                return ret;
            }
        }

        public bool VerifyResetCodeLine() { return Regex.IsMatch(ResetCode, "[AXYDULRaxydulr]{4}"); }

        private static string TranslateResetCode(char code) {
            switch(code) {
                case 'A':
                case 'a':
                case 'X':
                case 'x':
                case 'Y':
                case 'y':
                    return string.Format("{0} Button", code.ToString(CultureInfo.InvariantCulture).ToUpper());
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

        private uint CalculateSmcCheckSum() {
            uint sum = 0;
            for(int i = 0, len = 252; i < len; i++)
                sum += (uint)Data[i + 0x10] & 0xFF;
            return (~sum & 0xFFFF);
        }

        public bool VerifySmcConfigChecksum() {
            var checkSum = BitConverter.ToUInt16(Data, 0);
            var calculatedCheckSum = CalculateSmcCheckSum();
            if(checkSum == calculatedCheckSum)
                return true;
            Main.SendInfo(Main.VerbosityLevels.Low, "ERROR: SMC_Config Checksums don't match! Expected: {0:X4} Calculated: {1:X4}", checkSum, calculatedCheckSum);
            return false;
        }

        public string GetTempString(SmcConfigTemps temp) {
            if(!_valid)
                throw new InvalidOperationException();
            return string.Format("{0}°C", Data[(int)temp]);
        }

        public string GetFanSpeed(SmcConfigFans fan) {
            if(!_valid)
                throw new InvalidOperationException();
            switch(Data[(int)fan] & 128) {
                case 0:
                case 127:
                    return "AUTO";
                default:
                    return string.Format("{0}%", Data[(int)fan] & 127);
            }
        }
    }
}