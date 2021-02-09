using FSMExtension.Models;
using Microsoft.AspNetCore.Http;

namespace FSMExtension
{
    public static class Utils
    {
        public static OnsightConnectPlatform DetectPlatform(HttpRequest request = null)
        {
            var userAgent = request.Headers["User-Agent"].ToString();

            if (userAgent.Contains("iPhone") || userAgent.Contains("iPad") || userAgent.Contains("iOS"))
                return OnsightConnectPlatform.iOS;
            // macOS not directly supported; also, iPads by default now pretend to be desktop Macs, making
            // distinguishing them from ordinary Macs impossible. So, treat all Apple products as iOS devices.
            if (userAgent.Contains("Macintosh"))
                return OnsightConnectPlatform.iOS;
            if (userAgent.Contains("Android"))
                return OnsightConnectPlatform.Android;

            return OnsightConnectPlatform.PC;
        }

        public static string ExtractEmailDomain(string emailAddress)
        {
            if (string.IsNullOrEmpty(emailAddress))
                return null;

            var index = emailAddress.IndexOf('@');
            if (index <= 0)
                return null;

            return emailAddress.Substring(index + 1);
        }
    }
}
