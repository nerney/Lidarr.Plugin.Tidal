using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;

namespace NzbDrone.Core.Download.Clients.Tidal
{
    public class TidalSettingsValidator : AbstractValidator<TidalSettings>
    {
        public TidalSettingsValidator()
        {
            RuleFor(x => x.DownloadPath).IsValidPath();
        }
    }

    public class TidalSettings : IProviderConfig
    {
        private static readonly TidalSettingsValidator Validator = new TidalSettingsValidator();

        [FieldDefinition(0, Label = "Download Path", Type = FieldType.Textbox)]
        public string DownloadPath { get; set; } = "";

        [FieldDefinition(1, Label = "Extract raw audio file from M4A", HelpText = "This will usually result in *.aac and *.flac files instead of *.m4a.", HelpTextWarning = "This requires FFMPEG and FFProbe to be available to Lidarr.", Type = FieldType.Checkbox)]
        public bool ExtractRawAudio { get; set; } = false;

        [FieldDefinition(2, Label = "Re-encode AAC into MP3", HelpText = "If used alongside Extract Raw Audio, it will result in just the *.mp3.", HelpTextWarning = "This requires FFMPEG and FFProbe to be available to Lidarr.", Type = FieldType.Checkbox)]
        public bool ReEncodeAACToMP3 { get; set; } = false;

        [FieldDefinition(3, Label = "Save Synced Lyrics", HelpText = "Saves synced lyrics to a separate .lrc file if available. Requires .lrc to be allowed under Import Extra Files.", Type = FieldType.Checkbox)]
        public bool SaveSyncedLyrics { get; set; } = false;

        [FieldDefinition(4, Label = "Use LRCLIB as Backup Lyric Provider", HelpText = "If Tidal does not have plain or synced lyrics for a track, the plugin will attempt to get them from LRCLIB.", Type = FieldType.Checkbox)]
        public bool UseLRCLIB { get; set; } = false;

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
