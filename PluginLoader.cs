using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AnlaxRevitUpdate
{
    public class PluginLoader
    {
        public void LoadAndInvokeMethod(string assemblyPath)
        {
            try
            {
                // Шаг 1: Чтение сборки с Mono.Cecil
                AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath);
                Console.WriteLine($"Загружена сборка: {assemblyDefinition.Name}");

                // Шаг 2: Найти нужный тип
                TypeDefinition targetType = assemblyDefinition.MainModule.Types.FirstOrDefault(t => t.Name == "ВашТип");
                if (targetType == null)
                {
                    Console.WriteLine("Тип не найден");
                    return;
                }

                // Шаг 3: Найти нужный метод
                MethodDefinition targetMethod = targetType.Methods.FirstOrDefault(m => m.Name == "DownloadPluginUpdate");
                if (targetMethod == null)
                {
                    Console.WriteLine("Метод не найден");
                    return;
                }

                Console.WriteLine($"Метод {targetMethod.Name} найден");

                // Шаг 4: Загрузка сборки и вызов метода через рефлексию
                Assembly loadedAssembly = Assembly.LoadFrom(assemblyPath);
                Type runtimeType = loadedAssembly.GetType(targetType.FullName);

                if (runtimeType == null)
                {
                    Console.WriteLine("Тип не найден через рефлексию");
                    return;
                }

                object instance = Activator.CreateInstance(runtimeType);
                MethodInfo runtimeMethod = runtimeType.GetMethod("DownloadPluginUpdate");

                if (runtimeMethod != null)
                {
                    runtimeMethod.Invoke(instance, null);
                    Console.WriteLine("Метод успешно вызван");
                }
                else
                {
                    Console.WriteLine("Метод не найден через рефлексию");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }
    }
}
