using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Astc_Encoder_CSharp_Generator
{
    internal class GitHubFileResponse
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("encoding")]
        public string Encoding { get; set; }

        [JsonPropertyName("size")]
        public int? Size { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("sha")]
        public string Sha { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("git_url")]
        public string GitUrl { get; set; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; }

        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; }

        [JsonPropertyName("_links")]
        public Links Links { get; set; }
    }

    public class Links
    {
        [JsonPropertyName("git")]
        public string Git { get; set; }

        [JsonPropertyName("self")]
        public string Self { get; set; }

        [JsonPropertyName("html")]
        public string Html { get; set; }
    }
}
