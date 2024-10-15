using Mono.Cecil;
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
using System.Runtime.Loader;  // Для AssemblyLoadContext

namespace AnlaxRevitUpdate
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string PluginAutoUpdateDirectory { get; set; }
        static string ClassUpdtareName = "IPluginUpdater";
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
                var targetDirectory = directoryInfo.Parent.Parent;

                // Получаем название папки (в вашем случае это версия Revit)
                string revitVersion = targetDirectory.Name;
                return revitVersion;
            }
        }
        public string FolderPluginMain
        {
            get
            {
                var directoryInfo = new System.IO.DirectoryInfo(PluginAutoUpdateDirectory);

                // Поднимаемся на 3 уровня вверх
                var targetDirectory = directoryInfo.Parent;

                // Получаем название папки (в вашем случае это версия Revit)
                string revitVersion = targetDirectory.Name;
                return revitVersion;
            }
        }
        public bool IsDebug
        {
            get
            {
                if (PluginAutoUpdateDirectory.Contains("AnlaxDev"))
                {
                    return true;
                }
                return false;
            }
        }
        public List<string> DllPaths = new List<string>();
        public MainWindow()
        {

            InitializeComponent();
            // Формируем путь к RevitAPIUI.dll на основе версии Revit
            PluginAutoUpdateDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
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
                    TextBlockMessage.Text = "Не закрывайте окно. Идет проверка обновления плагина Anlax\n";
                    MessageBox.Show("das");
                    Show();
                    // Устанавливаем максимальное значение прогрессбара
                    DllPaths = FindDllsWithApplicationStart();
                    ProgressBarDownload.Maximum = DllPaths.Count + 1;
                    int progress = 0;

                    Task.Run(() =>
                    {
                        foreach (string dll in DllPaths)
                        {
                            string message = HotReload(dll);
                            string plugName = GetPluginName(dll);
                            progress++;
                            Dispatcher.Invoke(() =>
                            {
                                ProgressBarDownload.Value = progress;
                                TextBlockMessage.Text += $"Загрузка {plugName}. {message}\n";
                                TextBlockDownload.Text = $"{progress}/{DllPaths.Count + 1} загружено";
                            });
                        }
                        string messageMain =ReloadMainPlug();

                        // После завершения ставим максимальное значение и сообщаем о завершении
                        Dispatcher.Invoke(() =>
                        {
                            ProgressBarDownload.Value = ProgressBarDownload.Maximum;
                            TextBlockDownload.Text = "Обновление завершено!";
                            TextBlockMessage.Text += $"Загрузка AnlaxBase. {messageMain}\n";
                            TextBlockMessage.Text += "Все обновления завершены!\n";
                        });
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
            string repposotoryName = "AnlaxBase";
            var directoryInfo = new System.IO.DirectoryInfo(PluginDirectory);
            string plugFolderName = directoryInfo.Parent.Name;
            GitHubBaseDownload gitHubDownloader = new GitHubBaseDownload(pathToBaseDll, IsDebug, token, userName, repposotoryName, "AnlaxBase");
            string status =gitHubDownloader.HotReloadPlugin(false);
            return status;
        }
        public string HotReload(string path)
        {
            try
            {
                // Используем Mono.Cecil для анализа сборки
                var assemblyDefinition = AssemblyDefinition.ReadAssembly(path);

                TypeDefinition typeStart = null;
                foreach (var type in assemblyDefinition.MainModule.Types)
                {
                    // Проверяем, реализует ли тип интерфейс IPluginUpdater
                    if (type.Interfaces.Any(i => i.InterfaceType.Name == "IPluginUpdater"))
                    {
                        typeStart = type;
                        break;
                    }
                }

                if (typeStart != null)
                {
                    // Создаем новый домен для загрузки сборки
                    AppDomain domain = AppDomain.CreateDomain("HotReloadDomain");

                    // Создаем прокси-объект в новом домене
                    var loader = (AssemblyLoader)domain.CreateInstanceFromAndUnwrap(
                        typeof(AssemblyLoader).Assembly.Location,
                        typeof(AssemblyLoader).FullName);

                    // Вызов метода DownloadPluginUpdate через прокси в новом домене
                    string result = loader.LoadAndExecute(
                        path,                        // Путь к сборке
                        typeStart.FullName,           // Полное имя типа, реализующего IPluginUpdater
                        "DownloadPluginUpdate",       // Имя вызываемого метода
                        new object[] { path, false }  // Аргументы метода (path и IsDebug)
                    );

                    // Разрушаем домен после завершения работы
                    AppDomain.Unload(domain);

                    return result;
                }
                else
                {
                    return "Класс, реализующий IPluginUpdater, не найден.";
                }
            }
            catch (Exception ex)
            {
                return "Общая ошибка: " + ex.Message;
            }
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

        private string GetPluginName(string filePath)
        {
            // Получаем путь до файла без последней части
            string directory = System.IO.Path.GetDirectoryName(filePath);

            // Разбиваем путь на папки
            string[] pathParts = directory.Split(System.IO.Path.DirectorySeparatorChar);

            // Получаем имя файла без расширения
            string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(filePath);

            // Берем две последние папки и имя файла
            string result = System.IO.Path.Combine(pathParts[pathParts.Length - 1], fileNameWithoutExtension);

            return result;
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
                    // Используем Mono.Cecil для анализа сборки
                    var assemblyDefinition = AssemblyDefinition.ReadAssembly(dll);

                    TypeDefinition typeStart = null;
                    foreach (var type in assemblyDefinition.MainModule.Types)
                    {
                        // Проверяем, реализует ли тип интерфейс IPluginUpdater
                        if (type.Interfaces.Any(i => i.InterfaceType.Name == "IPluginUpdater"))
                        {
                            typeStart = type;
                            break;
                        }
                    }

                    if (typeStart != null)
                    {
                        result.Add(dll);
                    }
                }
                catch (Exception ex)
                {
                    // Логируем ошибки, если нужно
                    // Console.WriteLine($"Ошибка при обработке {dll}: {ex.Message}");
                }
            }

            return result;
        }
        private void ButtonRestart_Click(object sender, RoutedEventArgs e)
        {
            // Получаем текущий путь к исполняемому файлу
            string appPath = Process.GetCurrentProcess().MainModule.FileName;

            // Запускаем новый процесс
            Process.Start(appPath);

            // Завершаем текущее приложение
            Application.Current.Shutdown();
        }
    }
}