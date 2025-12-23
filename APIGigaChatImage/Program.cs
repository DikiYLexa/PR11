using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace APIGigaChatImage
{
    public class Program
    {
        public static string ClientId = "019b45ee-916a-7606-93bf-4637aa43e929";
        public static string AuthorizationKey = "MDE5YjQ1ZWUtOTE2YS03NjA2LTkzYmYtNDYzN2FhNDNlOTI5OjQ3NGM3NGU1LTRiOWMtNDM4ZC05MTI2LWUwN2Y3YWExM2RlYQ==";

        // Папка для сохранения изображений
        private static string imagesFolder = "GeneratedImages";

        static async Task Main(string[] args)
        {
            // Устанавливаем кодировку для корректного отображения символов
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("=== Программа генерации изображений GigaChat ===\n");

            try
            {
                // Получение токена
                Console.Write("Получение токена... ");
                string token = await GetToken(ClientId, AuthorizationKey);

                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("ОШИБКА: Не удалось получить токен");
                    return;
                }
                Console.WriteLine("УСПЕХ\n");

                // Ввод промпта
                Console.Write("Введите промпт для генерации изображения: ");
                string prompt = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(prompt))
                {
                    prompt = "Красивый закат над горами с озером в лесу";
                    Console.WriteLine($"Используется промпт по умолчанию: \"{prompt}\"");
                }

                // Генерация изображения
                Console.Write($"Генерация изображения \"{prompt}\"... ");
                var generationResult = await GenerateImageWithId(token, prompt);

                // Обработка результата
                if (generationResult != null && generationResult.ImageBytes != null && generationResult.ImageBytes.Length > 0)
                {
                    // Создаем папку если не существует
                    if (!Directory.Exists(imagesFolder))
                    {
                        Directory.CreateDirectory(imagesFolder);
                    }

                    // Сохраняем изображение
                    string fileName = $"image_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    string fullPath = Path.Combine(imagesFolder, fileName);

                    try
                    {
                        File.WriteAllBytes(fullPath, generationResult.ImageBytes);
                        Console.WriteLine("УСПЕХ");
                        Console.WriteLine("\n=== РЕЗУЛЬТАТ ===");
                        Console.WriteLine($"Изображение сохранено:");
                        Console.WriteLine($"Файл: {fileName}");
                        Console.WriteLine($"Папка: {Path.GetFullPath(imagesFolder)}");
                        Console.WriteLine($"Полный путь: {Path.GetFullPath(fullPath)}");
                        Console.WriteLine($"Размер: {generationResult.ImageBytes.Length:N0} байт");

                        // Опция открыть папку
                        Console.Write("\nОткрыть папку с изображением? (y/n): ");
                        var key = Console.ReadKey();
                        if (key.KeyChar == 'y' || key.KeyChar == 'Y')
                        {
                            System.Diagnostics.Process.Start("explorer.exe", Path.GetFullPath(imagesFolder));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ОШИБКА сохранения: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("ОШИБКА: Не удалось сгенерировать изображение");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ОШИБКА: {ex.Message}");
            }

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }

        public static async Task<string> GetToken(string rqUID, string bearer)
        {
            try
            {
                string url = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";

                using (HttpClientHandler handler = new HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true;

                    using (HttpClient client = new HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromSeconds(30);
                        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);

                        request.Headers.Add("Accept", "application/json");
                        request.Headers.Add("RqUID", rqUID);
                        request.Headers.Add("Authorization", $"Bearer {bearer}");

                        var data = new List<KeyValuePair<string, string>>
                        {
                            new KeyValuePair<string, string>("scope", "GIGACHAT_API_PERS")
                        };

                        request.Content = new FormUrlEncodedContent(data);

                        HttpResponseMessage response = await client.SendAsync(request);

                        if (response.IsSuccessStatusCode)
                        {
                            string responseContent = await response.Content.ReadAsStringAsync();
                            var token = JsonConvert.DeserializeObject<ResponseToken>(responseContent);
                            return token?.access_token;
                        }
                    }
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        public static async Task<ImageGenerationResult> GenerateImageWithId(string token, string prompt, string model = "GigaChat")
        {
            try
            {
                // Генерация через чатовый эндпоинт
                string chatUrl = "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";

                using (HttpClientHandler handler = new HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true;

                    using (HttpClient client = new HttpClient(handler))
                    {
                        // Увеличиваем таймаут для генерации
                        client.Timeout = TimeSpan.FromMinutes(3);

                        client.DefaultRequestHeaders.Add("Accept", "application/json");
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                        // Простой промпт
                        var requestBody = new
                        {
                            model = model,
                            messages = new[]
                            {
                                new { role = "user", content = $"Нарисуй {prompt}" }
                            },
                            function_call = "auto"
                        };

                        string jsonContent = JsonConvert.SerializeObject(requestBody);
                        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                        HttpResponseMessage response = await client.PostAsync(chatUrl, content);

                        if (response.IsSuccessStatusCode)
                        {
                            string responseContent = await response.Content.ReadAsStringAsync();
                            var result = new ImageGenerationResult();

                            try
                            {
                                JObject jsonResponse = JObject.Parse(responseContent);
                                string assistantReply = jsonResponse["choices"]?[0]?["message"]?["content"]?.ToString();

                                if (!string.IsNullOrEmpty(assistantReply))
                                {
                                    // Извлечение ID изображения
                                    var match = Regex.Match(assistantReply, @"src=""([^""]+)""");
                                    if (match.Success)
                                    {
                                        string fileId = match.Groups[1].Value;
                                        result.ImageId = fileId;

                                        // Скачивание изображения
                                        string downloadUrl = $"https://gigachat.devices.sberbank.ru/api/v1/files/{fileId}/content";

                                        using (HttpClient downloadClient = new HttpClient(handler))
                                        {
                                            downloadClient.Timeout = TimeSpan.FromSeconds(60);
                                            downloadClient.DefaultRequestHeaders.Add("Accept", "image/jpeg");
                                            downloadClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                                            HttpResponseMessage downloadResponse = await downloadClient.GetAsync(downloadUrl);

                                            if (downloadResponse.IsSuccessStatusCode)
                                            {
                                                result.ImageBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
                                                return result;
                                            }
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                return null;
                            }
                        }
                    }
                }
            }
            catch
            {
                return null;
            }

            return null;
        }
    }

    public class ResponseToken
    {
        public string access_token { get; set; }
        public long expires_at { get; set; }
    }

    public class ImageGenerationResult
    {
        public string ImageId { get; set; }
        public byte[] ImageBytes { get; set; }
        public string ImageUrl { get; set; }
    }

    public class WallpaperSetter
    {
        private const int SPI_SETDESKWALLPAPER = 0x0014;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDWININICHANGE = 0x02;

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int SystemParametersInfo(
            int uAction,
            int uParam,
            string lpvParam,
            int fuWinIni
        );

        public static void SetWallpaper(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    Console.WriteLine($"Файл не найден: {imagePath}");
                    return;
                }

                SystemParametersInfo(
                    SPI_SETDESKWALLPAPER,
                    0,
                    imagePath,
                    SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE
                );
                Console.WriteLine($"Обои установлены: {Path.GetFileName(imagePath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка установки обоев: {ex.Message}");
            }
        }
    }
}