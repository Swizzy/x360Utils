namespace SMCCheck {
    using System;
    using System.IO;
    using x360Utils.NAND;

    internal static class Program {
        private static void Main(string[] args) {
            if(args.Length < 1)
                return;
            x360Utils.Main.VerbosityLevel = (int)x360Utils.Main.VerbosityLevels.FullDebug;
            x360Utils.Main.InfoOutput += (sender, arg) => Console.WriteLine(arg.Data);
            var smc = new Smc(File.ReadAllBytes(args[0]));
            smc.Decrypt();
            smc.Analyze();
            Console.WriteLine("SMC Type: {0}", smc.Type);
            Console.WriteLine("SMC Version: {0}", smc.VersionString);
            Console.WriteLine("SMC Motherboard: {0}", smc.Motherboard);
            if(smc.CheckIsJtag()) {
                Console.WriteLine("Dma Read Hack: {0}", smc.DmaReadHack);
                Console.WriteLine("GPU JTAG Hack: {0}", smc.GpuJtagHack);
                Console.WriteLine("PCI Mask Bug: {0}", smc.PciMaskBug);
                Console.WriteLine("UnconditionalBoot: {0}", smc.UnconditionalJtagBoot);
                Console.WriteLine("PNC Charge: {0}", smc.PncCharge);
                Console.WriteLine("PNC No Charge: {0}", smc.PncNoCharge);
                try {
                    Console.WriteLine("TMS: {0}", smc.Tms);
                }
                catch {
                    Console.WriteLine("TMS: <Not Found>");
                }
                Console.WriteLine("TDI: {0}", smc.Tdi);
            }
            Console.WriteLine("Hit <ENTER> to exit");
            Console.ReadLine();
        }
    }
}