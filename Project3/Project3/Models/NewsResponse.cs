using System.Collections.Generic;

public class NewsResponse
{
    public string Status { get; set; }
    public int TotalResults { get; set; }
    public List<NewsArticle> Articles { get; set; }
}
