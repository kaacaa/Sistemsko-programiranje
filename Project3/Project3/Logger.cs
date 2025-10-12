using System;

public static class Logger
{
    private static readonly object _lock = new object();

    public static void Log(string message)
    {
        lock (_lock)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}
