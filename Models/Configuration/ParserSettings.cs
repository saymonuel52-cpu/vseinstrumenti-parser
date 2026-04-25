using System.Text.Json.Serialization;

namespace VseinstrumentiParser.Models.Configuration
{
    public class ParserSettings
    {
        [JsonPropertyName("BaseUrls")]
        public BaseUrlsSettings BaseUrls { get; set; } = new BaseUrlsSettings();

        [JsonPropertyName("RequestSettings")]
        public RequestSettings RequestSettings { get; set; } = new RequestSettings();

        [JsonPropertyName("ParsingLimits")]
        public ParsingLimits ParsingLimits { get; set; } = new ParsingLimits();

        [JsonPropertyName("ExportSettings")]
        public ExportSettings ExportSettings { get; set; } = new ExportSettings();
    }

    public class BaseUrlsSettings
    {
        [JsonPropertyName("Vseinstrumenti")]
        public string Vseinstrumenti { get; set; } = "https://www.vseinstrumenti.ru";

        [JsonPropertyName("Volt220")]
        public string Volt220 { get; set; } = "https://www.220-volt.ru";
    }

    public class RequestSettings
    {
        [JsonPropertyName("TimeoutSeconds")]
        public int TimeoutSeconds { get; set; } = 30;

        [JsonPropertyName("MaxRetries")]
        public int MaxRetries { get; set; } = 3;

        [JsonPropertyName("RetryDelayMs")]
        public int RetryDelayMs { get; set; } = 1000;

        [JsonPropertyName("DelayBetweenRequestsMs")]
        public int DelayBetweenRequestsMs { get; set; } = 2000;

        [JsonPropertyName("UserAgent")]
        public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    }

    public class ParsingLimits
    {
        [JsonPropertyName("MaxPagesPerCategory")]
        public int MaxPagesPerCategory { get; set; } = 10;

        [JsonPropertyName("MaxProductsPerPage")]
        public int MaxProductsPerPage { get; set; } = 50;

        [JsonPropertyName("MaxConcurrentRequests")]
        public int MaxConcurrentRequests { get; set; } = 3;

        [JsonPropertyName("EnableCaching")]
        public bool EnableCaching { get; set; } = true;

        [JsonPropertyName("CacheDurationMinutes")]
        public int CacheDurationMinutes { get; set; } = 60;
    }

    public class ExportSettings
    {
        [JsonPropertyName("DefaultFormat")]
        public string DefaultFormat { get; set; } = "CSV";

        [JsonPropertyName("OutputDirectory")]
        public string OutputDirectory { get; set; } = "./exports";

        [JsonPropertyName("IncludeTimestamp")]
        public bool IncludeTimestamp { get; set; } = true;

        [JsonPropertyName("CompressOutput")]
        public bool CompressOutput { get; set; } = false;
    }
}