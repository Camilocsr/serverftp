#pragma warning disable CS8603
#pragma warning disable CS8618
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.Polly;
using Amazon.Polly.Model;
using NAudio.Wave;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using FTPSERVER;


class Program
{
    static string transcribedText;
    /// <summary>
    /// The Main function sets up a TCP listener on a specified IP address and port, accepts incoming
    /// client connections, and handles each client connection in a separate task.
    /// </summary>
    static void Main()
    {
        TcpListener listener = new TcpListener(Claves.IpAddress, Claves.Port);

        try
        {
            listener.Start();
            Console.WriteLine($"Servidor CSR 😁 FTP iniciado en {Claves.IpAddress}:{Claves.Port}");

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine("Cliente conectado.");

                Task.Run(() => HandleClient(client));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// The HandleClient function asynchronously reads audio data from a TcpClient stream, saves it
    /// locally, sends it to a server, transcribes it, and generates audio using Polly.
    /// </summary>
    /// <param name="TcpClient">The `TcpClient` parameter in the `HandleClient` method represents a TCP
    /// client connection. It is used to communicate over a TCP connection with a remote host. In the
    /// provided code snippet, the `TcpClient` object is passed to the `HandleClient` method to handle
    /// communication with the client</param>
    static async Task HandleClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream();

        while (true)
        {
            byte[] headerSizeBytes = new byte[4];
            int bytesRead = await stream.ReadAsync(headerSizeBytes, 0, headerSizeBytes.Length);

            if (bytesRead == 0)
            {
                Console.WriteLine("Cliente desconectado.");
                break;
            }

            int headerSize = BitConverter.ToInt32(headerSizeBytes, 0);

            byte[] headerBytes = new byte[headerSize];
            bytesRead = await stream.ReadAsync(headerBytes, 0, headerBytes.Length);

            if (bytesRead == 0)
            {
                Console.WriteLine("Cliente desconectado.");
                break;
            }

            string header = System.Text.Encoding.UTF8.GetString(headerBytes);

            
            if (header == "AUDIO")
            {
                byte[] fileSizeBytes = new byte[4];
                bytesRead = await stream.ReadAsync(fileSizeBytes, 0, fileSizeBytes.Length);

                if (bytesRead == 0)
                {
                    Console.WriteLine("Cliente desconectado.");
                    break;
                }

                int fileSize = BitConverter.ToInt32(fileSizeBytes, 0);

                byte[] fileData = new byte[fileSize];
                int bytesReadTotal = 0;
                int bytesReadThisTime;

                while (bytesReadTotal < fileSize)
                {
                    bytesReadThisTime = await stream.ReadAsync(fileData, bytesReadTotal, fileSize - bytesReadTotal);

                    if (bytesReadThisTime == 0)
                    {
                        throw new Exception("Error al recibir datos de audio: se esperaba más datos pero se recibieron cero.");
                    }

                    bytesReadTotal += bytesReadThisTime;
                }

                Console.WriteLine("Audio recibido de Unity.");
                await SaveAudioLocally(fileData);
                transcribedText = await SendFileToServer("guardar/audio_recibido.wav");

                Stopwatch stopwatch = Stopwatch.StartNew();

                stopwatch.Restart();

                Console.WriteLine($"Tiempo total de EnvioServerHttp: {stopwatch.ElapsedMilliseconds / 1000.0} segundos");

                Console.WriteLine(transcribedText);

                GeneratePollyAudio(transcribedText, stream);
            }
            else if (header == "TEXTO")
            {
                byte[] textBytes = new byte[4];
                bytesRead = await stream.ReadAsync(textBytes, 0, textBytes.Length);

                if (bytesRead == 0)
                {
                    Console.WriteLine("Cliente desconectado.");
                    break;
                }

                int textSize = BitConverter.ToInt32(textBytes, 0);

                byte[] textData = new byte[textSize];
                int bytesReadTotal = 0;
                int bytesReadThisTime;

                while (bytesReadTotal < textSize)
                {
                    bytesReadThisTime = await stream.ReadAsync(textData, bytesReadTotal, textSize - bytesReadTotal);

                    if (bytesReadThisTime == 0)
                    {
                        throw new Exception("Error al recibir datos de texto: se esperaba más datos pero se recibieron cero.");
                    }

                    bytesReadTotal += bytesReadThisTime;
                }

                string text = System.Text.Encoding.UTF8.GetString(textData);
                Console.WriteLine("Se cambio el idioma a: " + text);

            }
            else
            {
                Console.WriteLine("Encabezado desconocido: " + header);
            }
        }
    }
    
    /// <summary>
    /// The function `SendFileToServer` sends a file to a specified server URL using HTTP POST request
    /// with error handling.
    /// </summary>
    /// <param name="filePath">The `SendFileToServer` method you provided is an asynchronous method that
    /// sends a file to a server using HttpClient. It reads the file content asynchronously, creates a
    /// multipart form data content with the file content, and then sends a POST request to the
    /// specified server URL.</param>
    /// <returns>
    /// The method `SendFileToServer` is returning a `Task<string>`. The string being returned is the
    /// response body from the server if the file was successfully sent and the response status code is
    /// successful. If there is an error during the process, it will return null.
    /// </returns>
    static async Task<string> SendFileToServer(string filePath)
    {
        using (var httpClient = new HttpClient())
        {
            try
            {

                string serverUrl = "http://192.168.1.17:9999/v1/OpenAi";


                using (var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath)))
                {

                    using (var formData = new MultipartFormDataContent())
                    {
                        formData.Add(fileContent);


                        HttpResponseMessage response = await httpClient.PostAsync(serverUrl, formData);


                        if (response.IsSuccessStatusCode)
                        {
                            Console.WriteLine("Archivo enviado exitosamente al servidor HTTP.");

                            string responseBody = await response.Content.ReadAsStringAsync();
                            return responseBody;
                        }
                        else
                        {
                            Console.WriteLine($"Error al enviar el archivo al servidor HTTP. Código de estado: {response.StatusCode}");
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar el archivo al servidor HTTP: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// The function `SaveAudioLocally` saves audio data as a WAV file locally in C# asynchronously.
    /// </summary>
    /// <param name="fileData">fileData is a byte array containing the audio data that needs to be saved
    /// locally as a WAV file.</param>
    static async Task SaveAudioLocally(byte[] fileData)
    {
        try
        {

            string filePath = "guardar/audio_recibido.wav";


            await File.WriteAllBytesAsync(filePath, fileData);

            Console.WriteLine("Archivo de audio guardado localmente correctamente.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al guardar el archivo de audio localmente: {ex.Message}");
        }
    }

    /// <summary>
    /// The function `GeneratePollyAudio` generates audio from text using Amazon Polly and sends it to a
    /// client stream in C#.
    /// </summary>
    /// <param name="text">The `GeneratePollyAudio` method you provided is responsible for generating
    /// audio from text using Amazon Polly service and sending it to a client through a network
    /// stream.</param>
    /// <param name="NetworkStream">The `NetworkStream` parameter in the `GeneratePollyAudio` method
    /// represents a stream used for reading and writing data over a network connection. In this
    /// context, it is being used to send the generated audio file to a client over the network. The
    /// `clientStream` parameter is an instance of</param>
    static void GeneratePollyAudio(string text, NetworkStream clientStream)
    {
        try
        {

            text = Regex.Replace(text, @"[\p{P}\p{S}]", "");

            using (var pollyClient = new AmazonPollyClient(Claves.Credentials, RegionEndpoint.SAEast1))
            {
                var request = new SynthesizeSpeechRequest
                {
                    Text = text,
                    // VoiceId = VoiceId.Lucia, // voz en Español.
                    VoiceId = VoiceId.Joanna, // Voz en Ingles.
                    //VoiceId = VoiceId.Celine, // voz en frances
                    OutputFormat = OutputFormat.Pcm
                };

                var response = pollyClient.SynthesizeSpeechAsync(request).Result;

                using (var memoryStream = new MemoryStream())
                {
                    response.AudioStream.CopyTo(memoryStream);
                    memoryStream.Seek(0, SeekOrigin.Begin);

                    string tempDirectory = Path.GetTempPath();
                    string audioFileName = Path.Combine(tempDirectory, Guid.NewGuid().ToString() + ".wav");

                    using (var fileStream = new FileStream(audioFileName, FileMode.Create, FileAccess.Write))
                    {
                        using (var writer = new WaveFileWriter(fileStream, new WaveFormat(16000, 16, 1)))
                        {
                            var buffer = new byte[memoryStream.Length];
                            memoryStream.Read(buffer, 0, (int)memoryStream.Length);
                            writer.Write(buffer, 0, buffer.Length);
                        }
                    }

                    Console.WriteLine("Audio generado y guardado temporalmente en: " + audioFileName);

                    byte[] fileData = File.ReadAllBytes(audioFileName);
                    byte[] fileSize = BitConverter.GetBytes(fileData.Length);
                    clientStream.Write(fileSize, 0, fileSize.Length);
                    clientStream.Write(fileData, 0, fileData.Length);
                    Console.WriteLine("Audio enviado al cliente.");

                    File.Delete(audioFileName);
                    Console.WriteLine("Archivo temporal eliminado: " + audioFileName);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error al generar y enviar el audio: " + ex.Message);
        }
    }
}