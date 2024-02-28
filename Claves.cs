using Amazon.Runtime;
using System.Net;

namespace FTPSERVER
{
    public class Claves
    {
        public static IPAddress IpAddress { get; } = IPAddress.Parse("");
        public static int Port { get; } = 1234;
        public static string AccessKeyId { get; } = "";
        public static string SecretAccessKey { get; } = "";
        public static BasicAWSCredentials Credentials { get; } = new BasicAWSCredentials(AccessKeyId, SecretAccessKey);
    }
}