using System;
using System.Net.Http;

public static class ArtApiService
{
    //HttpClient instanca sa omogućenom redirekcijom
    //http client radi samo na windowsu 
    private static readonly HttpClient _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    // Dodavanje User-Agent hedera kako bi API dozvolio pristup (ne dobijamo 403)
    static ArtApiService()
    {
        _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    }

    // Sinhrona verzija pretrage
    public static string Search(string rawUrl)
    {
        var uri = new Uri("http://localhost" + rawUrl);
        var query = uri.Query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Upit nije validan.";
        }

        string apiUrl = $"https://api.artic.edu/api/v1/artworks/search?{query}";

        try
        {
            //httpclient je dizajniran da koristi asinhrone metode, nema sinhrone metode
            //zato koristimo .result da bi blokirali cekanje odgovora
            var response = _client.GetAsync(apiUrl).ConfigureAwait(false).GetAwaiter().GetResult(); // blokira dok se ne dobije odgovor 
            response.EnsureSuccessStatusCode(); // baca exception ako nije 200-OK
            return response.Content.ReadAsStringAsync().Result; // čita telo odgovora kao string
        }
        catch (Exception ex)
        {
            return $"Greska pri pozivu API-ja: {ex.Message}";
        }
    }
}
