using System;
using System.Collections.Generic;
using System.Threading;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Plugin.Tidal;

namespace NzbDrone.Core.Indexers.Tidal
{
    public class TidalRequestGenerator : IIndexerRequestGenerator
    {
        private const int MaxPages = 1;

        // In the *arrs it's not easy to share data between an indexer's request generator and the parser that runs on the
        // result. The only data that survives is the URL, but the query is already an arbitrary string of the artist and
        // album name smushed together. Store the artist and album separately so we can use them for culling junk results
        // later in the parser. AsyncLocal is thread-safe in case of parallel requests.
        private static readonly AsyncLocal<string> SearchArtist = new();
        private static readonly AsyncLocal<string> SearchAlbum = new();

        public static string GetSearchArtist() => SearchArtist.Value;
        public static string GetSearchAlbum() => SearchAlbum.Value;

        public TidalIndexerSettings Settings { get; set; }
        public Logger Logger { get; set; }

        public virtual IndexerPageableRequestChain GetRecentRequests()
        {
            // Lidarr's Indexer dialog "test" button runs GetRecentRequests and expects at least one result for the test to pass.
            //
            // This plugin doesn't support RSS-style "recent" data, so this dummy implementation is just to satisfy the test.
            SearchArtist.Value = "Korn";
            SearchAlbum.Value = "Follow the Leader";

            var pageableRequests = new IndexerPageableRequestChain();
            pageableRequests.Add(GetRequests("Korn Follow the Leader"));

            return pageableRequests;
        }

        public IndexerPageableRequestChain GetSearchRequests(AlbumSearchCriteria searchCriteria)
        {
            SearchArtist.Value = searchCriteria.ArtistQuery;
            SearchAlbum.Value = searchCriteria.AlbumQuery;

            var chain = new IndexerPageableRequestChain();
            chain.AddTier(GetRequests($"{searchCriteria.ArtistQuery} {searchCriteria.AlbumQuery}"));

            return chain;
        }

        public IndexerPageableRequestChain GetSearchRequests(ArtistSearchCriteria searchCriteria)
        {
            SearchArtist.Value = searchCriteria.ArtistQuery;
            SearchAlbum.Value = "";

            var chain = new IndexerPageableRequestChain();
            chain.AddTier(GetRequests(searchCriteria.ArtistQuery));

            return chain;
        }

        private IEnumerable<IndexerRequest> GetRequests(string searchParameters)
        {
            if (DateTime.UtcNow > TidalAPI.Instance.Client.ActiveUser.ExpirationDate)
            {
                // ensure we always have an accurate expiration date
                if (TidalAPI.Instance.Client.ActiveUser.ExpirationDate == DateTime.MinValue)
                    TidalAPI.Instance.Client.ForceRefreshToken().Wait();
                else
                    TidalAPI.Instance.Client.IsLoggedIn().Wait(); // calls an internal function which handles refreshes if needed
            }

            var pageSize = (TidalSearchStrategy)Settings.SearchStrategy switch
            {
                TidalSearchStrategy.Fast => 100,
                TidalSearchStrategy.Comprehensive => 300,
                _ => 100
            };

            for (var page = 0; page < MaxPages; page++)
            {
                var data = new Dictionary<string, string>()
                {
                    ["query"] = searchParameters,
                    ["limit"] = $"{pageSize}",
                    ["types"] = "albums,tracks",
                    ["offset"] = $"{page * pageSize}",
                };

                var url = TidalAPI.Instance!.GetAPIUrl("search", data);
                var req = new IndexerRequest(url, HttpAccept.Json);
                req.HttpRequest.Method = System.Net.Http.HttpMethod.Get;
                req.HttpRequest.Headers.Add("Authorization", $"{TidalAPI.Instance.Client.ActiveUser.TokenType} {TidalAPI.Instance.Client.ActiveUser.AccessToken}");
                yield return req;
            }
        }
    }
}
