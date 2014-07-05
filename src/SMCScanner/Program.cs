namespace SMCScanner {
    using System;
    using System.IO;
    using x360Utils.NAND;

    internal class Program {
        private static readonly X360NAND NAND = new X360NAND();
        private static readonly Smc Smc = new Smc();

        private static void CheckSmc(string file) {
            try {
                Console.WriteLine("Checking {0}:", file);
                var smc = NAND.GetSmc(new NANDReader(file, true), true);
                if(Smc.GetType(ref smc) != Smc.SmcTypes.Retail)
                    return;
                var ver = Smc.GetVersion(ref smc);
                Console.WriteLine("SMC Is clean... version: {0}", ver);
                if(!File.Exists(ver + ".bin")) {
                    Console.WriteLine("Saving it!");
                    File.WriteAllBytes(ver + ".bin", smc);
                }
            }
            catch { }
        }

        private static void Main(string[] args) { ScanDir(args[0]); }

        private static void ScanDir(string dir) {
            foreach(var tmp in Directory.GetDirectories(dir))
                ScanDir(tmp);
            foreach(var tmp in Directory.GetFiles(dir))
                CheckSmc(tmp);
        }
    }
}