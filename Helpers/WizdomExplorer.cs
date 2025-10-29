using MediaBrowser.Common.Net;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wizdom.Plugin.Configuration;
using Wizdom.Plugin.Model;
using HttpRequestOptions = MediaBrowser.Common.Net.HttpRequestOptions;

namespace Wizdom.Plugin.Helpers
{

    public class WizdomExplorer
    {
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILogger _logger;
        private readonly IHttpClient _httpClient;
        private readonly IZipClient _zipClient;

        public WizdomExplorer(
            IJsonSerializer jsonSerializer,
            ILogger logger,
            IHttpClient httpClient,
            IZipClient zipClient)
        {
            _jsonSerializer = jsonSerializer;
            _logger = logger;
            _httpClient = httpClient;
            _zipClient = zipClient;
        }

        // Singleton implementation
        private static WizdomExplorer _instance;
        private static readonly object _lock = new();

        public static WizdomExplorer Instance
        {
            get
            {
                lock (_lock)
                {
                    return _instance;
                }
            }
        }

        public static void Initialize(IJsonSerializer jsonSerializer, ILogger logger, IHttpClient httpClient, IZipClient zipClient)
        {
            lock (_lock)
            {
                _instance ??= new WizdomExplorer(jsonSerializer, logger, httpClient, zipClient);
            }
        }

        private const string ApiBaseUrl = "https://wizdom.xyz/api";
        private const string SearchUrl = $"{ApiBaseUrl}/search";
        private const string DownloadUrl = $"{ApiBaseUrl}/files/sub";

        public async Task<string> WizdomFreeSearch(string title)
        {
            string searchUrl = $"{SearchUrl}?search={title}&page=0";
            var httpRequest = new HttpRequestOptions();
            httpRequest.Url = searchUrl;
            var response = await _httpClient.GetResponse(httpRequest);
            var initialResponse = _jsonSerializer.DeserializeFromStream<WizdomFreeSearch[]>(response.Content);
            if (initialResponse != null && initialResponse.Count() > 0)
            {
                var match = initialResponse.FirstOrDefault(x =>
                    string.Equals(x.title, title, StringComparison.Ordinal) ||
                    string.Equals(x.title_en, title, StringComparison.Ordinal));

                if (match != null && !string.IsNullOrWhiteSpace(match.imdb))
                {
                    _logger.Info($"Wizdom: Found IMDB id '{match.imdb}' for title '{title}'.");
                    return match.imdb;
                }
            }
            return null;
        }
        public async Task<List<RemoteSubtitleInfo>> GetMovieRemoteSubtitles(string imdbId)
        {
            var results = new List<RemoteSubtitleInfo>();

            string searchUrl = $"{SearchUrl}?action=by_id&imdb={imdbId}";
            var httpRequest = new HttpRequestOptions();
            httpRequest.Url = searchUrl;
            var response = await _httpClient.GetResponse(httpRequest);
            var initialResponse = _jsonSerializer.DeserializeFromStream<WizdomSearch[]>(response.Content);
            if (initialResponse != null && initialResponse.Count() > 0)
            {
                foreach (var subtitle in initialResponse)
                {
                    results.Add(new RemoteSubtitleInfo
                    {
                        Author = "Wizdom",
                        Name = subtitle.versioname,
                        ProviderName = Plugin.PluginName,
                        Id = subtitle.id,
                        Language = "he",
                        IsForced = false,
                        Format = "srt"
                    });
                }
            }
            return results;
        }
        public async Task<List<RemoteSubtitleInfo>>  GetSeriesRemoteSubtitles(string imdbId, int? seasonIndex, int? episodeIndex)
        {
            var results = new List<RemoteSubtitleInfo>();
            string searchUrl = $"{SearchUrl}?action=by_id&imdb={imdbId}&season={seasonIndex}&episode={episodeIndex}";
            var httpRequest = new HttpRequestOptions();
            httpRequest.Url = searchUrl;
            var response = await _httpClient.GetResponse(httpRequest);
            var initialResponse = _jsonSerializer.DeserializeFromStream<WizdomSearch[]>(response.Content);
            if (initialResponse.Count() > 0)
            {
                foreach (var subtitle in initialResponse)
                {
                    results.Add(new RemoteSubtitleInfo
                    {
                        Author = "Wizdom",
                        Name = subtitle.versioname,
                        ProviderName = Plugin.PluginName,
                        Id = subtitle.id,
                        Language = "he",
                        IsForced = false,
                        Format = "srt"
                    });
                }
            }
            return results;
        }
        public async Task<SubtitleResponse> DownloadSubtitle(string id)
        {
            var downloadUrl = $"{DownloadUrl}/{id}";
            var httpRequest = new HttpRequestOptions();
            httpRequest.Url = downloadUrl;
            var downloadResponse = await _httpClient.SendAsync(httpRequest, System.Net.Http.HttpMethod.Get.ToString());
            if (downloadResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                _logger.Warn($"Wizdom API download subtitle request failed: {downloadResponse.StatusCode} for ID: {id}");
                return null;
            }

            try
            {
                // Read entire content into memory once
                var contentStream = new MemoryStream();
                await downloadResponse.Content.CopyToAsync(contentStream);
                if (contentStream.Length == 0)
                {
                    _logger.Warn($"Wizdom: downloaded subtitle content was empty for ID: {id}");
                    return null;
                }

                // Detect ZIP by header bytes: "PK" (0x50, 0x4B)
                contentStream.Position = 0;
                bool isZip = false;
                if (contentStream.Length >= 4)
                {
                    byte[] header = new byte[4];
                    await contentStream.ReadAsync(header, 0, 4);
                    // Reset position after peek
                    contentStream.Position = 0;
                    if (header[0] == 0x50 && header[1] == 0x4B)
                    {
                        isZip = true;
                    }
                }

                if (!isZip)
                {
                    // Treat as raw SRT content
                    _logger.Info($"Wizdom: downloaded subtitle succesfully and detected as raw SRT for ID: {id}.");
                    contentStream.Position = 0L;
                    return new SubtitleResponse
                    {
                        Stream = contentStream,
                        Format = "srt",
                        Language = "he"
                    };
                }

                // Handle ZIP: extract to temp folder and find first .srt
                _logger.Info($"Wizdom: downloaded subtitle detected as ZIP archive for ID: {id}. Extracting...");
                var tempRoot = Path.Combine(Path.GetTempPath(), "Wizdom", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);
                try
                {
                    contentStream.Position = 0;
                    _zipClient.ExtractAllFromZip(contentStream, tempRoot, true);

                    // Search extracted files for .srt (case-insensitive)
                    var srtFile = Directory.EnumerateFiles(tempRoot, "*.srt", SearchOption.AllDirectories).FirstOrDefault();
                    if (srtFile == null)
                    {
                        _logger.Warn($"Wizdom: no .srt file found inside downloaded zip for ID: {id}");
                        return null;
                    }

                    _logger.Info($"Wizdom: extracted .srt file found. for ID: {id}. Preparing subtitle...");
                    var resultStream = new MemoryStream();
                    using (var fs = new FileStream(srtFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        await fs.CopyToAsync(resultStream);
                    }
                    resultStream.Position = 0L;
                    _logger.Info($"Wizdom: subtitle extraction and preparation completed for ID: {id}.");
                    return new SubtitleResponse
                    {
                        Stream = resultStream,
                        Format = "srt",
                        Language = "he"
                    };
                }
                finally
                {
                    // Cleanup extracted files
                    try
                    {
                        if (Directory.Exists(tempRoot))
                        {
                            _logger.Info($"Wizdom: Cleaning up...");
                            Directory.Delete(tempRoot, recursive: true);
                            _logger.Info($"Wizdom: Cleaned up successfully.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug($"Wizdom: failed to delete temp extraction folder {tempRoot}. {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Wizdom: failed downloading/extracting subtitle ID: {id}. {ex}");
                return null;
            }
        }
    }
}
