using MCPFeeder.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace MCPFeeder.Tools;

public class FeederTool(ILogger<FeederTool> logger, IFeedService feedService)
{
    [Function(nameof(FeedUrl))]
    public async Task<string> FeedUrl(
        [McpToolTrigger(ToolsDefinitions.FeederTool.Name, ToolsDefinitions.FeederTool.Description)] 
        ToolInvocationContext context,
        [McpToolProperty(ToolsDefinitions.FeederTool.UrlParameter.Name, ToolsDefinitions.FeederTool.UrlParameter.Description, true)] 
        string url
    )
    {
        logger.LogInformation($"Fetching data from URL: {url}");
        
        try
        {
            // Fetch feeds for yesterday
            var now = DateTimeOffset.UtcNow;
            var end = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
            var start = end.AddDays(-1);

            logger.LogInformation($"Fetching data for day: {start.Date}");

            var feeds = await feedService.GetFeedsAsync(url, start, end);
            
            if (!feeds.Any())
            {
                return "No feeds found for the specified URL and time range.";
            }

            // Return feeds as text
            var result = feeds.Select(f => f.Content).ToList();
            return string.Join(Environment.NewLine, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching feeds from {Url}", url);
            return $"Error fetching feeds: {ex.Message}";
        }
    }
}