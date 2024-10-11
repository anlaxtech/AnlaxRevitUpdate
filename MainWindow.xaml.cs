using AnlaxPackage;
using Autodesk.Revit.UI;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AnlaxRevitUpdate
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string PluginAutoUpdateDirectory { get; set; }
        string PluginDirectory
        {
            get
            {
                var directoryInfo = new System.IO.DirectoryInfo(PluginAutoUpdateDirectory);
                var targetDirectory = directoryInfo.Parent;
                return targetDirectory.FullName;
            }
        }
        public string RevitVersion
        {
            get
            {
                var directoryInfo = new System.IO.DirectoryInfo(PluginAutoUpdateDirectory);

                // Поднимаемся на 3 уровня вверх
                var targetDirectory = directoryInfo.Parent.Parent.Parent;

                // Получаем название папки (в вашем случае это версия Revit)
                string revitVersion = targetDirectory.Name;
                return revitVersion;
            }
        }
        public bool IsDebug
        {
            get
            {
                if (PluginAutoUpdateDirectory.Contains("Anlax dev"))
                {
                    return true;
                }
                return false;
            }
        }
        public List<string> DllPaths = new List<string>();
        Dictionary<string, string> downloadResult = new Dictionary<string, string>();

        public MainWindow()
        {
            PluginAutoUpdateDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            InitializeComponent();
            Thread.Sleep(5000);
            bool runs = IsRevitRunning(RevitVersion);
            if (runs)
            {
                Thread.Sleep(5000);
            }
            runs = IsRevitRunning(RevitVersion);
            if (!runs)
            {
                try
                {
                    TextBlockMessage.Text = "Не закрывайте окно. Идет проверка обновления плагина Anlax";
                    Show();

                    // Устанавливаем максимальное значение прогрессбара
                    DllPaths = FindDllsWithApplicationStart();
                    ProgressBarDownload.Maximum = DllPaths.Count + 1;
                    int progress = 0;

                    foreach (string dll in DllPaths)
                    {
                        HotReload(dll);

                        // Обновляем прогресс
                        progress++;
                        Dispatcher.Invoke(() =>
                        {
                            ProgressBarDownload.Value = progress;
                            TextBlockDownload.Text = $"Обновление: {progress} из {DllPaths.Count + 1}";
                        });
                    }

                    ReloadMainPlug();

                    // После завершения ставим максимальное значение
                    Dispatcher.Invoke(() =>
                    {
                        ProgressBarDownload.Value = ProgressBarDownload.Maximum;
                        TextBlockDownload.Text = "Обновление завершено!";
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error", $"Ошибка в автообновлении: {ex.StackTrace}");
                }
            }
            else
            {
                TextBlockMessage.Text = "Для проверки обновлений необходимо закрыть все сессии ревита";
                Timer timer = new Timer(CloseWindowCallback, null, 3000, Timeout.Infinite);
            }
        }
        private void CloseWindowCallback(object state)
        {
            // Этот метод будет вызван после истечения 5 секунд
            Dispatcher.Invoke(() =>
            {
                // Закрыть окно
                Close();
            });
        }
        private string ReloadMainPlug()
        {
            string pathToBaseDll = System.IO.Path.Combine(PluginDirectory, "AnlaxBase.dll");
            string token = "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";
            string userName = "anlaxtech";
            string repposotoryName = "AnlaxTemplate";
            var directoryInfo = new System.IO.DirectoryInfo(PluginDirectory);
            string plugFolderName = directoryInfo.Parent.Name;
            GitHubDownloader gitHubDownloader = new GitHubDownloader(pathToBaseDll, IsDebug, token, userName, repposotoryName, plugFolderName);
            string status =gitHubDownloader.HotReloadPlugin(true);
            return status;
        }
        private string HotReload(string path)
        {
            byte[] assemblyBytes = File.ReadAllBytes(path);
            Assembly assembly = Assembly.Load(assemblyBytes);
            // Ищем класс "ApplicationStart"
            RevitRibbonPanelCustom revitRibbonPanelCustom = new RevitRibbonPanelCustom("похер", "пофигу", path, new List<PushButtonData>());
            Type typeStart = assembly.GetTypes()
.Where(t => t.GetInterfaces().Any(i => i == typeof(IApplicationStartAnlax)))
.FirstOrDefault();

            if (typeStart != null)
            {
                object instance = Activator.CreateInstance(typeStart);
                MethodInfo onStartupMethod = typeStart.GetMethod("DownloadPluginUpdate");
                var allMethods = typeStart.GetMethods();
                if (onStartupMethod != null)
                {
                    string message = (string)onStartupMethod.Invoke(instance, new object[] { revitRibbonPanelCustom, IsDebug });
                    return message;
                }
            }
            return "Ошибка обновления";
        }


        public static bool IsRevitRunning(string versionNumber)
        {
            // Получаем все процессы с именем "Revit" (может потребоваться уточнение в зависимости от версии)
            Process[] processes = Process.GetProcessesByName("Revit");
            if (processes.Length > 0)
            {
                if (processes.Any(it => it.MainWindowTitle.Contains(versionNumber)))
                {
                    return true;
                }
            }
            return false;
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ButtonAnik_Click(object sender, RoutedEventArgs e)
        {
            string anik = "Идет Бог по Раю\n\n" +
              "Видит, сады горят\n" +
              "На грушевый пофигу\n" +
              "А яблочный\n" +
              "Спас";
            MessageBox.Show(anik);
        }


        private void Button_Click_Cancel(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ButtonLog_Click(object sender, RoutedEventArgs e)
        {
            List<string> wrongFiles = new List<string>();
            foreach (string keyFile in downloadResult.Keys)
            {
                string Value = downloadResult[keyFile];

                if (Value == "Не удалось перезаписать" || Value == "Не удалось перезаписать")
                {
                    wrongFiles.Add(Value);
                }
            }
            if (wrongFiles.Count > 0)
            {
                MessageBox.Show("Ошибки в файлах:" + string.Join(", ", wrongFiles));
            }
            else
            {
                MessageBox.Show("Все файлы обновлены без ошибок");
            }
        }
        public List<string> FindDllsWithApplicationStart()
        {
            List<string> result = new List<string>();

            // Рекурсивно ищем все файлы с расширением .dll
            var dllFiles = Directory.GetFiles(PluginDirectory, "*.dll", SearchOption.AllDirectories);

            foreach (var dll in dllFiles)
            {
                try
                {
                    // Читаем DLL как байтовый массив
                    var assemblyBytes = File.ReadAllBytes(dll);

                    // Загружаем сборку из байтов
                    var assembly = Assembly.Load(assemblyBytes);

                    // Ищем класс "ApplicationStart"
                    Type typeStart = assembly.GetTypes()
    .Where(t => t.GetInterfaces().Any(i => i == typeof(IApplicationStartAnlax)))
    .FirstOrDefault();

                    if (typeStart != null)
                    {
                        // Ищем метод "GetRevitRibbonPanelCustom"
                        var method = typeStart.GetMethod("GetRevitRibbonPanelCustom");

                        if (method != null)
                        {
                            result.Add(dll);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Логируем ошибки
                    Console.WriteLine($"Ошибка при обработке {dll}: {ex.Message}");
                }
            }

            return result;
        }
        private void ButtonRestart_Click(object sender, RoutedEventArgs e)
        {
            // Создаем новое окно
            MainWindow newWindow = new MainWindow();
            // Устанавливаем его как главное окно приложения
            System.Windows.Application.Current.MainWindow = newWindow;
            // Показываем новое окно
            newWindow.Show();
            // Закрываем текущее окно
            this.Close();
        }
    }
}