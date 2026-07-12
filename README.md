# FSK Rating Updater

A Jellyfin plugin that standardizes the age ratings (`OfficialRating`) of your entire library to
the canonical German FSK format — **FSK-0 / FSK-6 / FSK-12 / FSK-16 / FSK-18** — and fills in
missing or foreign ratings with the official German certification from TMDb.

Why the hyphenated `FSK-12`? Jellyfin's built-in German rating table (`de.csv`) only recognizes
the spellings `FSK-n` / `DE-n` for parental controls. `FSK 12` (with a space) is **not** matched —
which is exactly what this plugin fixes.

## How it works

A scheduled task (default: daily at 3:00 AM, can be run manually anytime under
**Dashboard → Scheduled Tasks → FSK Rating Updater**) processes movies and series in three steps:

1. **Normalization** — already-German values are unified without any API call:
   `FSK 12`, `fsk12`, `DE-12`, `12`, `ab 12 Jahren` → `FSK-12` · `o.A.` → `FSK-0` ·
   `Keine Jugendfreigabe` → `FSK-18`.
2. **TMDb lookup** — items with a missing or foreign rating (e.g. `PG-13`) are resolved via TMDb's
   German certification (movies: DE release dates, series: DE content ratings). A missing TMDb ID
   is resolved through the IMDb ID.
3. **Fallback** (configurable) — when no German rating can be determined:
   keep unchanged (default, safe) · map from the foreign rating (`PG-13`→`FSK-12`, `R`→`FSK-16`,
   approximate) · clear the rating.

Only the `OfficialRating` field is modified — no other metadata is touched.

## Configuration

| Setting | Default | Description |
|---|---|---|
| TMDb API Key | empty | Free key from [themoviedb.org](https://www.themoviedb.org/settings/api). Empty = normalization-only mode, no online lookups. |
| Dry run | **on** | Only log proposed changes (Dashboard → Logs, lines tagged `[DryRun]`) without writing. Review, then turn off. |
| Update movies / TV series | on | Which media types are processed. Seasons and episodes inherit the series rating. |
| Re-fetch existing FSK | off | On: valid FSK values are re-fetched from TMDb too (overwrites manual corrections). |
| Fallback | Keep unchanged | Behavior when no German rating can be determined (see above). |

## Requirements

- Jellyfin **10.11.x**
- A free TMDb API key (optional — without it the plugin still normalizes existing ratings)

## Installation

### Via plugin repository

1. **Dashboard → Plugins → Repositories → +** and add:
   `https://raw.githubusercontent.com/ch-bauer/jellyfin-plugin-fsk-rating-updater/main/manifest.json`
2. Install **FSK Rating Updater** from the catalog and restart Jellyfin.

### Manual

1. Download the zip from the [latest release](https://github.com/ch-bauer/jellyfin-plugin-fsk-rating-updater/releases/latest).
2. Extract it into `<jellyfin config>/plugins/FskRatings/`
   (Windows: `C:\ProgramData\Jellyfin\Server\plugins\FskRatings\`).
3. Restart Jellyfin.

## Recommended first run

1. Enter your TMDb key, leave **Dry run** enabled, save.
2. Run the task manually (Dashboard → Scheduled Tasks).
3. Check the log: every proposed change appears as `[DryRun] Title: 'FSK 12' -> 'FSK-12'`,
   followed by a summary (changed / already correct / unresolved).
4. Disable dry run and run the task again.

## Development

```
dotnet build     # compile
dotnet test      # unit tests (normalization + foreign-rating mapping)
dotnet publish src/Jellyfin.Plugin.FskRatings -c Release -o publish
```

Releases are created by pushing a tag (`v1.0.0.0`) — see `.github/workflows/release.yml`.

Inspired by [jellyfin-imdb-rating-updater](https://github.com/voc0der/jellyfin-imdb-rating-updater).

## License

[MIT](LICENSE)
