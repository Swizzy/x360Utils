
#define WINAPI

namespace x360Utils.Common {
    using System;
#if WINAPI
    using System.Runtime.InteropServices;
    using System.ComponentModel;

#endif

    public static class DateTimeUtils {
#if WINAPI

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)] private static extern bool DosDateTimeToFileTime(ushort dateValue, ushort timeValue,
                                                                                                                                                         out ulong fileTime);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)] private static extern bool FileTimeToDosDateTime(ref long fileTime, out ushort dosDate,
                                                                                                                                                         out ushort dosTime);

#endif

        public static DateTime DosTimeStampToDateTime(int timestamp) {
#if WINAPI
            UInt64 filetime;
            if(!DosDateTimeToFileTime((ushort)(timestamp >> 16), (ushort)(timestamp & 0xFFFF), out filetime))
                throw new Win32Exception();
            return DateTime.FromFileTime((long)filetime);
#else
            //var years = 1980 + (timestamp >> 25); // We want bits 9-15 from the higher end
            //var months = timestamp >> 21 & 0xF; // We want bits 5-8 from the higher end
            //var days = timestamp >> 16 & 0x1F; // We want bits 0-4 from the higher end
            //var hours = timestamp >> 11 & 0x1F; // We want bits 11-15 only
            //var minutes = timestamp >> 5 & 0xFF; // We want bits 5-10 only
            //var seconds = (timestamp & 0x1F) * 2; // We want bits 0-4 only
            //return new DateTime(years, months, days, hours, minutes, seconds).ToLocalTime();
            return new DateTime(1980 + (timestamp >> 25), timestamp >> 21 & 0xF, timestamp >> 16 & 0x1F, timestamp >> 11 & 0x1F, timestamp >> 5 & 0xFF, (timestamp & 0x1F) * 2).ToLocalTime();
#endif
        }

        public static uint DateTimetoDosTimeStamp(DateTime dateTime) {
#if WINAPI
            ushort dosDate, dosTime;
            var ft = dateTime.ToFileTime();
            if (!FileTimeToDosDateTime(ref ft, out dosDate, out dosTime))
                throw new Win32Exception();
            return (uint)(dosDate << 16 | dosTime);
#else
            //var ret = 0;
            //ret |= (dateTime.Year - 1980) << 25;
            //ret |= dateTime.Month << 21;
            //ret |= dateTime.Day << 16;
            //ret |= dateTime.Hour << 11;
            //ret |= dateTime.Minute << 5;
            //ret |= dateTime.Second / 2;
            //return ret;
            return (uint)((dateTime.Year - 1980) << 25 | dateTime.Month << 21 | dateTime.Day << 16 | dateTime.Hour << 11 | dateTime.Minute << 5 | dateTime.Second / 2);
#endif
        }

        public static DateTime DosTimeStampToDateTime(uint timeStamp) { return DosTimeStampToDateTime((int)timeStamp); }
    }
}