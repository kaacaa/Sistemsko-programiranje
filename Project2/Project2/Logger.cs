using System;

public static class Logger
{
    private static readonly object _lock = new object();

    public static void Log(string message)
    {
        // Lock je i dalje potreban jer Console nije thread-safe
        lock (_lock)
        {
            Console.WriteLine($"[{DateTime.Now}] {message}");
        }
    }
}