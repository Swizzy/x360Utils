namespace x360Utils.Network {
    using System;
    using System.Net;

    public sealed class WebClientWithTimeout: WebClient {
        public WebClientWithTimeout() { Timeout = 60000; }

        public WebClientWithTimeout(int timeout) { Timeout = timeout; }

        public int Timeout { get; set; }

        protected override WebRequest GetWebRequest(Uri address) {
            var result = base.GetWebRequest(address);
            if(result == null)
                return null;
            result.Timeout = Timeout;
            return result;
        }
    }
}