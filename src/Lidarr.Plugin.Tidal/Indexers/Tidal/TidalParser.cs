using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Instrumentation;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Plugin.Tidal;
using TidalSharp.Data;
using TidalSharp.Exceptions;

namespace NzbDrone.Core.Indexers.Tidal
{
    public class TidalParser : IParseIndexerResponse
    {
        private static readonly Regex BeginningThe = new Regex(@"^the\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex StandardizeSingleQuotes = new Regex(@"[\u0060\u00B4\u2018\u2019]", RegexOptions.Compiled);
        private static readonly Regex RepeatingSpaces = new Regex(@"\s{2,}", RegexOptions.Compiled);
        private static readonly Regex TrailingBrackets = new Regex(@"\s*[\(\[{].*[\)\]}]\s*$", RegexOptions.Compiled);
        private readonly Logger _logger;

        public TidalIndexerSettings Settings { get; set; }

        public TidalParser()
        {
            _logger = NzbDroneLogger.GetLogger(this);
        }

        private static string NormalizeForMatch(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            // Strip leading "The "
            text = BeginningThe.Replace(text, "");

            // Standardize single quotes
            text = StandardizeSingleQuotes.Replace(text, "'");

            // Collapse repeating spaces
            text = RepeatingSpaces.Replace(text, " ").Trim();

            // Remove accents
            text = text.RemoveAccent();

            // Strip trailing disambiguators (brackets/parentheses)
            text = TrailingBrackets.Replace(text, "");

            // Lowercase
            return text.ToLowerInvariant();
        }

        public IList<ReleaseInfo> ParseResponse(IndexerResponse response)
        {
            var torrentInfos = new List<ReleaseInfo>();
            var content = new HttpResponse<TidalSearchResponse>(response.HttpResponse).Content;

            var jsonResponse = JObject.Parse(content).ToObject<TidalSearchResponse>();

            var criteria = TidalRequestGenerator.Criteria;

            // If criteria are blank, bail early with an empty result.
            if (criteria == null || string.IsNullOrEmpty(criteria.ArtistQuery))
            {
                _logger.Error("Tidal parser is operating on a request that had no artist defined. Not great!");
                return torrentInfos.ToArray();
            }

            // Deduplicate Tidal results
            var allAlbums = jsonResponse.AlbumResults.Items
                .GroupBy(a => a.Id)
                .Select(g => g.First())
                .ToArray();
            var strategy = (TidalSearchStrategy)Settings.SearchStrategy;

            // Extract raw query strings from criteria
            var searchArtist = criteria.Artist.Name;
            string searchAlbumTitle = null;
            if (criteria is AlbumSearchCriteria albumCriteria)
            {
                searchAlbumTitle = albumCriteria.AlbumTitle;
            }

            _logger.Trace($"Tidal search '{searchArtist}' (album: {searchAlbumTitle ?? "(none)"}): {allAlbums.Length} albums fetched, strategy: {strategy}");

            // Filter
            IEnumerable<TidalSearchResponse.Album> matchedAlbums;
            if (strategy == TidalSearchStrategy.Comprehensive)
            {
                // Comprehensive mode: skip filtering, pass through all results
                matchedAlbums = allAlbums;
            }
            else if (searchAlbumTitle != null)
            {
                // Fast mode: artist + album filter
                matchedAlbums = allAlbums.Where(a =>
                    MatchArtist(searchArtist, a)
                    && MatchAlbum(searchAlbumTitle, a.Title));
            }
            else
            {
                // Fast mode: artist-only filter (should retrieve all albums/EPs by the same artist, used in artist page view search)
                matchedAlbums = allAlbums.Where(a => MatchArtist(searchArtist, a));
            }

            // Fast mode: if there are zero good matches, pray that Tidal gave relevant results and hand over the top 50.
            var matchedArray = matchedAlbums.ToArray();
            if (matchedArray.Length == 0 && strategy != TidalSearchStrategy.Comprehensive)
            {
                _logger.Trace("No matches found, falling back to top 50 raw results");
                matchedArray = allAlbums.Take(50).ToArray();
            }

            var releases = matchedArray.Select(result => ProcessAlbumResult(result)).ToArray();

            foreach (var task in releases)
            {
                torrentInfos.AddRange(task);
            }

            var processedAlbumIds = new HashSet<string>(matchedArray.Select(a => a.Id));
            var trackLimit = strategy == TidalSearchStrategy.Comprehensive ? 300 : 10;

            foreach (var track in jsonResponse.TrackResults.Items.Take(trackLimit))
            {
                if (!processedAlbumIds.Contains(track.Album.Id))
                {
                    var processTrackTask = ProcessTrackAlbumResultAsync(track);
                    processTrackTask.Wait();
                    if (processTrackTask.Result != null)
                        torrentInfos.AddRange(processTrackTask.Result);
                }
            }

            return torrentInfos
                .OrderByDescending(o => o.Size)
                .ToArray();
        }
        // Returns true if *any of the artists* of an album are a match for the search artist
        private bool MatchArtist(string searchArtist, TidalSearchResponse.Album album)
        {
            return album.Artists.Any(a =>
                NormalizeForMatch(a.Name).FuzzyMatch(NormalizeForMatch(searchArtist)) >= 0.7);
        }

        // Returns true if the album title string is a > 70% fuzzy match
        private bool MatchAlbum(string searchAlbum, string tidalTitle)
        {
            // Both sides normalized through the same function (brackets stripped, accents removed, etc.)
            var score = NormalizeForMatch(searchAlbum).FuzzyMatch(NormalizeForMatch(tidalTitle));

            if (score >= 0.7)
            {
                _logger.Trace($"Album match: {tidalTitle} (score {score:P2})");
                return true;
            }

            _logger.Trace($"Album mismatch: {tidalTitle} (score {score:P2})");
            return false;
        }

        private IEnumerable<ReleaseInfo> ProcessAlbumResult(TidalSearchResponse.Album result)
        {
            // determine available audio qualities
            List<AudioQuality> qualityList = new() { AudioQuality.LOW, AudioQuality.HIGH };

            if (result.MediaMetadata.Tags.Contains("HIRES_LOSSLESS"))
            {
                qualityList.Add(AudioQuality.LOSSLESS);
                qualityList.Add(AudioQuality.HI_RES_LOSSLESS);
            }
            else if (result.MediaMetadata.Tags.Contains("LOSSLESS"))
                qualityList.Add(AudioQuality.LOSSLESS);

            var quality = Enum.Parse<AudioQuality>(result.AudioQuality);
            return qualityList.Select(q => ToReleaseInfo(result, q));
        }

        private async Task<IEnumerable<ReleaseInfo>> ProcessTrackAlbumResultAsync(TidalSearchResponse.Track result)
        {
            try
            {
                var album = (await TidalAPI.Instance.Client.API.GetAlbum(result.Album.Id)).ToObject<TidalSearchResponse.Album>();

                // Re-apply matching for track results
                var criteria = TidalRequestGenerator.Criteria;
                if (criteria != null)
                {
                    if (!MatchArtist(criteria.Artist.Name, album))
                        return null;

                    if (criteria is AlbumSearchCriteria albumCriteria)
                    {
                        if (!MatchAlbum(albumCriteria.AlbumTitle, album.Title))
                            return null;
                    }
                }

                return ProcessAlbumResult(album);
            }
            catch (ResourceNotFoundException)
            {
                return null;
            }
        }

        private static ReleaseInfo ToReleaseInfo(TidalSearchResponse.Album x, AudioQuality bitrate)
        {
            var publishDate = DateTime.UtcNow;
            var year = 0;
            if (DateTime.TryParse(x.ReleaseDate, out var digitalReleaseDate))
            {
                publishDate = digitalReleaseDate;
                year = publishDate.Year;
            }
            else if (DateTime.TryParse(x.StreamStartDate, out var startStreamDate))
            {
                publishDate = startStreamDate;
                year = publishDate.Year;
            }

            var url = x.Url;

            var result = new ReleaseInfo
            {
                Guid = $"Tidal-{x.Id}-{bitrate}",
                Artist = x.Artists.First().Name,
                Album = x.Title,
                DownloadUrl = url,
                InfoUrl = url,
                PublishDate = publishDate,
                DownloadProtocol = nameof(TidalDownloadProtocol)
            };

            string format;
            switch (bitrate)
            {
                case AudioQuality.LOW:
                    result.Codec = "AAC";
                    result.Container = "96";
                    format = "AAC (M4A) 96kbps";
                    break;
                case AudioQuality.HIGH:
                    result.Codec = "AAC";
                    result.Container = "320";
                    format = "AAC (M4A) 320kbps";
                    break;
                case AudioQuality.LOSSLESS:
                    result.Codec = "FLAC";
                    result.Container = "Lossless";
                    format = "FLAC (M4A) Lossless";
                    break;
                case AudioQuality.HI_RES_LOSSLESS:
                    result.Codec = "FLAC";
                    result.Container = "24bit Lossless";
                    format = "FLAC (M4A) 24bit Lossless";
                    break;
                default:
                    throw new NotImplementedException();
            }

            // estimated sizing as tidal doesn't provide exact sizes in its api
            var bps = bitrate switch
            {
                AudioQuality.HI_RES_LOSSLESS => 1152000,
                AudioQuality.LOSSLESS => 176400,
                AudioQuality.HIGH => 40000,
                AudioQuality.LOW => 12000,
                _ => 40000
            };
            var size = x.Duration * bps;

            result.Size = size;
            result.Title = $"{x.Artists.First().Name} - {x.Title}";

            if (year > 0)
            {
                result.Title += $" ({year})";
            }

            if (x.Explicit)
            {
                result.Title += " [Explicit]";
            }

            result.Title += $" [{format}] [WEB]";

            return result;
        }
    }
}
