using Octokit;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AnlaxRevitUpdate
{
    public class GitHubBaseDownload
    {
        private readonly HttpClient _client;

        public GitHubBaseDownload(string assemlyPath, bool debug, string token, string ownerName, string repposotoryName, string folderName1)
        {
            AssemlyPath = assemlyPath;
            Debug = debug;
            Token = token;
            OwnerName = ownerName;
            RepposotoryName = repposotoryName;
            FolderName = folderName1;
            _client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30) // Таймаут 30 секунд
            };
            _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AppName", "1.0"));
        }

        public string AssemlyPath { get; }
        public bool Debug { get; }
        public string Token { get; }
        public string OwnerName { get; }
        public string RepposotoryName { get; }
        public string FolderName { get; }

        public DateTime DateUpdateLocalFile
        {
            get
            {
                if (!File.Exists(AssemlyPath))
                {
                    return DateTime.MinValue;
                }

                return File.GetLastWriteTime(AssemlyPath);
            }
        }
        public string RevitVersion
        {
            get
            {
                DirectoryInfo directory = Directory.GetParent(Directory.GetParent(AssemlyPath).FullName);
                return directory.Name; // Здесь мы получаем "2022" и т.д.
            }
        }
        public string ReleaseTag
        {
            get
            {
                string releaseTag = $"Release R{RevitVersion.Substring(RevitVersion.Length - 2)}";
                if (Debug)
                {
                    releaseTag = $"Debug R{RevitVersion.Substring(RevitVersion.Length - 2)}";
                }
                return releaseTag;
            }
        }
        public DateTime DateRelease
        {
            get
            {
                if (Release != null)
                {
                    return Release.PublishedAt?.DateTime ?? DateTime.MinValue;
                }
                return DateTime.MinValue;
            }
        }

        public Release Release
        {
            get
            {
                // Получаем информацию о релизе с помощью Octokit
                var client = new GitHubClient(new Octokit.ProductHeaderValue(RepposotoryName + "-Updater"));
                client.Credentials = new Credentials(Token);

                var releases = client.Repository.Release.GetAll(OwnerName, RepposotoryName).Result;

                var release = releases.FirstOrDefault(r => r.Name == ReleaseTag);

                return release;
            }
        }
        public ReleaseAsset ReleaseAsset
        {
            get
            {

                return Release.Assets.FirstOrDefault(a => a.Name == FolderName + ".zip");
            }
        }
        public string TempPathToDownload
        {
            get
            {
                return Path.Combine(Path.GetTempPath(), FolderName + ".zip");
            }
        }
        private void DeleteOldAndUpdate(bool deleteAllFiles = false)
        {
            string extractPath = Path.GetDirectoryName(AssemlyPath);

            // Удаляем старые файлы, если указано
            if (deleteAllFiles)
            {
                DirectoryInfo di = new DirectoryInfo(extractPath);
                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in di.GetDirectories())
                {
                    dir.Delete(true);
                }
            }

            // Распаковываем с перезаписью существующих файлов
            using (ZipArchive archive = ZipFile.OpenRead(TempPathToDownload))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destinationPath = Path.Combine(extractPath, entry.FullName);

                    // Создаем директории, если их нет
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destinationPath);
                    }
                    else
                    {
                        // Удаляем файл, если он существует
                        if (File.Exists(destinationPath))
                        {
                            try
                            {
                                File.Delete(destinationPath);
                            }
                            catch 
                            {

                            }
                            
                        }
                        try
                        {
                            // Распаковываем файл
                            entry.ExtractToFile(destinationPath, true);
                        }
                        catch { }

                    }
                }
            }
            // Удаляем временный файл архива
            File.Delete(TempPathToDownload);
        }
        public string HotReloadPlugin(bool checkDate)
        {
            string result = string.Empty;
            if (checkDate)
            {
                if (DateRelease > DateUpdateLocalFile)
                {
                    result = DownloadReleaseAsset();
                    if (result == "Загрузка прошла успешно")
                    {
                        DeleteOldAndUpdate();
                    }
                    else
                    {
                        return result;
                    }
                }
                else
                {
                    return "Загружена актуальная версия плагина";
                }
            }
            result = DownloadReleaseAsset();
            if (result == "Загрузка прошла успешно")
            {
                DeleteOldAndUpdate();
            }
            return result;
        }
        public string DownloadReleaseAsset()
        {
            try
            {
                // Добавляем токен авторизации
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", Token);

                // Важно: Устанавливаем Accept для работы с API GitHub
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

                // Выполняем запрос синхронно
                var response = _client.GetAsync(ReleaseAsset.Url).Result;
                response.EnsureSuccessStatusCode();

                // Записываем данные в файл
                using (var stream = response.Content.ReadAsStreamAsync().Result)
                using (var fileStream = new FileStream(TempPathToDownload, System.IO.FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.CopyTo(fileStream);
                }
                return "Загрузка прошла успешно";
            }
            catch (HttpRequestException ex)
            {
                return ($"Ошибка HTTP-запроса: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                return ("Загрузка прервана из-за превышения времени ожидания.");
            }
            catch (Exception ex)
            {
                return ($"Ошибка: {ex.Message}");
            }
        }
    }
}
