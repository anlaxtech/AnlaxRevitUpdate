using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AnlaxRevitUpdate
{
    [Serializable]
    public class AssemblyLoader : MarshalByRefObject
    {
        public string LoadAndExecute(string assemblyPath, string typeName, string methodName, object[] methodArgs)
        {
            try
            {
                // Загружаем сборку по указанному пути
                Assembly assembly = Assembly.LoadFrom(assemblyPath);

                // Ищем тип по имени
                var type = assembly.GetType(typeName);
                if (type == null)
                    return $"Тип {typeName} не найден в загруженной сборке.";

                // Создаем экземпляр типа
                object instance = Activator.CreateInstance(type);

                // Ищем метод по имени
                var method = type.GetMethod(methodName);
                if (method == null)
                    return $"Метод {methodName} не найден в типе {typeName}.";

                // Вызов метода
                var result = method.Invoke(instance, methodArgs);
                return result?.ToString() ?? "Метод вернул null.";
            }
            catch (Exception ex)
            {
                return "Ошибка при загрузке сборки или выполнении метода: " + ex.Message;
            }
        }
    }

}
