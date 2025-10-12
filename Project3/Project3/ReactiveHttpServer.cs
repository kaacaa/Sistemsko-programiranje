using System;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

public class ReactiveHttpServer
{
    private readonly HttpListener _listener;    //klasa koja implementira jednostavan http server(slusa na datu url/port)
    private readonly NewsService _newsService;      //http pozivi prema news apiju
    private readonly TopicModelingService _topicModelingService;        //za odredjivanje tema iz naslova

    public ReactiveHttpServer(int port)
    {
        _listener = new HttpListener();     //slusa zahteve od klijenata
        _listener.Prefixes.Add($"http://localhost:{port}/");        //prefixes lista svih adresa na kojima server slusa, mora da se zavrsi sa /
        _newsService = new NewsService();       
        _topicModelingService = new TopicModelingService();     
    }

    public void Start()
    {
        _listener.Start();      //fizicki pokrece slusanje na portu
        Logger.Log($"Server pokrenut na {_listener.Prefixes.First()}");     

        Observable.FromAsync(() => _listener.GetContextAsync())     //
            .Repeat()
            .ObserveOn(TaskPoolScheduler.Default)  ////koristi thread pool, dolani http zahtevi nece da blokiraju glavnu nit
                                                   //vec se odvijaju paralelno
            .Subscribe(async context => await HandleRequest(context));      //za svaki dolazni zahtev, poziva handle request , obradjuje ga i salje odgovor
                                                                            //server može istovremeno da obradi više zahteva, a glavna nit listener-a
                                                                            //ostaje slobodna da prihvata nove zahteve

    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        if (context.Request.RawUrl.EndsWith("favicon.ico"))     //preskace onu ikonicu 
        {
            context.Response.StatusCode = 204; // No Content
            context.Response.Close();
            return; // Ignoriši ovaj zahtev
        }
        var query = context.Request.QueryString;    //cita parmetre urla koji dolaze nakon ?
        var keyword = query["keyword"] ?? "bitcoin";        //uzima kljucnu rec a ako nije pronadjena onda stavlja to sto smo zadali
        var category = query["category"] ?? "business";

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
    }
}
