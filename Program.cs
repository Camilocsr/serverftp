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
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using FTPSERVER;

class Program
{
    static string transcribedText;
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

            Stopwatch stopwatch = Stopwatch.StartNew();

            await SaveAudioToS3(fileData);

            Console.WriteLine($"Tiempo total de SaveAudioToS3: {stopwatch.ElapsedMilliseconds / 1000.0} segundos");

            stopwatch.Restart();

            await EnvioServerHttp(transcribedText, stream);

            Console.WriteLine($"Tiempo total de EnvioServerHttp: {stopwatch.ElapsedMilliseconds / 1000.0} segundos");

            Console.WriteLine(transcribedText);
        }
    }

    static async Task SaveAudioToS3(byte[] audioData)
    {
        try
        {
            string bucketName = "unitrascripcionesingles";
            string keyName = "carpeta/audio.wav";

            using (var client = new AmazonS3Client(Claves.Credentials, Amazon.RegionEndpoint.SAEast1))
            {
                using (var memoryStream = new MemoryStream(audioData))
                {
                    var fileTransferUtility = new TransferUtility(client);
                    await fileTransferUtility.UploadAsync(memoryStream, bucketName, keyName);
                }
            }

            Console.WriteLine("Archivo de audio guardado en Amazon S3.");
            await TranscribeAudio(bucketName, keyName);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error al guardar el archivo de audio en S3: " + ex.Message);
        }
    }

    static async Task TranscribeAudio(string bucketName, string keyName)
    {
        try
        {
            string jobName = "AudioTranscriptionJob_" + DateTime.Now.ToString("yyyyMMddHHmmss");


            var transcribeClient = new AmazonTranscribeServiceClient(Claves.Credentials, RegionEndpoint.SAEast1);

            var request = new StartTranscriptionJobRequest
            {
                TranscriptionJobName = jobName,
                LanguageCode = Amazon.TranscribeService.LanguageCode.EsES,
                Media = new Media
                {
                    MediaFileUri = "https://s3.amazonaws.com/" + bucketName + "/" + keyName
                }
            };

            var response = await transcribeClient.StartTranscriptionJobAsync(request);

            Stopwatch stopwatch = Stopwatch.StartNew();

            while (true)
            {
                var jobResponse = await transcribeClient.GetTranscriptionJobAsync(new GetTranscriptionJobRequest
                {
                    TranscriptionJobName = response.TranscriptionJob.TranscriptionJobName
                });

                if (jobResponse.TranscriptionJob.TranscriptionJobStatus == TranscriptionJobStatus.COMPLETED)
                {
                    var transcriptUri = jobResponse.TranscriptionJob.Transcript.TranscriptFileUri;
                    var transcriptJson = await new WebClient().DownloadStringTaskAsync(transcriptUri);
                    var transcriptResult = JsonConvert.DeserializeObject<Transcript>(transcriptJson);

                    var transcript = transcriptResult.results.transcripts[0].transcript;
                    transcribedText = transcript;
                    Console.WriteLine("Texto transcrito:");
                    Console.WriteLine(transcript);

                    break;
                }
                else if (jobResponse.TranscriptionJob.TranscriptionJobStatus == TranscriptionJobStatus.FAILED)
                {
                    throw new Exception("La transcripción del audio ha fallado.");
                }
            }

            Console.WriteLine($"Tiempo total de TranscribeAudio: {stopwatch.ElapsedMilliseconds / 1000.0} segundos");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error al transcribir el audio: " + ex.Message);
        }
    }

    static async Task EnvioServerHttp(string message, NetworkStream stream)
    {
        try
        {
            using (var httpClient = new HttpClient())
            {
                var requestBody = new { texto = message };
                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("http://192.168.1.17:9999/v1/OpenAi", content);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    dynamic jsonResponse = JsonConvert.DeserializeObject(responseBody);
                    string textoModificado = jsonResponse.textoModificado;
                    Console.WriteLine("Texto modificado recibido del servidor HTTP: " + textoModificado);
                    Console.WriteLine("Mensaje enviado exitosamente al servidor HTTP.");

                    GeneratePollyAudio(textoModificado, stream);
                }
                else
                {
                    Console.WriteLine($"Error al enviar el mensaje al servidor HTTP. Código de estado: {response.StatusCode}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al enviar el mensaje al servidor HTTP: {ex.Message}");
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
                    // VoiceId = VoiceId.Emma,
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

                    DeleteFilesInS3Folder("unitrascripcionesingles","carpeta");


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


    static async Task DeleteFilesInS3Folder(string bucketName, string folderName)
    {
        try
        {
            using (var client = new AmazonS3Client(Claves.Credentials, Amazon.RegionEndpoint.SAEast1))
            {
                var listObjectsRequest = new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    Prefix = folderName + "/"
                };

                var listObjectsResponse = await client.ListObjectsV2Async(listObjectsRequest);

                foreach (var obj in listObjectsResponse.S3Objects)
                {
                    var deleteObjectRequest = new DeleteObjectRequest
                    {
                        BucketName = bucketName,
                        Key = obj.Key
                    };

                    await client.DeleteObjectAsync(deleteObjectRequest);
                    Console.WriteLine($"Archivo eliminado: {obj.Key}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error al borrar archivos de S3: " + ex.Message);
        }
    }

    class Transcript
    {
        public TranscriptResults results { get; set; }
    }

    class TranscriptResults
    {
        public List<TranscriptItem> transcripts { get; set; }
    }

    class TranscriptItem
    {
        public string transcript { get; set; }
    }
}