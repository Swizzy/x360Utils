namespace x360Utils.Network {
    using System;
    using System.IO;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using x360Utils.CPUKey;

    public class XeLL {
        internal static void FuseDownloader(string ip)
        {
            using (var client = new WebClientWithTimeout())
            {
                try
                {
                    client.DownloadFile(string.Format("http://{0}/FUSE", ip), "FUSE.txt");
                }
                catch
                {
                    if (File.Exists("FUSE.txt"))
                        File.Delete("FUSE.txt");
                    throw new XeLLNetworkException("FUSE Download FAILED!");
                }
            }
        }

        internal static void FuseDownloader(IPAddress ip)
        {
            FuseDownloader(ip.ToString());
        }

        public string GetKeyFromXeLL(string ip) {
            IPAddress ipcheck;
            if(!IPAddress.TryParse(ip, out ipcheck))
                throw new ArgumentException("Bad IP Input!");
            return GetKeyFromXeLL(ipcheck);
        }

        public string GetKeyFromXeLL(IPAddress ipadrAddress) {
            switch(ipadrAddress.AddressFamily) {
                case AddressFamily.InterNetwork:
                    var pinger = new Ping();
                    var reply = pinger.Send(ipadrAddress, 1000);
                    if(reply != null && reply.Status != IPStatus.Success)
                        reply = pinger.Send(ipadrAddress, 1000);
                    if(reply == null || reply.Status != IPStatus.Success)
                        throw new TimeoutException(string.Format("Ping Timeout for {0}", ipadrAddress));
                    FuseDownloader(ipadrAddress);
                    var keyutils = new CpukeyUtils();
                    return keyutils.GetCPUKeyFromTextFile("FUSE.txt");
                default:
                    throw new NotSupportedException("IP Must be IPv4!");
            }
        }

        public IPAddress FindXeLL(string baseip, string macAddress = "") {
            var scanner = new XeLLNetworkScanner(baseip, macAddress);
            scanner.ScanForXeLL();
            return scanner.XeLLIPAddress;
        }
    }
}