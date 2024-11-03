﻿using Octokit;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;

namespace AnlaxRevitUpdate
{
    public class GitHubBaseDownload
    {
        private readonly HttpClient _client;

        public GitHubBaseDownload(string assemlyPath, bool debug, string ownerName, string repposotoryName, string folderName1)
        {
            AssemlyPath = assemlyPath;
            Debug = debug;
            OwnerName = ownerName;
            RepposotoryName = repposotoryName;
            FolderName = folderName1;
            _client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30) // Таймаут 30 секунд
            };
            _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AppName", "1.0"));
        }
        public string CurrentVersion
        {
            get
            {
                if (!File.Exists(AssemlyPath))
                    return "1.0.0";

                var versionInfo = FileVersionInfo.GetVersionInfo(AssemlyPath);
                string version = versionInfo.FileVersion;

                // Преобразуем версию в формат 2022.1.1.2
                string formattedVersion = $"{versionInfo.ProductMajorPart}.{versionInfo.ProductMinorPart}.{versionInfo.ProductBuildPart}.{versionInfo.ProductPrivatePart}";

                // Убираем первые четыре знака и точку
                string result = formattedVersion.Substring(5);
                return result;
            }
        }

        public string ReleaseVersion
        {
            get
            {
                if (Release != null && !string.IsNullOrEmpty(Release.TagName))
                {
                    string tagRelease = Release.TagName;
                    // Убираем слова "Release" и "Debug" из TagName
                    string cleanTag = tagRelease.Replace("Debug", "").Replace("Release", "").Trim();
                    // Возвращаем очищенный TagName
                    return cleanTag;
                }
                return "1.0.0";
            }
        }
        public bool IsReleaseVersionGreater()
        {
            // Пробуем создать объекты Version из строк версий
            if (Version.TryParse(CurrentVersion, out var currentVersion) &&
                Version.TryParse(ReleaseVersion, out var releaseVersion))
            {
                // Сравниваем версии: возвращает true, если ReleaseVersion > CurrentVersion
                return releaseVersion > currentVersion;
            }
            return false;
        }

        public string AssemlyPath { get; }
        public bool Debug { get; }
        public string OwnerName { get; }
        public string RepposotoryName { get; }
        public string FolderName { get; }

        public DateTime DateUpdateLocalFile => File.Exists(AssemlyPath) ? File.GetLastWriteTime(AssemlyPath) : DateTime.MinValue;

        public string RevitVersion => Directory.GetParent(Directory.GetParent(AssemlyPath).FullName).Name.Substring(RevitVersion.Length - 2);

        public string ReleaseTag => Debug ? "Debug" : "Release";

        public DateTime DateRelease => Release?.PublishedAt?.DateTime ?? DateTime.MinValue;

        public Release Release
        {
            get
            {
                var client = new GitHubClient(new Octokit.ProductHeaderValue(RepposotoryName + "-Updater"));
                var releases = client.Repository.Release.GetAll(OwnerName, RepposotoryName).Result;
                return releases.FirstOrDefault(r => r.Name == ReleaseTag);
            }
        }

        public ReleaseAsset ReleaseAsset => Release?.Assets.FirstOrDefault(a => a.Name.Contains("R" + RevitVersion));

        public string TempPathToDownload => Path.Combine(Path.GetTempPath(), FolderName + ".zip");

        private void DeleteOldAndUpdate(bool deleteAllFiles = false)
        {
            string extractPath = Path.GetDirectoryName(AssemlyPath);

            if (deleteAllFiles)
            {
                var di = new DirectoryInfo(extractPath);
                foreach (var file in di.GetFiles()) file.Delete();
                foreach (var dir in di.GetDirectories()) dir.Delete(true);
            }

            using (ZipArchive archive = ZipFile.OpenRead(TempPathToDownload))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destinationPath = Path.Combine(extractPath, entry.FullName);

                    if (string.IsNullOrEmpty(entry.Name))
                        Directory.CreateDirectory(destinationPath);
                    else
                    {
                        if (File.Exists(destinationPath)) File.Delete(destinationPath);
                        entry.ExtractToFile(destinationPath, true);
                    }
                }
            }

            File.Delete(TempPathToDownload);
        }

        public string HotReloadPlugin(bool checkDate)
        {
            string result = string.Empty;
            if (checkDate && IsReleaseVersionGreater())
            {
                result = DownloadReleaseAsset();
                if (result == "Загрузка прошла успешно") DeleteOldAndUpdate();
                else return result;
            }
            else return "Загружена актуальная версия плагина";

            result = DownloadReleaseAsset();
            if (result == "Загрузка прошла успешно") DeleteOldAndUpdate();
            return result;
        }

        public string DownloadReleaseAsset()
        {
            try
            {
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
                var response = _client.GetAsync(ReleaseAsset.Url).Result;
                response.EnsureSuccessStatusCode();

                using (var stream = response.Content.ReadAsStreamAsync().Result)
                using (var fileStream = new FileStream(TempPathToDownload, System.IO.FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.CopyTo(fileStream);
                }
                return "Загрузка прошла успешно";
            }
            catch (HttpRequestException ex)
            {
                return $"Ошибка HTTP-запроса: {ex.Message}";
            }
            catch (TaskCanceledException)
            {
                return "Загрузка прервана из-за превышения времени ожидания.";
            }
            catch (Exception ex)
            {
                return $"Ошибка: {ex.Message}";
            }
        }
    }
}
