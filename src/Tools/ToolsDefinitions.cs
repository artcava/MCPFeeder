namespace MCPFeeder.Tools;
internal sealed class ToolsDefinitions
{
    public static class FeederTool
    {
        public const string Name = "get_feeds";
        public const string Description = "Feeds data from a specified URL.";

        public static class UrlParameter
        {
            public const string Name = "Url";
            public const string Description = "The URL to fetch data from.";
        }
    }
}
