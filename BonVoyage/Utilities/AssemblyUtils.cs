using System;

namespace BonVoyage
{
    public class AssemblyUtils
    {
        /// <summary>
        /// Check if specific assembly is loaded
        /// </summary>
        /// <param name="name">Assembly name</param>
        /// <returns>True = assembly loaded. False = Assembly not loaded.</returns>
        internal static bool AssemblyIsLoaded(string assemblyName)
        {
            bool result = false;

            int i = 0;
            while (!result && (i < AssemblyLoader.loadedAssemblies.Count))
            {
                if (AssemblyLoader.loadedAssemblies[i].name == assemblyName)
                    result = true;
                i++;
            }

            return result;
        }

        /// <summary>
        /// Get path of BonVoyage module
        /// </summary>
        /// <returns></returns>
        internal static string GetModulePath()
        {
            string codeBase = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            path = System.IO.Path.GetDirectoryName(path);
            return path;
        }
    }

}
