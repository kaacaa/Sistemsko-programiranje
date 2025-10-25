using System;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Specialized;

public class ReactiveHttpServer
{
    private readonly HttpListener _listener;
    private readonly NewsService _newsService;
    private readonly TopicModelingService _topicModelingService;

    // FIKSNA LISTA: Samo zvanične News API kategorije za grubu klasifikaciju
    private static readonly HashSet<string> ValidNewsApiCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "business", "entertainment", "general", "health", "science", "sports", "technology"
    };

    public ReactiveHttpServer(int port)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _newsService = new NewsService();
        _topicModelingService = new TopicModelingService();
    }

    public void Start()
    {
        _listener.Start();
        Logger.Log($"Server pokrenut na {_listener.Prefixes.First()}");

        Observable.FromAsync(() => _listener.GetContextAsync())
            .Repeat()
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(async context => await HandleRequest(context));
    }

    public async Task HandleRequest(HttpListenerContext context)
    {
        try
        {
            var requestUrl = context.Request.Url;
            var absolutePath = requestUrl.AbsolutePath.TrimEnd('/').ToLowerInvariant();

            // 1. Provera za favicon.ico
            if (absolutePath.EndsWith("/favicon.ico"))
            {
                context.Response.StatusCode = 204; // No Content
                context.Response.Close();
                return;
            }

            string responseText = string.Empty;
            int statusCode = 200;

            if (absolutePath == "")
            {
                NameValueCollection query = context.Request.QueryString;
                var keyword = query["keyword"];

                string[] categoryValues = query.GetValues("category");

                List<string> userCategories = new List<string>();
                if (categoryValues != null)
                {
                    // Parsiranje svih unetih kategorija iz query stringa
                    userCategories = categoryValues
                                        .SelectMany(val => val.Split(new[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries))
                                        .Select(c => c.Trim().ToLowerInvariant())
                                        .Where(c => !string.IsNullOrWhiteSpace(c))
                                        .ToList();
                }

                // filtriranje
                List<string> apiCategories = userCategories
                    .Where(c => ValidNewsApiCategories.Contains(c))
                    .Distinct()
                    .ToList();

                // validacija
                if (string.IsNullOrWhiteSpace(keyword) || apiCategories.Count == 0)
                {
                    responseText = $"Nije dobar upit. Morate proslediti 'keyword' i validne 'category' parametre. Validne kategorije su: {string.Join(", ", ValidNewsApiCategories)}.";
                    statusCode = 400; // Bad Request
                }
                else
                {
                    Logger.Log($"Zahtev obradjen: keyword={keyword}, unete kategorije={string.Join(", ", userCategories)}, API kategorije={string.Join(", ", apiCategories)}");

                    // prosleđivanje liste validnih API kategorija
                    var articles = await _newsService.FetchNewsAsync(keyword, apiCategories);
                    Logger.Log($"Broj clanaka: {articles.Count}\n");

                    if (articles.Count > 0)
                    {
                        var titles = articles.Select(a => a.Title).ToList();

                        var topics = _topicModelingService.AnalyzeTopics(titles);

                        responseText = $"Pretrazene kategorije: {string.Join(", ", apiCategories)}\n\n" +
                                       string.Join("\n", articles.Select(a => $"Naslov: {a.Title} | Izvor: {a.Source.Name}")) +
                                       "\n\nAnalizirane Teme (Vas model):\n" + string.Join("\n", topics);
                    }
                    else
                    {
                        responseText = "API nije pronasao clanke za dati upit i kategorije.";
                    }
                }
            }
            else if (absolutePath == "/info")
            {
                responseText = $"Ovo je Info putanja. Koristite /?keyword=...&category=...&category=... za pretragu. Validne kategorije su: {string.Join(", ", ValidNewsApiCategories)}.";
                statusCode = 200;
            }
            else if (absolutePath == "/status")
            {
                responseText = $"Status servera: Aktivan i Asinhrone niti rade. Vreme: {DateTime.Now:HH:mm:ss}";
                statusCode = 200;
            }
            else
            {
                responseText = $"Nepoznata putanja: {absolutePath}. Validne putanje su: /?keyword=...&category=..., /info, /status.";
                statusCode = 404; // Not Found
            }

            // Slanje odgovora klijentu
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/plain";

            var buffer = Encoding.UTF8.GetBytes(responseText);
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }
        catch (Exception ex)
        {
            Logger.Log($"Kriticna greska u HandleRequest: {ex.Message}");
            try
            {
                context.Response.StatusCode = 500;
                context.Response.ContentType = "text/plain";
                var errorBuffer = Encoding.UTF8.GetBytes($"Interna greska servera: {ex.Message}");
                context.Response.ContentLength64 = errorBuffer.Length;
                await context.Response.OutputStream.WriteAsync(errorBuffer, 0, errorBuffer.Length);
                context.Response.OutputStream.Close();
            }
            catch { /* Ignorisemo greške */ }
        }
    }

    /*private async Task HandleRequest(HttpListenerContext context)
    {
        if (context.Request.RawUrl.EndsWith("favicon.ico"))     //preskace onu ikonicu 
        {
            context.Response.StatusCode = 204; // No Content
            context.Response.Close();
            return; // Ignoriši ovaj zahtev
        }
        var query = context.Request.QueryString;    //cita parmetre urla koji dolaze nakon ?
        var keyword = query["keyword"];        //uzima kljucnu rec a ako nije pronadjena onda null
        var category = query["category"];

        if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(category))
        {
            string errorResponse = "Nije dobar upit. Morate proslediti 'keyword' i 'category' parametre (npr. /?keyword=football&category=sport).";

            // Postavljanje odgovora za grešku 400 Bad Request
            context.Response.StatusCode = 400;
            context.Response.ContentType = "text/plain";

            var errorBuffer = Encoding.UTF8.GetBytes(errorResponse);
            context.Response.ContentLength64 = errorBuffer.Length;
            await context.Response.OutputStream.WriteAsync(errorBuffer, 0, errorBuffer.Length);
            context.Response.OutputStream.Close();

            Logger.Log("Greska 400: Nije dobar upit (nedostaju parametri).");
            return; // Završi obradu zahteva
        }

        Logger.Log($"Zahtev obraden: keyword={keyword}, category={category}");      

        var articles = await _newsService.FetchNewsAsync(keyword, category);        

        Logger.Log($"Broj članaka: {articles.Count}");      

        var topics = _topicModelingService.AnalyzeTopics(articles.Select(a => a.Title).ToList());       

        foreach (var article in articles)
            Logger.Log($"Naslov: {article.Title} | Izvor: {article.Source.Name}");

        Logger.Log("=== Teme ===");
        foreach (var topic in topics)
            Logger.Log($"Tema: {topic}");
        Logger.Log("============");

        var responseText = string.Join("\n", articles.Select(a => $"Naslov: {a.Title} | Izvor: {a.Source.Name}")) +
                           "\n\nTeme:\n" + string.Join("\n", topics);

        var buffer = Encoding.UTF8.GetBytes(responseText);
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        context.Response.Close();
    }*/
}
