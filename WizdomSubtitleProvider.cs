using Wizdom.Plugin.Helpers;
using Wizdom.Plugin.Model;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Wizdom.Plugin
{
    public class WizdomSubtitleProvider : ISubtitleProvider
    {
        private readonly ILogger _logger;
        private WizdomExplorer _WizdomExplorer;
        private ImdbExplorer _imdbExplorer;

        public WizdomSubtitleProvider(
            ILogger logger )
        {
            _logger = logger;
        }

        public string Name => "Wizdom";

        public IEnumerable<string> CurrentSupportedLanguages => new[] { "he", "heb" };
        public IEnumerable<VideoContentType> SupportedMediaTypes => new[] { VideoContentType.Episode, VideoContentType.Movie };

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            // Initialize explorers            
            _WizdomExplorer = WizdomExplorer.Instance;
            _imdbExplorer = ImdbExplorer.Instance;

            if (string.IsNullOrEmpty(request.Language) || !string.Equals(request.Language, "he", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info($"Wizdom: Subtitle search was initiated for non-Hebrew language. Wizdom exiting...");
                return Enumerable.Empty<RemoteSubtitleInfo>();
            }
            else
            {
                _logger.Info($"Wizdom: Hebrew subtitle search was initiated");
            }

            try
            {
                var searchResults = new List<RemoteSubtitleInfo>();

                if (request.ContentType == VideoContentType.Movie)
                {
                    _logger.Info($"Wizdom: Subtitle request type was detected as Movie");
                    searchResults.AddRange(await SearchMovies(request, cancellationToken));
                }
                else if (request.ContentType == VideoContentType.Episode)
                {
                    _logger.Info($"Wizdom: Subtitle request type was detected as Series");
                    searchResults.AddRange(await SearchEpisodes(request, cancellationToken));
                }

                return searchResults;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error searching for subtitles in Wizdom. {ex}");
                return Enumerable.Empty<RemoteSubtitleInfo>();
            }
        }

        private async Task<IEnumerable<RemoteSubtitleInfo>> SearchMovies(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            if (!request.ProviderIds.TryGetValue("Imdb", out var imdbId) || string.IsNullOrWhiteSpace(imdbId))
            {
                _logger.Info("Wizdom: Movie IMDB id not provided in request, trying free search.");
                imdbId = await _WizdomExplorer.WizdomFreeSearch(request.Name);
            }

            if (string.IsNullOrWhiteSpace(imdbId))
            {
                _logger.Info("Wizdom: Could not find IMDB id for movie, exiting search.");
                return Enumerable.Empty<RemoteSubtitleInfo>();
            }
            else
            {
                // Search for subtitles
                _logger.Info($"Wizdom: Searching subtitles for Movie: {request.Name}, ImdbID: {imdbId}");
                return await _WizdomExplorer.GetMovieRemoteSubtitles(imdbId);
            }
        }

        private async Task<IEnumerable<RemoteSubtitleInfo>> SearchEpisodes(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            // Get Series IMDB ID
            // Replace the direct index accessor with a safe check
            string seriesImdbId;
            if (!request.ProviderIds.TryGetValue("Imdb", out var episodeImdbId) || string.IsNullOrWhiteSpace(episodeImdbId))
            {
                _logger.Info("Wizdom: Episode IMDB id not provided in request, trying free search.");
                seriesImdbId = await _WizdomExplorer.WizdomFreeSearch(request.SeriesName);
            }
            else
            {
                seriesImdbId = await _imdbExplorer.GetSeriesImdbId(episodeImdbId);
            }

            if (string.IsNullOrWhiteSpace(seriesImdbId))
            {
                _logger.Info("Wizdom: Could not find IMDB id for series, exiting search.");
                return Enumerable.Empty<RemoteSubtitleInfo>();
            }
            else
            {
                // Search for subtitles
                _logger.Info($"Wizdom: Searching subtitles for Series: {request.SeriesName}, Season: {request.ParentIndexNumber}, Episode: {request.IndexNumber}, ImdbID: {seriesImdbId}");
                return await _WizdomExplorer.GetSeriesRemoteSubtitles(seriesImdbId, request.ParentIndexNumber, request.IndexNumber);
            }
        }

        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            _logger.Info($"Wizdom: User Requested downloading subtitle with ID: {id}");
            return await _WizdomExplorer.DownloadSubtitle(id);
        }
    }
}