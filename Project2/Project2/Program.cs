using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Glavna nit pokreće server asinhrono
            await WebServer.StartAsync();
        }
        catch (Exception ex)
        {
            Logger.Log($"Kriticna greska: {ex.Message}");
        }

        Logger.Log("Server je zaustavljen. Pritisnite bilo koji taster za izlaz...");
        Console.ReadKey();
    }
}