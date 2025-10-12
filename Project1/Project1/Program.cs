using System;
using System.Threading;

namespace Project1
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // Glavna nit pokreće server
                WebServer.Start();
            }
            catch (Exception ex)
            {
                Logger.Log($"Kriticna greska: {ex.Message}");
            }

            Logger.Log("Pritisnite bilo koji taster za izlaz...");
            Console.ReadKey();
        }
    }
}