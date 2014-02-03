namespace x360Utils.Network {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Text.RegularExpressions;
    using System.Threading;

    internal sealed class XeLLNetworkScanner {
        private readonly string _macAddress;
        private readonly List<IPAddress> _responselist = new List<IPAddress>();
        public IPAddress XeLLIPAddress;
        private string _baseip;

        public XeLLNetworkScanner(string baseip, string macAddress) {
            _macAddress = macAddress;
            _baseip = baseip;
        }

        internal void ScanForXeLL() {
            _responselist.Clear();
            var basetest = _baseip.Split('.');
            if((basetest.Length == 4) || (basetest.Length == 3)) {
                _baseip = string.Format("{0}.{1}.{2}.", basetest[0], basetest[1], basetest[2]);
                Main.SendInfo("Scannning network for your console... Pinging full network for response...");
                var lastping = DateTime.Now;
                for(var j = 0; j < 2; j++) {
                    for(var i = 0; i < 255; i++) {
                        var ip = string.Format("{0}{1}", _baseip, i);
                        var p = new Ping();
                        p.PingCompleted += PingCompleted;
                        p.SendAsync(ip, 500, ip);
                        lastping = DateTime.Now;
                    }
                }
                var localIPs = Dns.GetHostAddresses(Dns.GetHostName());
                var ipfull = "";
                for(var i = 0; i < localIPs.Length; i++) {
                    if(localIPs[i].AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    ipfull = localIPs[i].ToString();
                    var split = ipfull.Split('.');
                    if(_baseip.Equals(string.Format("{0}.{1}.{2}.", split[0], split[1], split[2])))
                        break;
                }
                Main.SendInfo("Scannning network for your console... Waiting for pings to complete...");
                while((DateTime.Now - lastping).TotalMilliseconds < 500)
                    Thread.Sleep(100);
                Main.SendInfo("Scannning network for your console... Looking for console response based on MAC Address...");
                var proc = new Process {
                    StartInfo = {
                        FileName = "arp",
                        Arguments = !string.IsNullOrEmpty(ipfull) ? "-a -N " + ipfull.Trim() : "-a",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    }
                };
                proc.Start();
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                var lines = output.Split('\n');
                var tmp = new Dictionary<string, string>();
                for(var i = 0; i < lines.Length; i++) {
                    if(string.IsNullOrEmpty(lines[i]))
                        continue;
                    lines[i] = Regex.Replace(lines[i].Trim(), "\\s+", " ");
                    var tmpl = lines[i].Split(' ');
                    if(tmpl.Length <= 2)
                        continue;
                    IPAddress trash;
                    if(!IPAddress.TryParse(tmpl[0], out trash))
                        continue;
                    try {
                        tmp.Add(tmpl[1].ToUpper(), tmpl[0]);
                    }
                    catch {
                    }
                }
                try {
                    XeLLIPAddress = IPAddress.Parse(tmp[_macAddress]);
                }
                catch {
                    if(!Scanfailsafe())
                        throw new XeLLNetworkException("Can't find your console :'(");
                }
            }
            else
                throw new ArgumentException("Invalid Base IP!");
        }

        private bool Failsaferesponse(IPAddress ip) {
            try {
                XeLL.FuseDownloader(ip);
                Main.SendInfo(string.Format("Success! Found console on {0}", ip));
                XeLLIPAddress = ip;
                return true;
            }
            catch(XeLLNetworkException) {
                return false;
            }
        }

        private void PingCompleted(object sender, PingCompletedEventArgs e) {
            if(e.Reply.Status != IPStatus.Success)
                return;
            var ip = e.Reply.Address;
            if(!_responselist.Contains(ip))
                _responselist.Add(ip);
        }

        private bool Scanfailsafe() {
            Main.SendInfo("Scannning network for your console... Trying failsafe...");
            var response = 0;
            foreach(var s in _responselist) {
                var p = new Ping();
                var pingReply = p.Send(s, 500);
                if(pingReply == null || pingReply.Status != IPStatus.Success)
                    continue;
                response++;
                if(Failsaferesponse(s))
                    return true;
            }
            Main.SendInfo(string.Format("Network scan FAILED! Got response from {0} of {1} during failsafe...", response, _responselist.Count));
            return false;
        }
    }
}