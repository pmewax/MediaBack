using System.IO;

namespace MediaBack.Infrastructure;

public static class ErrorLog
{
    public static void Write(string directory, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(directory); var path = Path.Combine(directory, "error.log");
            if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024) File.Move(path, path + ".old", true);
            File.AppendAllText(path, DateTimeOffset.Now + " " + exception + Environment.NewLine);
        }
        catch (Exception) { }
    }
}
