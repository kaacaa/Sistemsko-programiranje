using System;

class Program
{
    static void Main(string[] args)
    {
        var server = new ReactiveHttpServer(8080);
        server.Start();

        Console.WriteLine("Server radi... Pritisnite Enter za izlaz.");
        Console.ReadLine();
    }
}
