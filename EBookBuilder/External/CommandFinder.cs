using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace lpubsppop01.EBookBuilder
{
    class CommandFinder
    {
        public static string Find(string filename)
        {
            string appDirPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var envPathDirPaths = Environment.GetEnvironmentVariable("PATH").Split(';');
            var targetDirPaths = new[] { appDirPath }.Concat(envPathDirPaths);
            foreach (var targetDirPath in targetDirPaths)
            {
                if (!Directory.Exists(targetDirPath)) continue;
                string[] filePaths;
                try
                {
                    filePaths = Directory.EnumerateFiles(targetDirPath).ToArray();
                }
                catch
                {
                    continue;
                }
                foreach (var filePath in filePaths)
                {
                    if (Path.GetFileName(filePath) != filename) continue;
                    return filePath;
                }
            }
            return "";
        }
    }
}
