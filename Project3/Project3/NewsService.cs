using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public class NewsService
{
    private readonly HttpClient _httpClient;

    public NewsService()
    {
        _httpClient = new HttpClient();     //.net klasa za slanje i primanje http zahteva, thread-safe, 
                                            //jedinstvena instanca za sve zahteve, da se ne otvara i zatvara stalno
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ReactiveNewsServer");   
    }

    public async Task<List<NewsArticle>> FetchNewsAsync(string keyword, List<string> categories)
    {
        if (categories == null || categories.Count == 0)
        {
            Logger.Log("Nema kategorija za pretragu.");
            return new List<NewsArticle>();
        }

        // Kreiranje liste asinhronih zadataka (jedan zadatak po kategoriji)
        var fetchTasks = categories.Select(category => FetchSingleCategoryAsync(keyword, category)).ToList();

        var results = await Task.WhenAll(fetchTasks);

        // spajamo rezultate
        var allArticles = results.SelectMany(list => list)
                                 .GroupBy(article => article.Url)
                                 .Select(group => group.First())
                                 .ToList();

        return allArticles;
    }

    private async Task<List<NewsArticle>> FetchSingleCategoryAsync(string keyword, string category)
    {
        string apiKey = "d8519999b1f549378e48c2e2dcdfa0e8";

        string keywordEncoded = Uri.EscapeDataString(keyword);
        string categoryEncoded = Uri.EscapeDataString(category);

        string url = $"https://newsapi.org/v2/top-headlines?country=us&q={keywordEncoded}&category={categoryEncoded}&apiKey={apiKey}";
        Logger.Log($"Pozivam URL za kategoriju '{category}': {url}");

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var newsResponse = JsonSerializer.Deserialize<NewsResponse>(content, options);

            return newsResponse?.Articles ?? new List<NewsArticle>();
        }
        catch (HttpRequestException ex)
        {
            Logger.Log($"Greska pri pozivu API-ja za kategoriju '{category}': {ex.Message}");
            return new List<NewsArticle>();
        }
    }

    /*public async Task<List<NewsArticle>> FetchNewsAsync(string keyword, string category)        //ovo radi paralelno, ne blokira glavnu nit
    {
        string apiKey = "d8519999b1f549378e48c2e2dcdfa0e8";
        //string apiKey = Environment.GetEnvironmentVariable("NEWSAPI_KEY") ?? "";

        string keywordEncoded = Uri.EscapeDataString(keyword);      //Uri.EscapeDataString() služi da ispravno kodira tekst koji ide u URL
        string categoryEncoded = Uri.EscapeDataString(category);

        //sastavlja kompletan url za newsapi zahtev
        string url = $"https://newsapi.org/v2/top-headlines?country=us&q={keywordEncoded}&category={categoryEncoded}&apiKey={apiKey}";
        Logger.Log($"Pozivam URL: {url}"); 

        var response = await _httpClient.GetAsync(url);     //salje http zahtev na zadati url i ceka odgovor
        response.EnsureSuccessStatusCode();     //EnsureSuccessStatusCode() baca izuzetak ako je status npr. 404, 401, 500
                                                //itd — znači da automatski zaustavlja kod ako API vrati grešku

        var content = await response.Content.ReadAsStringAsync();       //cita telo http odgovora, json tekst, kao obican string

        //ovo koristimo da pretvorimo json u .net objekat
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };     //ovo koristimo da ne brinemo o malim i velikim slovima
        var newsResponse = JsonSerializer.Deserialize<NewsResponse>(content, options);      //cita json string content i pravi objekat tipa newsresponse

        return newsResponse?.Articles ?? new List<NewsArticle>();       //vraca listu clanaka iz odgovora ili ako je null vraca praznu listu
    }*/
}
