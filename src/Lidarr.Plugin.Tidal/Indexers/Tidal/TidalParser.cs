using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Common.Instrumentation;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Plugin.Tidal;
using TidalSharp.Data;
using TidalSharp.Exceptions;

namespace NzbDrone.Core.Indexers.Tidal
{
    public class TidalParser : IParseIndexerResponse
    {
        private readonly Logger _logger;

        public TidalIndexerSettings Settings { get; set; }

        public TidalParser()
        {
            _logger = NzbDroneLogger.GetLogger(this);
        }

        public IList<ReleaseInfo> ParseResponse(IndexerResponse response)
        {
            var torrentInfos = new List<ReleaseInfo>();
            var content = new HttpResponse<TidalSearchResponse>(response.HttpResponse).Content;

            var jsonResponse = JObject.Parse(content).ToObject<TidalSearchResponse>();

            var searchArtist = TidalRequestGenerator.GetSearchArtist();
            var searchAlbum = TidalRequestGenerator.GetSearchAlbum();
            var strategy = (TidalSearchStrategy)Settings.SearchStrategy;

            var albums = strategy == TidalSearchStrategy.Fast
                ? jsonResponse.AlbumResults.Items.Where(a => IsRelevantMatch(a, searchArtist, searchAlbum)).ToArray()
                : jsonResponse.AlbumResults.Items;

            var relevantAlbums = albums
                .GroupBy(a => a.Id)
                .Select(g => g.First())
                .ToArray();

            var releases = relevantAlbums.Select(result => ProcessAlbumResult(result)).ToArray();

            foreach (var task in releases)
            {
                torrentInfos.AddRange(task);
            }

            var processedAlbumIds = new HashSet<string>(relevantAlbums.Select(a => a.Id));
            var trackLimit = strategy switch
            {
                TidalSearchStrategy.Fast => 10,
                TidalSearchStrategy.Comprehensive => 300,
                _ => 10
            };

            _logger.Trace($"Tidal search '{searchArtist} - {searchAlbum}': {jsonResponse.AlbumResults.Items.Length} albums fetched, {relevantAlbums.Length} relevant, {jsonResponse.TrackResults.Items.Length} tracks (limit {trackLimit})");

            foreach (var track in jsonResponse.TrackResults.Items.Take(trackLimit))
            {
                if (!processedAlbumIds.Contains(track.Album.Id))
                {
                    var processTrackTask = ProcessTrackAlbumResultAsync(track, strategy);
                    processTrackTask.Wait();
                    if (processTrackTask.Result != null)
                        torrentInfos.AddRange(processTrackTask.Result);
                }
            }

            return torrentInfos
                .OrderByDescending(o => o.Size)
                .ToArray();
        }

        private bool IsRelevantMatch(TidalSearchResponse.Album album, string searchArtist, string searchAlbum)
        {
            var albumArtist = album.Artists.First().Name;

            if (string.IsNullOrEmpty(searchArtist))
            {
                _logger.Trace($"Filtered album (no search artist): {albumArtist} - {album.Title}");
                return false;
            }

            var artistMatch = album.Artists.Any(a =>
                a.Name.IndexOf(searchArtist, StringComparison.OrdinalIgnoreCase) >= 0 ||
                searchArtist.IndexOf(a.Name, StringComparison.OrdinalIgnoreCase) >= 0);

            if (!artistMatch)
            {
                _logger.Trace($"Filtered album (artist mismatch): {albumArtist} - {album.Title} (searching for '{searchArtist}')");
                return false;
            }

            if (string.IsNullOrEmpty(searchAlbum))
                return true;

            var albumWords = searchAlbum.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 3)
                .Select(w => w.ToLowerInvariant())
                .ToArray();

            if (albumWords.Length == 0)
                return true;

            var albumTitle = album.Title.ToLowerInvariant();
            var matches = albumWords.Any(word => albumTitle.Contains(word));

            if (!matches)
            {
                _logger.Trace($"Filtered album (album mismatch): {albumArtist} - {album.Title} (searching for '{searchAlbum}')");
            }

            return matches;
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

        private async Task<IEnumerable<ReleaseInfo>> ProcessTrackAlbumResultAsync(TidalSearchResponse.Track result, TidalSearchStrategy strategy)
        {
            try
            {
                var album = (await TidalAPI.Instance.Client.API.GetAlbum(result.Album.Id)).ToObject<TidalSearchResponse.Album>();
                if (strategy == TidalSearchStrategy.Fast && !IsRelevantMatch(album, TidalRequestGenerator.GetSearchArtist(), TidalRequestGenerator.GetSearchAlbum()))
                    return null;
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
                year = startStreamDate.Year;
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
