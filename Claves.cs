using Amazon.Runtime; // Importar el espacio de nombres necesario para BasicAWSCredentials
using System.Net; // Importar el espacio de nombres necesario para IPAddress

namespace FTPSERVER
{
    /* The class "Claves" contains static properties for defining FTP server IP address, port, AWS
    Access Key ID, Secret Access Key, and BasicAWSCredentials object. */
    public class Claves
    {
        // Definir IPAddress para el servidor FTP
        // Define IPAddress for the FTP server
        public static IPAddress IpAddress { get; } = IPAddress.Parse("192.168.1.130");

        // Definir Puerto para el servidor FTP
        // Define Port for the FTP server
        public static int Port { get; } = 80;

        // Definir AWS Access Key ID para acceder a los servicios de AWS
        // Define AWS Access Key ID for accessing AWS services
        public static string AccessKeyId { get; } = "";

        // Definir Secret Access Key para acceder a los servicios de AWS
        // Define Secret Access Key for accessing AWS services
        public static string SecretAccessKey { get; } = "";

        // Crear objeto BasicAWSCredentials utilizando el Access Key ID y Secret Access Key
        // Create BasicAWSCredentials object using the Access Key ID and Secret Access Key
        public static BasicAWSCredentials Credentials { get; } = new BasicAWSCredentials(AccessKeyId, SecretAccessKey);
    }
}