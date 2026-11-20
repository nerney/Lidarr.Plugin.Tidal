# Tidal for Lidarr

A Tidal indexer and download client for Lidarr.

This repository is a fork of [TrevTV/Lidarr.Plugin.Tidal](https://github.com/TrevTV/Lidarr.Plugin.Tidal), based on the latest upstream code and continuing development here. It incorporates [putnam's search performance improvements](https://github.com/TrevTV/Lidarr.Plugin.Tidal/pull/58) and updates the plugin to .NET 10.

## Requirements

- A plugin-enabled Lidarr installation compatible with .NET 10. The current release targets Lidarr 4.0.0.5222 or newer.
- A Tidal account.
- FFmpeg and FFprobe available to Lidarr if you enable audio conversion.

## Installation

1. In Lidarr, open **System > Plugins**, enter `https://github.com/nerney/Lidarr.Plugin.Tidal`, and select **Install**. Restart Lidarr when prompted.
2. Under **Settings > Indexers**, add **Tidal**. Enter a writable **Config Path** and select **Test** (it may fail), then cancel, refresh the page, and reopen the Tidal indexer settings. This initializes the login URL.
3. Open the **Tidal URL**, sign in, and approve access. The redirect may show an "Oops" page; copy its complete URL.
4. Paste that URL into **Redirect Url**, enter the same **Config Path**, and save the indexer.
5. Under **Settings > Download Clients**, add **Tidal**, set a writable **Download Path**, and save.
6. Under **Settings > Profiles > Delay Profiles**, edit each applicable profile and enable Tidal.

Keep the redirect URL private: it grants access to your account. Redirect URLs cannot be reused; use a newly generated Tidal URL if you need to sign in again. The config directory stores account information, so keep it private and persistent.

If running Lidarr in Docker, the config and download paths must be paths **inside the container**, backed by writable, persistent volume mounts. Ensure your music library is also accessible to Lidarr.

## Options

- **Search Strategy:** Fast reduces API calls; Comprehensive searches more broadly but can be slower.
- **Lyrics:** Enable **Save Synced Lyrics** to save available `.lrc` files. In Lidarr's media management settings, enable **Import Extra Files** and add `lrc` to the extensions. LRCLIB can optionally provide fallback lyrics.
- **Audio conversion:** Enable FLAC extraction or AAC-to-MP3 conversion only when FFmpeg and FFprobe are installed and accessible to Lidarr.
- **Download Delay:** Add a delay between tracks to help avoid Tidal rate limits.

Search result sizes are estimates because Tidal does not provide exact sizes in its search API.
