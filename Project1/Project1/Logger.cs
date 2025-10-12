using System;

public static class Logger
{
    private static readonly object _lock = new object();

    public static void Log(string message)
    {
        // samo jedna nit moze da upisuje u jednom trenutku
        lock (_lock)
        {
            Console.WriteLine($"[{DateTime.Now}] {message}");
        }
    }
}
