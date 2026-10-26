# grassfinder (C#)

Finds Minecraft world coordinates from reconstructed grass plant offsets (for example, to locate where a screenshot was taken) by brute-force search.

This is a C# reimplementation of [humhue/grassfinder](https://github.com/humhue/grassfinder), an optimized Rust fork (wgpu GPU + Rayon CPU) of [polymetric/grassfinder](https://github.com/polymetric/grassfinder). It adds rotation search (`--rot`), so a pattern recorded in a different orientation can still be found.

Every grass plant is drawn with a small offset (0–15 on each axis) that is a fixed function of its world position. If you have reconstructed those offsets for several plants, this program tries every candidate position in a search area and prints the positions whose computed offsets match yours (average error below a threshold).

## Requirements

- .NET SDK (6.0 or newer recommended)

## Run

Clone the repository, put your `offsets.txt` next to `grassfinder.csproj`, then:

```
dotnet run -c Release
dotnet run -c Release -- --rot=All
dotnet run -c Release -- --post-1.8
```

Use `-c Release`: it is much faster than a Debug build. Options go after the `--`.

## Input: `offsets.txt`

One plant per line, six whitespace-separated integers:

```
x y z offsetX offsetY offsetZ
```

- `x y z`: observed coordinates of the plant's block
- `offsetX offsetY offsetZ`: observed offset of the plant, each 0–15
- The **first line is the reference plant**. All other plants are taken relative to it.
- Blank lines are skipped. Comments and header lines are not supported.

The offsets can be acquired with polymetric's [mcah-manualgrass](https://github.com/polymetric/mcah-manualgrass) mod.

Example (placeholder numbers, replace with your own):

```
100 64 200 3 7 12
101 64 200 9 2 5
100 64 203 14 0 8
```

## Options

| Option | Meaning |
| --- | --- |
| `--post-1.8` | Minecraft 1.8+: use the newer hash that ignores Y. Y is not searched in this mode. Default is the older hash that includes Y. |
| `--rot=<mode>` | Rotate the recorded pattern before searching: `None_0` (default), `Clockwise_90`, `Rotate_180`, `Clockwise_270`, or `All` to try all four. Case-insensitive. |

An unrecognized `--rot=` value is silently ignored and `None_0` is used, so check the "Rotation mode" line printed at startup.

Rotation is around the first plant, clockwise as seen from above (north up, east right). Use `--rot=All` if you are not sure how your screenshot is oriented.

## Output

One line per candidate:

```
[None_0]        -123    64   456    avg delta: 1.273    delta:   100     0   -50
```

- `[None_0]`: the rotation being tested
- first three numbers: candidate world position `x y z` of the first (reference) plant
- `avg delta`: sum of `|dx|+|dy|+|dz|` over all plants, divided by the plant count. Lower is better, 0 is an exact match.
- `delta`: candidate position minus the first plant's observed position

With `--post-1.8` the printed Y is always 0, so ignore the Y in the position and in `delta`.

## Settings

These are at the top of `Main` in `grassfinder.cs`. Edit them and rebuild.

| Setting | Default | Meaning |
| --- | --- | --- |
| `spawnrange` | `2000` | X and Z are searched in `[-spawnrange, spawnrange)` |
| `yrange` | `(63, 85)` | Y range (upper bound exclusive), older hash only |
| `maxAvgDelta` | `2.0` | Report candidates with average delta strictly below this |
| `deltaMode` | `true` | Set to `false` to report exact matches only |

A higher `maxAvgDelta` prints more candidates and is slower, because fewer candidates are rejected early.

## Credits and license

- [polymetric/grassfinder](https://github.com/polymetric/grassfinder) by polymetric: the original grassfinder (MIT).
- [humhue/grassfinder](https://github.com/humhue/grassfinder): optimized Rust fork of the original (MIT). This project reimplements it in C#.
- [polymetric/mcah-manualgrass](https://github.com/polymetric/mcah-manualgrass): mod for acquiring the grass offsets.

Licensed under the MIT license, see [LICENSE](LICENSE). It keeps the copyright notice of the original authors.
