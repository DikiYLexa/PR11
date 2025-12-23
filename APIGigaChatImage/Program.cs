using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace APIGigaChatImage
{
    public class Program
    {
        public static string ClientId = "019b45ee-916a-7606-93bf-4637aa43e929";
        public static string AuthorizationKey = "MDE5YjQ1ZWUtOTE2YS03NjA2LTkzYmYtNDYzN2FhNDNlOTI5OjQ3NGM3NGU1LTRiOWMtNDM4ZC05MTI2LWUwN2Y3YWExM2RlYQ==";

        // Исправленная точка входа
        static async Task Main(string[] args)
        {
            try
            {
                string token = await GetToken(ClientId, AuthorizationKey);

                if (!string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("Токен получен успешно!");

                    // Пример 1: Генерация нового изображения
                    string prompt = "Красивый закат над горами с озером в лесу";
                    var generationResult = await GenerateImageWithId(token, prompt);

                    if (generationResult != null && !string.IsNullOrEmpty(generationResult.ImageId))
                    {
                        Console.WriteLine($"Изображение сгенерировано. ID: {generationResult.ImageId}");

                        // Сохранение первого сгенерированного изображения
                        if (generationResult.ImageBytes != null && generationResult.ImageBytes.Length > 0)
                        {
                            string fileName = $"generated_image_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                            SaveImageToFile(generationResult.ImageBytes, fileName);
                            Console.WriteLine($"Изображение сохранено в файл: {fileName}");
                        }

                        // Пример 2: Скачивание изображения по ID
                        Console.WriteLine("\nСкачивание изображения по ID...");
                        byte[] downloadedImage = await DownloadImageById(token, generationResult.ImageId);

                        if (downloadedImage != null && downloadedImage.Length > 0)
                        {
                            string downloadedFileName = $"downloaded_by_id_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                            SaveImageToFile(downloadedImage, downloadedFileName);
                            Console.WriteLine($"Изображение скачано по ID и сохранено в файл: {downloadedFileName}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Не удалось получить токен");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }

        // Если нужна синхронная точка входа для старых версий .NET Framework
        /*
        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }
        
        static async Task MainAsync(string[] args)
        {
            // Весь асинхронный код здесь
        }
        */

        public static async Task<string> GetToken(string rqUID, string bearer)
        {
            string returnToken = null;
            string url = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";

            using (HttpClientHandler handler = new HttpClientHandler())
            {
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true;

                using (HttpClient client = new HttpClient(handler))
                {
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
                        Console.WriteLine($"Ответ от сервера: {responseContent}");

                        var token = JsonConvert.DeserializeObject<ResponseToken>(responseContent);
                        returnToken = token?.access_token;
                    }
                    else
                    {
                        Console.WriteLine($"Ошибка получения токена: {response.StatusCode}");
                        string errorContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Детали ошибки: {errorContent}");
                    }
                }
            }

            return returnToken;
        }

        public static async Task<ImageGenerationResult> GenerateImageWithId(string token, string prompt,
            string model = "GigaChat", int n = 1, string size = "1024x1024",
            string responseFormat = "url")
        {
            try
            {
                string url = "https://gigachat.devices.sberbank.ru/api/v1/images/generations";

                using (HttpClientHandler handler = new HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true;

                    using (HttpClient client = new HttpClient(handler))
                    {
                        client.DefaultRequestHeaders.Add("Accept", "application/json");
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                        var requestBody = new
                        {
                            model = model,
                            prompt = prompt,
                            n = n,
                            size = size,
                            response_format = responseFormat
                        };

                        string jsonContent = JsonConvert.SerializeObject(requestBody);
                        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                        Console.WriteLine($"Отправка запроса на генерацию изображения...");
                        Console.WriteLine($"Промпт: {prompt}");

                        HttpResponseMessage response = await client.PostAsync(url, content);

                        if (response.IsSuccessStatusCode)
                        {
                            string responseContent = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"Получен ответ от API: {responseContent}");

                            var result = new ImageGenerationResult();

                            if (responseFormat == "url")
                            {
                                JObject jsonResponse = JObject.Parse(responseContent);

                                // Получаем ID изображения
                                if (jsonResponse["id"] != null)
                                {
                                    result.ImageId = jsonResponse["id"].ToString();
                                }
                                else
                                {
                                    // Если нет ID в ответе, генерируем свой
                                    result.ImageId = $"img_{Guid.NewGuid().ToString("N").Substring(0, 16)}";
                                }

                                // Получаем URL изображения
                                if (jsonResponse["data"] is JArray dataArray && dataArray.Count > 0)
                                {
                                    JObject firstImage = (JObject)dataArray[0];
                                    string imageUrl = firstImage["url"]?.ToString();

                                    if (!string.IsNullOrEmpty(imageUrl))
                                    {
                                        Console.WriteLine($"URL изображения: {imageUrl}");
                                        result.ImageBytes = await DownloadImageFromUrl(imageUrl);
                                        result.ImageUrl = imageUrl;
                                    }
                                }
                            }
                            else if (responseFormat == "b64_json")
                            {
                                JObject jsonResponse = JObject.Parse(responseContent);

                                if (jsonResponse["id"] != null)
                                {
                                    result.ImageId = jsonResponse["id"].ToString();
                                }
                                else
                                {
                                    result.ImageId = $"img_{Guid.NewGuid().ToString("N").Substring(0, 16)}";
                                }

                                if (jsonResponse["data"] is JArray dataArray && dataArray.Count > 0)
                                {
                                    JObject firstImage = (JObject)dataArray[0];
                                    string base64Image = firstImage["b64_json"]?.ToString();

                                    if (!string.IsNullOrEmpty(base64Image))
                                    {
                                        result.ImageBytes = Convert.FromBase64String(base64Image);
                                    }
                                }
                            }

                            return result;
                        }
                        else
                        {
                            Console.WriteLine($"Ошибка генерации изображения: {response.StatusCode}");
                            string errorContent = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"Детали ошибки: {errorContent}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Исключение при генерации изображения: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }

            return null;
        }

        // Основной метод для скачивания изображения по ID
        public static async Task<byte[]> DownloadImageById(string token, string imageId)
        {
            try
            {
                Console.WriteLine($"Скачивание изображения по ID: {imageId}");

                // Предполагаемый endpoint для получения изображения по ID
                string url = $"https://gigachat.devices.sberbank.ru/api/v1/images/{imageId}";

                using (HttpClientHandler handler = new HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true;

                    using (HttpClient client = new HttpClient(handler))
                    {
                        client.DefaultRequestHeaders.Add("Accept", "application/json");
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                        // Получение информации об изображении
                        HttpResponseMessage response = await client.GetAsync(url);

                        if (response.IsSuccessStatusCode)
                        {
                            string responseContent = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"Получена информация об изображении: {responseContent}");

                            // Парсим ответ, чтобы получить URL изображения
                            JObject jsonResponse = JObject.Parse(responseContent);

                            // Проверяем различные возможные пути к URL изображения
                            string imageUrl = null;

                            if (jsonResponse["url"] != null)
                            {
                                imageUrl = jsonResponse["url"].ToString();
                            }
                            else if (jsonResponse["data"] != null && jsonResponse["data"]["url"] != null)
                            {
                                imageUrl = jsonResponse["data"]["url"].ToString();
                            }
                            else if (jsonResponse["image_url"] != null)
                            {
                                imageUrl = jsonResponse["image_url"].ToString();
                            }

                            if (!string.IsNullOrEmpty(imageUrl))
                            {
                                Console.WriteLine($"Найден URL изображения: {imageUrl}");
                                return await DownloadImageFromUrl(imageUrl);
                            }
                            else
                            {
                                Console.WriteLine("URL изображения не найден в ответе.");

                                // Проверяем, есть ли base64 изображение
                                if (jsonResponse["b64_json"] != null)
                                {
                                    string base64Image = jsonResponse["b64_json"].ToString();
                                    return Convert.FromBase64String(base64Image);
                                }
                                else if (jsonResponse["data"] != null && jsonResponse["data"]["b64_json"] != null)
                                {
                                    string base64Image = jsonResponse["data"]["b64_json"].ToString();
                                    return Convert.FromBase64String(base64Image);
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Ошибка получения информации об изображении: {response.StatusCode}");

                            // Пробуем альтернативный подход
                            string alternativeUrl = $"https://gigachat.devices.sberbank.ru/api/v1/images/{imageId}/download";
                            HttpResponseMessage altResponse = await client.GetAsync(alternativeUrl);

                            if (altResponse.IsSuccessStatusCode)
                            {
                                Console.WriteLine("Изображение получено через альтернативный endpoint");
                                return await altResponse.Content.ReadAsByteArrayAsync();
                            }
                            else
                            {
                                Console.WriteLine($"Альтернативный endpoint также не сработал: {altResponse.StatusCode}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при скачивании изображения по ID: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }

            return null;
        }

        // Метод для скачивания изображения по URL
        private static async Task<byte[]> DownloadImageFromUrl(string url)
        {
            using (HttpClientHandler handler = new HttpClientHandler())
            {
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true;

                using (HttpClient client = new HttpClient(handler))
                {
                    try
                    {
                        Console.WriteLine($"Скачивание изображения с URL: {url}");
                        return await client.GetByteArrayAsync(url);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка загрузки изображения: {ex.Message}");
                        return null;
                    }
                }
            }
        }

        // Метод для скачивания изображения по ID с сохранением в файл
        public static async Task<bool> DownloadAndSaveImageById(string token, string imageId, string fileName)
        {
            try
            {
                byte[] imageBytes = await DownloadImageById(token, imageId);

                if (imageBytes != null && imageBytes.Length > 0)
                {
                    SaveImageToFile(imageBytes, fileName);
                    Console.WriteLine($"Изображение с ID {imageId} сохранено в файл: {fileName}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при сохранении изображения: {ex.Message}");
            }

            return false;
        }

        private static void SaveImageToFile(byte[] imageBytes, string fileName)
        {
            try
            {
                File.WriteAllBytes(fileName, imageBytes);
                Console.WriteLine($"Файл сохранен: {fileName}, размер: {imageBytes.Length} байт");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения файла: {ex.Message}");
            }
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
}