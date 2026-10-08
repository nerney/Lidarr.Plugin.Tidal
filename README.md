# Tidal for Lidarr
This plugin provides a Tidal indexer and downloader client for Lidarr.

## Installation
This requires your Lidarr setup to be using the `plugins` branch. My docker-compose is setup like the following.
```yml
  lidarr:
    image: ghcr.io/hotio/lidarr:pr-plugins
    container_name: lidarr
    environment:
      - PUID:100
      - PGID:1001
      - TZ:Etc/UTC
    volumes:
      - /path/to/config/:/config
      - /path/to/downloads/:/downloads
      - /path/to/music:/music
    ports:
      - 8686:8686
    restart: unless-stopped
```

1. In Lidarr, go to `System -> Plugins`, paste `https://github.com/TrevTV/Lidarr.Plugin.Tidal` into the GitHub URL box, and press Install.
2. Go into the Indexer settings and press Add. In the modal, choose `Tidal` (under Other at the bottom).
3. Enter a path to use to store user data, press Test, it will error, press Cancel.
4. Open the Lidarr log under `System -> Log Files` and download `lidarr.txt`.
5. In the file, search for `Tidal URL; use this to login:` and open the listed URL in a new tab.
6. In the new tab, log in to Tidal, then press `Yes, continue`. It will then bring you to a page labeled "Oops." Copy the new URL for that tab (something like `https://tidal.com/android/login/auth?code=[VERY LONG CODE]`).
   - Do NOT share this URL with people as it grants people access to your account.
   - Once you press, `Yes, continue`, the URL from the Lidarr log can not be used again. If you need to sign in again, make sure to use the newest URL from the plugin.
7. Go back to the Indexer settings and pick `Tidal` again.
8. Enter a path to use to store user data and paste the copied Tidal URL into the `Redirect Url` option. Then press Save.
9.  Go into the Download Client settings and press Add. In the modal, choose `Tidal` (under Other at the bottom).
10. Put the path you want to download tracks to and fill out the other settings to your choosing.
   - If you want `.lrc` files to be saved, go into the Media Management settings and enable Import Extra Files and add `lrc` to the list.
11. Go into the Profile settings and find the Delay Profiles. On each (by default there is only one), click the wrench on the right and toggle Tidal on.
12. Optional: To prevent Lidarr from downloading all track files into the base artist folder rather than into their own separate album folder, go into the Media Management settings and enable Rename Tracks. You can change the formats to your liking, but it helps to let each album have their own folder.

## Licensing
All of these libraries have been merged into the final plugin assembly due to (what I believe is) a bug in Lidarr's plugin system.
- [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) is licensed under the MIT license. See [LICENSE](https://github.com/JamesNK/Newtonsoft.Json/blob/master/LICENSE.md) for the full license.
- [TagLibSharp](https://github.com/mono/taglib-sharp) is licensed under the LGPL-2.1 license. See [COPYING](https://github.com/mono/taglib-sharp/blob/main/COPYING) for the full license.
- [TidalSharp](https://github.com/TrevTV/TidalSharp) is licensed under the GPL-3.0 license. See [LICENSE](https://github.com/TrevTV/TidalSharp/blob/main/LICENSE) for the full license.