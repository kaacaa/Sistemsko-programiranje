using System;
using System.Net.Http;
using System.Threading.Tasks;

public static class ArtApiService
{
    // HttpClient instanca sa omogućenom redirekcijom
    private static readonly HttpClient _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    // Dodavanje User-Agent hedera kako bi API dozvolio pristup (ne dobijamo 403)
    static ArtApiService()
    {
        _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    }

    // Asinhrona verzija pretrage
    public static async Task<string> SearchAsync(string rawUrl)
    {
        // Dodatna validacija pre nego što formiramo API URL
        if (!IsValidApiUrl(rawUrl))
        {
            return "{\"error\": \"Nevalidan URL za API poziv\"}";
        }

        var uri = new Uri("http://localhost" + rawUrl);
        var query = uri.Query.TrimStart('?');

        if (string.IsNullOrWhiteSpace(query))
        {
            return "{\"error\": \"Upit nije validan.\"}";
        }

        string apiUrl = $"https://api.artic.edu/api/v1/artworks/search?{query}";

        try
        {
            // Asinhroni poziv API-ja
            var response = await _client.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            return "{\"error\": \"Greska pri pozivu eksternog API-ja\"}";
        }
    }

    private static bool IsValidApiUrl(string rawUrl)
    {
        if (string.IsNullOrEmpty(rawUrl))
        {
            return false;
        } 

        // Sprečava SQL injection-like napade u URL-u
        if (rawUrl.ToLower().Contains("select") || 
            rawUrl.ToLower().Contains("insert") ||
            rawUrl.ToLower().Contains("delete") || 
            rawUrl.ToLower().Contains("drop"))
        {
            return false;
        }
           
        return true;
    }
}