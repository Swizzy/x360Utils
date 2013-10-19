namespace x360Utils.Common
{
    using System;

    public static class Translators
    {
        public static string TranslateVideoRegion(string videoregion) {
            switch (videoregion)
            {
                case "0x100":
                    return "NTSC-U (0x100)";
                case "0x200":
                    return "NTSC-J (0x200)";
                case "0x300":
                    return "PAL (0x300)";
                case "NTSC-U (0x100)":
                    return "0x100";
                case "NTSC-J (0x200)":
                    return "0x200";
                case "PAL (0x300)":
                    return "0x300";
                default:
                    return videoregion.StartsWith("Unkown", StringComparison.Ordinal) ? videoregion.Substring(8, videoregion.Length - 9) : string.Format("Unkown ({0})", videoregion);
            }
        }

        public static string TranslateDVDRegion(string dvdregion) {
            if (dvdregion.Trim().Length > 1)
                return dvdregion.Trim().Substring(0, 1);
            switch (dvdregion)
            {
                case "1":
                    return "1 (North America)";
                case "2":
                    return "2 (Europe)";
                case "3":
                    return "3 (South East Asia)";
                case "4":
                    return "4 (Australia)";
                case "5":
                    return "5 (Russia/South Asia)";
                case "6":
                    return "6 (China)";
                case "8":
                    return "8 (Aircrafts etc.)";
                default:
                    return string.Format("{0} (Unkown)", dvdregion);
            }
        }

        public static string TranslateGameRegion(string gameregion, bool includebytes = false) {
            switch (gameregion)
            {
                case "":
                case null:
                case "Unkown":
                    return "";
                case "0x02FE":
                    return !includebytes ? "PAL/Europe" : "PAL/Europe (0x02FE)";
                case "PAL/Europe":
                    return "0x02FE";
                case "0x0201":
                    return !includebytes ? "PAL/Australia" : "PAL/Australia (0x0201)";
                case "PAL/Australia":
                    return "0x0201";
                case "0x00FF":
                    return !includebytes ? "NTSC/USA" : "NTSC/USA (0x00FF)";
                case "NTSC/USA":
                    return "0x00FF";
                case "0x01FE":
                    return !includebytes ? "NTSC/Japan" : "NTSC/Japan (0x01FE)";
                case "NTSC/Japan":
                    return "0x01FE";
                case "0x01FC":
                    return !includebytes ? "NTSC/Korea" : "NTSC/Korea (0x01FC)";
                case "NTSC/Korea":
                    return "0x01FC";
                case "0x0101":
                    return !includebytes ? "NTSC/Hong Kong" : "NTSC/Hong Kong (0x0101)";
                case "NTSC/Hong Kong":
                    return "0x0101";
                case "0x7FFF":
                    return !includebytes ? "Devkit" : "Devkit (0x7FFF)";
                case "Devkit":
                    return "0x7FFF";
                default:
                    return !includebytes ? "Unkown" : string.Format("Unkown ({0})", gameregion);
            }
        }
    }
}
