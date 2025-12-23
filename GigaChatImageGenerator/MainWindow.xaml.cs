using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Net;
using System.Windows.Controls;

namespace ImageGenerator
{
    public partial class MainWindow : Window
    {
        private string _currentImagePath;
        private byte[] _currentImageBytes;
        private string _token;

        // Данные API
        private const string ClientId = "019b45ee-916a-7606-93bf-4637aa43e929";
        private const string AuthorizationKey = "MDE5YjQ1ZWUtOTE2YS03NjA2LTkzYmYtNDYzN2FhNDNlOTI5OjQ3NGM3NGU1LTRiOWMtNDM4ZC05MTI2LWUwN2Y3YWExM2RlYQ==";

        public MainWindow()
        {
            InitializeComponent();
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            string prompt = PromptTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(prompt))
            {
                MessageBox.Show("Введите что нарисовать");
                return;
            }

            // Получаем параметры
            string style = ((ComboBoxItem)StyleComboBox.SelectedItem).Content.ToString();
            string palette = PaletteBright.IsChecked == true ? "яркие цвета" : "пастельные цвета";
            string size = RatioSquare.IsChecked == true ? "1024x1024" : "1024x576";

            // Формируем промпт
            string enhancedPrompt = $"{prompt} в стиле {style.ToLower()}, {palette}";

            GenerateButton.IsEnabled = false;
            GenerateButton.Content = "Создаём...";

            try
            {
                // Токен
                if (string.IsNullOrEmpty(_token))
                {
                    _token = await GetToken();
                    if (string.IsNullOrEmpty(_token))
                    {
                        MessageBox.Show("Ошибка получения токена");
                        return;
                    }
                }

                // Генерация
                var result = await GenerateImage(_token, enhancedPrompt, size);

                if (result != null && result.ImageBytes != null)
                {
                    _currentImageBytes = result.ImageBytes;

                    // Автоматически сохраняем в папку Images
                    string fileName = $"image_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Images");

                    if (!Directory.Exists(folderPath))
                        Directory.CreateDirectory(folderPath);

                    _currentImagePath = Path.Combine(folderPath, fileName);
                    File.WriteAllBytes(_currentImagePath, _currentImageBytes);

                    // Показываем
                    ShowImage(_currentImageBytes);

                    // Включаем кнопки
                    SetWallpaperButton.IsEnabled = true;
                    SaveButton.IsEnabled = true;

                    MessageBox.Show($"Изображение создано и сохранено!\n\nПапка: {folderPath}\nФайл: {fileName}",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Не получилось создать изображение");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
            finally
            {
                GenerateButton.IsEnabled = true;
                GenerateButton.Content = "Создать";
            }
        }

        private void ShowImage(byte[] imageBytes)
        {
            try
            {
                using (var stream = new MemoryStream(imageBytes))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    GeneratedImage.Source = bitmap;
                }
            }
            catch
            {
                MessageBox.Show("Не могу показать картинку");
            }
        }

        private void SetWallpaperButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentImagePath) && File.Exists(_currentImagePath))
            {
                try
                {
                    const int SPI_SETDESKWALLPAPER = 0x0014;
                    const int SPIF_UPDATEINIFILE = 0x01;
                    const int SPIF_SENDWININICHANGE = 0x02;

                    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
                    static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

                    SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, _currentImagePath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);

                    MessageBox.Show("Обои установлены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch
                {
                    MessageBox.Show("Не могу установить обои", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImageBytes != null)
            {
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "JPEG Image|*.jpg|PNG Image|*.png|Все файлы|*.*",
                    FileName = $"image_{DateTime.Now:yyyyMMdd_HHmmss}",
                    DefaultExt = ".jpg",
                    Title = "Сохранить изображение как..."
                };

                if (saveDialog.ShowDialog() == true)
                {
                    try
                    {
                        File.WriteAllBytes(saveDialog.FileName, _currentImageBytes);
                        MessageBox.Show($"Изображение сохранено:\n{saveDialog.FileName}",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка сохранения: {ex.Message}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async Task<string> GetToken()
        {
            try
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                using (var client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(30);

                    var request = new HttpRequestMessage(HttpMethod.Post, "https://ngw.devices.sberbank.ru:9443/api/v2/oauth");
                    request.Headers.Add("Accept", "application/json");
                    request.Headers.Add("RqUID", ClientId);
                    request.Headers.Add("Authorization", $"Bearer {AuthorizationKey}");

                    var formData = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("scope", "GIGACHAT_API_PERS")
                    };

                    request.Content = new FormUrlEncodedContent(formData);

                    var response = await client.SendAsync(request);
                    string responseText = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var token = JsonConvert.DeserializeObject<TokenResponse>(responseText);
                        return token?.access_token;
                    }
                    else
                    {
                        MessageBox.Show($"Ошибка получения токена: {response.StatusCode}\n{responseText}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null;
        }

        private async Task<ImageResult> GenerateImage(string token, string prompt, string size)
        {
            try
            {
                string chatUrl = "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";

                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                using (var client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromMinutes(3);
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                    var requestBody = new
                    {
                        model = "GigaChat",
                        messages = new[]
                        {
                            new { role = "user", content = $"Нарисуй {prompt}" }
                        },
                        function_call = "auto"
                    };

                    string jsonContent = JsonConvert.SerializeObject(requestBody);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(chatUrl, content);
                    string responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var result = new ImageResult();
                        JObject jsonResponse = JObject.Parse(responseContent);
                        string assistantReply = jsonResponse["choices"]?[0]?["message"]?["content"]?.ToString();

                        if (!string.IsNullOrEmpty(assistantReply))
                        {
                            var match = Regex.Match(assistantReply, @"src=""([^""]+)""");
                            if (match.Success)
                            {
                                string fileId = match.Groups[1].Value;
                                return await DownloadImage(token, fileId);
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show($"Ошибка генерации: {response.StatusCode}\n{responseContent}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при генерации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null;
        }

        private async Task<ImageResult> DownloadImage(string token, string fileId)
        {
            try
            {
                string downloadUrl = $"https://gigachat.devices.sberbank.ru/api/v1/files/{fileId}/content";

                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                using (var client = new HttpClient(handler))
                {
                    client.DefaultRequestHeaders.Add("Accept", "image/jpeg");
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                    HttpResponseMessage response = await client.GetAsync(downloadUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
                        return new ImageResult { ImageBytes = imageBytes };
                    }
                    else
                    {
                        MessageBox.Show($"Ошибка скачивания: {response.StatusCode}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при скачивании: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null;
        }
    }

    public class TokenResponse
    {
        public string access_token { get; set; }
    }

    public class ImageResult
    {
        public byte[] ImageBytes { get; set; }
    }
}