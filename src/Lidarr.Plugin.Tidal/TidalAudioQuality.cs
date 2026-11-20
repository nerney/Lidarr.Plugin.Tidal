using System;
using TidalSharp.Data;

namespace NzbDrone.Plugin.Tidal
{
    internal static class TidalAudioQuality
    {
        public static string GetFormat(AudioQuality quality)
        {
            return quality switch
            {
                AudioQuality.LOW => "AAC (M4A) 96kbps",
                AudioQuality.HIGH => "AAC (M4A) 320kbps",
                AudioQuality.LOSSLESS => "FLAC (M4A) Lossless",
                AudioQuality.HI_RES_LOSSLESS => "FLAC (M4A) 24bit Lossless",
                _ => throw new NotImplementedException(),
            };
        }
    }
}
