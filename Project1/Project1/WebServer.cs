using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading;

public static class WebServer
{
    // HttpListener za HTTP komunikaciju
    private static readonly HttpListener listener = new HttpListener();

    private const string baseUrl = "http://localhost:8080/";

    private static volatile bool _isRunning = true;  //volatile omogucava da promene budu vidljive svim nitima, time signaliziramo
                                                     // kada server pretane da radi,
                                                     // ova promenljiva se nikad ne kešira unutar CPU registra jedne niti — svaka nit
                                                     // uvek čita najnoviju vrednost iz glavne memorije.

    public static void Start()
    {
        // Postavljanje prefiksa URL-a na kojem server osluskuje
        listener.Prefixes.Add(baseUrl);
        listener.Start();

        Logger.Log($"Server pokrenut na {baseUrl}\n");
        Logger.Log("Pritisnite Ctrl+C za zaustavljanje...\n");

        // Rukovanje Ctrl+C
        Console.CancelKeyPress += (sender, e) =>      //„Dodaj novi event handler (metodu, akciju ili lambda izraz)
                                                      //koja će se pokrenuti kad se desi taj događaj
        {
            e.Cancel = true;              //Ne prekidaj aplikaciju odmah, ja ću sam da odradim gašenje
            Logger.Log("Zaustavljanje servera...");
            _isRunning = false;
            listener.Stop();        //prestaje da sluša nove HTTP zahteve,i oslobađa port (8080) koji je koristio
        };

        while (_isRunning)
        {
            try
            {
                // Blokirajuci poziv - ceka na dolazni HTTP zahtev
                var context = listener.GetContext();        //DODAJ KASNIJE

                // (efikasnije za vise kratkotrajnih zahteva)
                ThreadPool.QueueUserWorkItem(HandleRequest, context);   //ugrađeni mehanizam u .NET-u koji održava grupu (bazeni) već kreiranih
                                                                        //niti koje sistem može ponovo koristiti za izvršavanje kratkotrajnih zadataka
                                                                        //umesto da svaki put pravimo novu nit (što je „skupo“ za CPU i memoriju),
                                                                        //.NET koristi ThreadPool koji ima spremne niti za upotrebu.

                // ALTERNATIVA: Klasicna nit (bolje za duze operacije)
                // Thread t = new Thread(() => HandleRequest(context));
                // t.Start();
            }
            catch (HttpListenerException) when (!_isRunning)
            {
                // Ovo je OK kada se server zaustavlja
                break;
            }
            catch (Exception ex)
            {
                if (_isRunning)// Loguj samo ako je server još uvek pokrenut
                {
                    Logger.Log($"Greska: {ex.Message}");
                }
            }
        }

        Logger.Log("Server zaustavljen.");
    }
 
    private static void HandleRequest(object state)
    {
        var context = (HttpListenerContext)state;
        string url = context.Request.RawUrl ?? "";

        Logger.Log($"Primljen zahtev: {url}");

        // Ignorisemo favicon.ico zahteve browsera
        if (url == "/favicon.ico")
        {
            context.Response.StatusCode = 404;
            context.Response.Close();
            return;
        }

        string responseText = "";
        string details = "";
        // flag za logovanje
        bool successfullyProcessed = false;

        try
        {
            // PROVERA KESA - koristimo MemoryCache klasu
            if (MemoryCache.TryGet(url, out string cachedResponse))
            {
                responseText = cachedResponse;
                successfullyProcessed = true;
                details = "Odgovor preuzet iz kesa";
                Logger.Log("Zahtev uspesno obradjen - kesiran odgovor");
            }
            else
            {
                // Ako nije u kesu, pravimo novi API poziv
                responseText = ArtApiService.Search(url);

                // Upis u kes
                MemoryCache.Add(url, responseText);

                successfullyProcessed = true;
                details = "API poziv uspesan - sacuvano u kes";
                Logger.Log("Zahtev uspesno obradjen - novi API poziv");
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 200;
        }
        catch (Exception ex)
        {
            successfullyProcessed = false;
            responseText = $"{{\"error\": \"Greska u obradi zahteva: {ex.Message}\"}}";
            details = $"Greska: {ex.Message}";
            context.Response.StatusCode = 500;

            Logger.Log($"Greska pri obradi zahteva: {ex.Message}");
        }

        // Logovanje detalja obrade
        Logger.Log($"Detalji obrade: {details}");
        Logger.Log($"Status: {(successfullyProcessed ? "USPESNO\n" : "NEUSPESNO\n")}");

        // Slanje odgovora klijentu
        try
        {
            byte[] buffer = Encoding.UTF8.GetBytes(responseText);
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }
        catch (Exception ex)
        {
            Logger.Log($"Greska pri slanju odgovora: {ex.Message}");
        }
    }
}