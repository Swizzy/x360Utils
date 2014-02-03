#region



#endregion

namespace x360Utils.NAND {
    using System;
    using System.Text;
    using System.Text.RegularExpressions;
    using x360Utils.Common;

    public class Keyvault {
        #region DateFormats enum

        public enum DateFormats {
            // ReSharper disable InconsistentNaming
            YYMMDD,
            DDMMYY,
            MMYYDD,
            DDYYMM,
            MMDDYY,
            YYDDMM
            // ReSharper restore InconsistentNaming
        }

        #endregion

        public ushort GetFCRTFlag(ref byte[] keyvaultdata) { return BitOperations.Swap(BitConverter.ToUInt16(keyvaultdata, 0x1C)); }

        public bool FCRTRequired(ref byte[] keyvaultdata) { return FCRTRequired(GetFCRTFlag(ref keyvaultdata)); }

        public bool FCRTRequired(ushort fcrtflag) { return (fcrtflag & 0x120) == 0x120; }

        public bool FCRTUsed(ref byte[] keyvaultdata) { return FCRTUsed(GetFCRTFlag(ref keyvaultdata)); }

        public bool FCRTUsed(ushort fcrtflag) { return (fcrtflag & 0x20) == 0x20; }

        public string GetGameRegion(ref byte[] keyvaultdata, bool includebytes = false) { return Translators.TranslateGameRegion(string.Format("0x{0:X2}{1:X2}", keyvaultdata[0xC8], keyvaultdata[0xC9]), includebytes); }

        public string GetDVDKey(ref byte[] keyvaultdata) { return StringUtils.ArrayToHex(keyvaultdata, 0x100, 0x10); }

        public string GetConsoleID(ref byte[] keyvaultdata) { return StringUtils.ArrayToHex(keyvaultdata, 0x9CA, 0x6); }

        public string GetMfrDate(ref byte[] keyvaultdata, DateFormats format) {
            var ret = Encoding.ASCII.GetString(keyvaultdata, 0x9E4, 8);
            if(!Regex.IsMatch(ret, "^[0-9]{2}-[0-9]{2}-[0-9]{2}$"))
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataInvalid);
            var split = ret.Split('-');
            switch(format) {
                case DateFormats.YYMMDD:
                    return string.Format("{0}-{1}-{2}", split[1], split[0], split[2]);
                case DateFormats.DDMMYY:
                    return string.Format("{0}-{1}-{2}", split[2], split[0], split[1]);
                case DateFormats.MMYYDD:
                    return string.Format("{0}-{1}-{2}", split[0], split[1], split[2]);
                case DateFormats.DDYYMM:
                    return string.Format("{0}-{1}-{2}", split[2], split[1], split[0]);
                case DateFormats.MMDDYY:
                    return string.Format("{0}-{1}-{2}", split[0], split[2], split[1]);
                case DateFormats.YYDDMM:
                    return string.Format("{0}-{1}-{2}", split[1], split[2], split[0]);
                default:
                    throw new ArgumentOutOfRangeException("format");
            }
        }

        public string GetOSIGData(ref byte[] keyvaultdata) { return StringUtils.GetAciiString(ref keyvaultdata, 0xC92, 0x1C, true); }

        public string GetConsoleSerial(ref byte[] keyvaultdata) { return StringUtils.GetAciiString(ref keyvaultdata, 0xB0, 0x10); }
    }
}