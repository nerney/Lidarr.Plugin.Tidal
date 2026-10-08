using System;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download.Clients.Tidal;
using NzbDrone.Core.Parser;
using NzbDrone.Plugin.Tidal;

namespace NzbDrone.Core.Indexers.Tidal
{
    public class Tidal : HttpIndexerBase<TidalIndexerSettings>
    {
        public override string Name => "Tidal";
        public override string Protocol => nameof(TidalDownloadProtocol);
        public override bool SupportsRss => false;
        public override bool SupportsSearch => true;
        public override int PageSize => 100;
        public override TimeSpan RateLimit => new TimeSpan(0);

        private readonly ITidalProxy _tidalProxy;

        public Tidal(ITidalProxy tidalProxy,
            IHttpClient httpClient,
            IIndexerStatusService indexerStatusService,
            IConfigService configService,
            IParsingService parsingService,
            Logger logger)
            : base(httpClient, indexerStatusService, configService, parsingService, logger)
        {
            _tidalProxy = tidalProxy;
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            if (!string.IsNullOrEmpty(Settings.ConfigPath))
            {
                TidalAPI.Initialize(Settings.ConfigPath, _logger);
                bool success = TidalAPI.Instance.Client.Login(Settings.RedirectUrl).Result;
                if (!success)
                {
                    return null;
                }
            }
            else
                return null;

            return new TidalRequestGenerator()
            {
                Settings = Settings,
                Logger = _logger
            };
        }

        public override IParseIndexerResponse GetParser()
        {
            return new TidalParser()
            {
                Settings = Settings
            };
        }
    }
}
