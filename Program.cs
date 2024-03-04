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
using Amazon.TranscribeService;
using Amazon.TranscribeService.Model;
using NAudio.Wave;
using Newtonsoft.Json;
using Amazon.S3;
using Amazon.S3.Transfer;
using FTPSERVER;


class Program
{
    static string transcribedText;
    private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
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

    static async Task HandleClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream();

        while (true)
        {
            byte[] fileSizeBytes = new byte[4];
            int bytesRead = await stream.ReadAsync(fileSizeBytes, 0, fileSizeBytes.Length);

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

            await semaphore.WaitAsync(); // Espera el semáforo
            try
            {
                // Realiza el procesamiento del audio
                await SaveAudioLocally(fileData);
                transcribedText = await SendFileToServer("guardar/audio_recibido.wav");

                Stopwatch stopwatch = Stopwatch.StartNew();
                stopwatch.Restart();

                Console.WriteLine($"Tiempo total de EnvioServerHttp: {stopwatch.ElapsedMilliseconds / 1000.0} segundos");
                Console.WriteLine(transcribedText); // Mostrar el texto devuelto por la API de OpenAI Turbo
                GeneratePollyAudio(transcribedText, stream);
            }
            finally
            {
                semaphore.Release(); // Libera el semáforo después de completar el procesamiento
            }
        }
    }
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

    static void GeneratePollyAudio(string text, NetworkStream clientStream)
    {
        try
        {
            using (var pollyClient = new AmazonPollyClient(Claves.Credentials, RegionEndpoint.SAEast1))
            {
                var request = new SynthesizeSpeechRequest
                {
                    Text = text,
                    VoiceId = VoiceId.Lucia,
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