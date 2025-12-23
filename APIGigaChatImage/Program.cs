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

        static async void Main(string[] args)
        {
            try
            {
                string token = await GetToken(ClientId, AuthorizationKey);

                if (!string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("Токен получен успешно!");

                    // Пример использования метода генерации изображения
                    string prompt = "Красивый закат над горами с озером в лесу";
                    byte[] imageBytes = await GenerateImage(token, prompt);

                    if (imageBytes != null && imageBytes.Length > 0)
                    {
                        // Сохранение изображения в файл
                        string fileName = $"generated_image_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                        SaveImageToFile(imageBytes, fileName);
                        Console.WriteLine($"Изображение сохранено в файл: {fileName}, Размер: {imageBytes.Length} байт");
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

            Console.WriteLine("Нажмите любую клавишу для выхода...");
            Console.ReadKey();
        }

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

                        // Десериализация с использованием Newtonsoft.Json
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

        public static async Task<byte[]> GenerateImage(string token, string prompt,
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
                        // Установка заголовков
                        client.DefaultRequestHeaders.Add("Accept", "application/json");
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                        // Создание тела запроса
                        var requestBody = new
                        {
                            model = model,
                            prompt = prompt,
                            n = n,
                            size = size,
                            response_format = responseFormat
                        };

                        // Сериализация с использованием Newtonsoft.Json
                        string jsonContent = JsonConvert.SerializeObject(requestBody);
                        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                        Console.WriteLine($"Отправка запроса на генерацию изображения...");
                        Console.WriteLine($"Промпт: {prompt}");

                        // Отправка запроса
                        HttpResponseMessage response = await client.PostAsync(url, content);

                        if (response.IsSuccessStatusCode)
                        {
                            string responseContent = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"Получен ответ от API: {responseContent}");

                            if (responseFormat == "url")
                            {
                                // Парсим JSON ответ для получения URL
                                JObject jsonResponse = JObject.Parse(responseContent);

                                // Проверяем наличие свойства data
                                if (jsonResponse["data"] is JArray dataArray && dataArray.Count > 0)
                                {
                                    JObject firstImage = (JObject)dataArray[0];
                                    string imageUrl = firstImage["url"]?.ToString();

                                    if (!string.IsNullOrEmpty(imageUrl))
                                    {
                                        Console.WriteLine($"URL изображения: {imageUrl}");

                                        // Скачиваем изображение по URL
                                        return await DownloadImageFromUrl(imageUrl);
                                    }
                                }
                            }
                            else if (responseFormat == "b64_json")
                            {
                                // Если изображение возвращается в base64
                                JObject jsonResponse = JObject.Parse(responseContent);

                                if (jsonResponse["data"] is JArray dataArray && dataArray.Count > 0)
                                {
                                    JObject firstImage = (JObject)dataArray[0];
                                    string base64Image = firstImage["b64_json"]?.ToString();

                                    if (!string.IsNullOrEmpty(base64Image))
                                    {
                                        return Convert.FromBase64String(base64Image);
                                    }
                                }
                            }
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

        private static void SaveImageToFile(byte[] imageBytes, string fileName)
        {
            try
            {
                File.WriteAllBytes(fileName, imageBytes);
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

    public class ImageGenerationResponse
    {
        public long created { get; set; }
        public List<ImageData> data { get; set; }
    }

    public class ImageData
    {
        public string url { get; set; }
        public string b64_json { get; set; }
    }
}