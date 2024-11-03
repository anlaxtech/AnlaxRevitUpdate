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
        public bool GoodDownload {  get; set; }
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


        public MainWindow()
        {
            GoodDownload = true;
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
                    ClearLogFile();
                    TextBlockMessage.Text = "Не закрывайте окно. Идет проверка обновления плагина Anlax\n";
                    Show();
                    // Устанавливаем максимальное значение прогрессбара
                    Task.Run(() =>
                    {
                        string messageMain =ReloadMainPlug();

                        // После завершения ставим максимальное значение и сообщаем о завершении
                        Dispatcher.Invoke(() =>
                        {
                            ProgressBarDownload.Value = ProgressBarDownload.Maximum;
                            TextBlockDownload.Text = "Обновление завершено!";
                            TextBlockMessage.Text += $"Загрузка AnlaxBase. {messageMain}\n";
                            TextBlockMessage.Text += "Все обновления завершены!\n";
                        });
                        if (GoodDownload)
                        {
                            Timer timer = new Timer(CloseWindowCallback, null, 2000, Timeout.Infinite);
                        }

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
            string userName = "anlaxtech";
            string repposotoryName = "AnlaxBase";
            var directoryInfo = new System.IO.DirectoryInfo(PluginDirectory);
            GitHubBaseDownload gitHubDownloader = new GitHubBaseDownload(pathToBaseDll, IsDebug,  userName, repposotoryName, "AnlaxBase");
            string status =gitHubDownloader.HotReloadPlugin(true);
            if (status != "Загрузка прошла успешно" && status != "Загружена актуальная версия плагина")
            {
                GoodDownload = false;
            }
            return status;
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

        public List<string> FindDllsWithApplicationStart()
        {
            List<string> result = new List<string>();

            // Рекурсивно ищем все файлы с расширением .dll
            var dllFiles = Directory.GetFiles(PluginDirectory, "*.dll", SearchOption.AllDirectories);

            foreach (var dll in dllFiles)
            {
                using (var assemblyDefinition = AssemblyDefinition.ReadAssembly(dll))
                {
                    try
                    {
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
        public void ClearLogFile()
        {
            var path = System.IO.Path.Combine(PluginDirectory, "AnlaxPackageLog.txt");

            try
            {
                // Проверяем, доступен ли файл для записи
                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    fileStream.SetLength(0); // Очищаем файл, устанавливая его длину в 0
                }
            }
            catch (IOException ex)
            {
                // Если файл занят другим процессом, регистрируем предупреждение

            }
            catch (Exception ex)
            {
                // Логируем любые другие исключения

            }
        }
    }
}