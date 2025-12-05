using MCPFeeder.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MCPFeeder.Services;

public interface IFeedService
{
    Task<IEnumerable<RSSFeed>> GetFeedsAsync(string url, DateTimeOffset start, DateTimeOffset end);
}

public class FeedService : IFeedService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<FeedService> _logger;

    public FeedService(IMemoryCache cache, ILogger<FeedService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<RSSFeed>> GetFeedsAsync(string url, DateTimeOffset start, DateTimeOffset end)
    {
        // Cache key based on URL and time interval
        var cacheKey = $"feeds_{url}_{start:yyyyMMdd}_{end:yyyyMMdd}";

        if (_cache.TryGetValue(cacheKey, out IEnumerable<RSSFeed>? cachedFeeds))
        {
            _logger.LogInformation($"Feed served from cache for {url}");
            return cachedFeeds??Enumerable.Empty<RSSFeed>();
        }


        var feeds = new List<RSSFeed>();

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        const string DateFormat = "ddd, dd MMM yyyy HH:mm:ss zzz";
        CultureInfo Culture = CultureInfo.InvariantCulture;

        try
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; ConsoleApp/1.0)");
            using var response = httpClient.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            content = content.Trim();

            XDocument xmlDoc = XDocument.Parse(content);
            var channel = xmlDoc.Root?.Element("channel");

            if (channel == null)
            {
                Console.WriteLine("No feeds found");
                throw new Exception("Invalid RSS feed format");
            }
            feeds.AddRange(channel.Elements("item")
                .Select(item => {
                    bool success = DateTimeOffset.TryParseExact(
                        item.Element("pubDate")?.Value,
                        DateFormat,
                        Culture,
                        DateTimeStyles.AssumeUniversal,
                        out DateTimeOffset parsedDate);

                    return new
                    {
                        ItemElement = item,
                        PublishDate = success ? (DateTimeOffset?)parsedDate : null,
                        Title = item.Element("title")?.Value
                    };
                })
                .Where(x => x.PublishDate.HasValue && x.PublishDate.Value >= start && x.PublishDate.Value <= end)
                .Select(item => new RSSFeed
                {
                    Title = item.Title?? string.Empty,

                    Content = WebUtility.HtmlDecode(
                        Regex.Replace(item.ItemElement.Element("description")?.Value ?? string.Empty, "<[^>]+>", " ").Trim()),

                    Link = item.ItemElement.Element("link")?.Value?? string.Empty,

                    PublishDate = item.PublishDate.GetValueOrDefault(start)
                }));
        }
        catch (Exception) { return Enumerable.Empty<RSSFeed>(); }

        // 1 day cache
        if (feeds.Any())
        _cache.Set(cacheKey, feeds, TimeSpan.FromHours(24));

        return await Task.FromResult(feeds);
    }
}