using System;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public static class WebServer
{
    private static readonly HttpListener listener = new HttpListener();

    private const string baseUrl = "http://localhost:8080/";

    private static volatile bool _isRunning = true;

    public static async Task StartAsync()
    {
        listener.Prefixes.Add(baseUrl);
        listener.Start();

        Logger.Log($"Server pokrenut na {baseUrl}\n");
        Logger.Log("Pritisnite Ctrl+C za zaustavljanje...\n");

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Logger.Log("Zaustavljanje servera...");
            _isRunning = false;
            listener.Stop();
        };

        while (_isRunning)
        {
            try
            {
                var getContextTask = Task.Run(() =>
                {
                    try
                    {
                        return listener.GetContext();
                    }
                    catch (HttpListenerException) when (!_isRunning)
                    {
                        return null;
                    }
                });

                var context = await getContextTask;

                // dal se server zaustavio
                if (context == null)
                {
                    Logger.Log("Server se graciozno zaustavlja...");
                    break;
                }

                await HandleRequestAsync(context);
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    Logger.Log($"Greska pri prijemu zahteva: {ex.Message}");
                    await Task.Delay(1000);
                }
            }
        }
    }

    private static async Task HandleRequestAsync(HttpListenerContext context)
    {

        try
        {
            string url = context.Request.RawUrl ?? "";
            Logger.Log($"Primljen zahtev: {url}");

            // brza validacija
            if (!IsValidRequest(url))
            {
                await SendErrorResponse(context, 400, "Nevalidan zahtev.");
                return;
            }

            // ignorisemo
            if (url == "/favicon.ico")
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            string responseText = "";
            string details = "";
            bool successfullyProcessed = false;

            try
            {
                if (MemoryCache.TryGet(url, out string cachedResponse))
                {
                    responseText = cachedResponse;
                    successfullyProcessed = true;
                    details = "Odgovor preuzet iz kesa";
                    Logger.Log("Zahtev uspešno obraden - kesiran odgovor");
                }

                if (string.IsNullOrEmpty(responseText))
                {
                    responseText = await ArtApiService.SearchAsync(url);
                    MemoryCache.Add(url, responseText);
                    successfullyProcessed = true;
                    details = "API poziv uspesan - sačuvano u kes";
                    Logger.Log("Zahtev uspesno obraden - novi API poziv");
                }

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 200;
            }
            catch (Exception ex)
            {
                successfullyProcessed = false;
                responseText = $"{{\"error\": \"Greska u obradi zahteva\"}}";
                details = $"Greska: {ex.Message}";
                context.Response.StatusCode = 500;
                Logger.Log($"Greska pri obradi zahteva: {ex.Message}");
            }

            Logger.Log($"Detalji obrade: {details}");
            Logger.Log($"Status: {(successfullyProcessed ? "USPESNO\n" : "NEUSPESNO\n")}");

            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(responseText);
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Logger.Log($"Greska pri slanju odgovora: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Greska u HandleRequestAsync: {ex.Message}");
        }
    }

    private static bool IsValidRequest(string url)
    {
        // Brze provere
        if (string.IsNullOrEmpty(url) || url.Length > 500)
            return false;

        // Osnovne bezbednosne provere
        if (url.Contains("..") || url.Contains("//") || url.Contains("\\"))
            return false;

        // SQL/XSS provere
        if (url.Contains("script") || url.Contains("select ") || url.Contains("insert "))
            return false;

        return true;
    }

    private static async Task SendErrorResponse(HttpListenerContext context, int statusCode, string message)
    {
        try
        {
            string errorResponse = $"{{\"error\": \"{message}\"}}";
            byte[] buffer = Encoding.UTF8.GetBytes(errorResponse);

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;

            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();

            Logger.Log($"Slanje error odgovora: {statusCode} - {message}");
        }
        catch (Exception ex)
        {
            Logger.Log($"Greska pri slanju error odgovora: {ex.Message}");
        }
    }
}