using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace apod_wallpaper
{
    class Network
    {
        public static void AllowInvalidCertificate()
        {
            ServicePointManager.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback(AcceptAllCertifications);
        }

        public static bool AcceptAllCertifications(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certification, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public static void SetCredentails(WebClient client)
        {
            client.Proxy = WebRequest.GetSystemWebProxy();
            client.Credentials = CredentialCache.DefaultCredentials;
            client.Proxy.Credentials = CredentialCache.DefaultCredentials;
        }
    }
}