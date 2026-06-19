using System;
using System.IO;

namespace GaldrApp;

public class Config
{
    public string LogFilePath { get; set; }

    public static Config Create(string appName)
    {
        string dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName);
        Directory.CreateDirectory(dataDirectory);

        return new Config
        {
            LogFilePath = Path.Combine(dataDirectory, "log.txt"),
        };
    }
}
