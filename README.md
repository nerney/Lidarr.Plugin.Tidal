# Tidal Plugin for Lidarr

This plugin adds Tidal as an indexer and download client in Lidarr. It searches
Tidal for releases and downloads tracks for import into your library.

## Project lineage

This repository is nerney's continuation of the original
[TrevTV/Lidarr.Plugin.Tidal](https://github.com/TrevTV/Lidarr.Plugin.Tidal)
project. This continuation was motivated in part by
[PR #58](https://github.com/TrevTV/Lidarr.Plugin.Tidal/pull/58), contributed by
putnam, which significantly improved the plugin's performance. This repository
adopts that work and provides a home for continued maintenance.

The Tidal API integration uses
[TidalSharp](https://github.com/TrevTV/TidalSharp), also created by TrevTV.
TidalSharp's source is included in this repository under `src/TidalSharp` and
built with the plugin.

## Requirements

- Lidarr running a build with plugin support. See the
  [Lidarr wiki](https://wiki.servarr.com/lidarr) for Lidarr documentation.
- Lidarr v3.0.0.4855 or later.
- FFmpeg available to Lidarr if you enable the plugin's FFmpeg conversion
  options.

## Installation

1. In Lidarr, open **System → Plugins**.
2. Enter `https://github.com/nerney/Lidarr.Plugin.Tidal` as the GitHub URL and
   select **Install**.
3. After installation, add Tidal under **Settings → Indexers → Add**. Choose
   **Tidal** under **Other**.
4. Enter a path for Tidal user data, select **Test**, then **Cancel** when the
   initial test fails.
5. Refresh Lidarr and open the Tidal indexer setup again. A **Tidal URL** should
   now be available. Open it in a browser, log in to Tidal, and select
   **Yes, continue**.
6. The browser will land on an “Oops” page. Copy the URL from the address bar;
   it should resemble
   `https://tidal.com/android/login/auth?code=...`.
   - Do not share this URL: it can grant access to your Tidal account.
   - Redirect URLs are single-use. To sign in again, generate a new Tidal URL
     from the indexer settings.
7. Return to the indexer settings, enter the user-data path and paste the copied
   URL into **Redirect Url**, then save.
8. Add Tidal under **Settings → Download Clients → Add**. Choose **Tidal** under
   **Other**, set the download path, and configure the remaining options.
   - To save `.lrc` lyrics files, enable **Import Extra Files** in Media
     Management and add `lrc` to the allowed extensions.
   - Enable FFmpeg options only if FFmpeg is available to Lidarr.
9. In **Settings → Profiles → Delay Profiles**, edit each profile and enable
   Tidal.
10. Optionally, enable **Rename Tracks** in Media Management to have Lidarr
    organize tracks into album folders. Configure the naming formats as
    desired.

## Docker and FFmpeg

For Docker installations, Hotio's
[Lidarr images](https://hotio.dev/containers/lidarr/) with the `nightly` or
`testing` tags are recommended. The
[LinuxServer Lidarr image](https://docs.linuxserver.io/images/docker-lidarr/)
is another option; use its `develop` or `nightly` tag. Check each provider's
documentation for current image and tag details.

For example, this Docker Compose service uses the Hotio `nightly` image:

```yaml
services:
  lidarr:
    image: ghcr.io/hotio/lidarr:nightly
    container_name: lidarr
    environment:
      PUID: 1000
      PGID: 1000
      TZ: America/Chicago
    volumes:
      - /opt/lidarr:/config # application data
      - /data:/data # mount containing your download and library directories
    ports:
      - 8686:8686
    restart: unless-stopped
```

### Adding FFmpeg to the container

If you enable the plugin's FFmpeg options, install FFmpeg in the container.
Here are two ways to do that:

1. **Install it with a Compose `post_start` hook:**

   ```yaml
   post_start:
     - user: root
       command: apk add --no-cache ffmpeg
   ```

2. **Build a custom image** by adding this to your Compose service. Use the same
   base image and tag as your service:

   ```yaml
   pull_policy: never
   build:
     context: .
     dockerfile_inline: |
       FROM ghcr.io/hotio/lidarr:nightly
       RUN apk add --no-cache ffmpeg
   ```

## Known limitations

- Search results report an estimated file size rather than the actual size.
- Tidal user access tokens are stored in the plugin's configured data folder.
- **AAC bitrate labels may not match the advertised bitrate exactly.** For
  example, a track advertised as 320 kbps may be about 315 kbps, so Lidarr can
  identify it as `AAC-VBR` instead of `AAC-320`. To target a specific AAC
  bitrate, put `AAC-VBR` in the same quality tier as the desired quality, then
  use a release profile for the Tidal indexer to block the other bitrate
  substring. For example, to target 320 kbps, put `AAC-320` and `AAC-VBR` in the
  same tier and block `96kbps`. To target 96 kbps instead, use the same tier
  setup with the 96 kbps AAC quality and `AAC-VBR`, and block `320kbps`.

## Attribution and licenses

This project continues the work of TrevTV's
[Lidarr.Plugin.Tidal](https://github.com/TrevTV/Lidarr.Plugin.Tidal) and uses
[TidalSharp](https://github.com/TrevTV/TidalSharp), whose source is included
under `src/TidalSharp`. The following libraries are merged into the plugin
assembly because of a limitation in Lidarr's plugin system:

- [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) — MIT license;
  see its [license](https://github.com/JamesNK/Newtonsoft.Json/blob/master/LICENSE.md).
- [TagLibSharp](https://github.com/mono/taglib-sharp) — LGPL-2.1 license; see
  its [license](https://github.com/mono/taglib-sharp/blob/main/COPYING).
- [TidalSharp](https://github.com/TrevTV/TidalSharp) — GPL-3.0 license; see
  its [license](https://github.com/TrevTV/TidalSharp/blob/main/LICENSE).

## Support

For help using the plugin, join the Servarr Discord and ask in
[`#lidarr-plugins`](https://discord.com/invite/8Dbsx35rrx). For bug reports or
feature requests, open an
[issue](https://github.com/nerney/Lidarr.Plugin.Tidal/issues) in this repository.
