using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace AnlaxRevitUpdate
{
    public class HotReloadAssemblyLoadContext : AssemblyLoadContext
    {
        public HotReloadAssemblyLoadContext() : base()
        {
        }
        protected override Assembly Load(AssemblyName assemblyName)
        {
            // Логика загрузки сборок при необходимости
            return null;
        }
    }
}
