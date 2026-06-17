# Item Helper — a Guild Wars 2 (Blish HUD) module

**Item Helper** is a [Blish HUD](https://blishhud.com) module that helps new
(and not-so-new) players understand what their items are *for*. Copy an item's
chat link in‑game and Item Helper decodes it and explains the item in plain
English — focusing on the categories that confuse people the most:

- **Consumables** (food, utility items, transmutation/unlock items, …)
- **Crafting materials** (and what they're used to craft)
- **Containers** (what to do with them)
- **Gizmos** (keys, reusable tools, summoned vendors, …)

Armor, weapons, trinkets and junk are intentionally treated as "not covered"
because their purpose is usually self‑explanatory.

## Why you copy a link instead of hovering

Guild Wars 2 does **not** expose your inventory to add‑ons. No supported
add‑on (Blish HUD or otherwise) can see what item you're hovering, where your
bag slots are, or draw a badge onto an item. That door is closed by the game.

What the game *does* give us is **chat links**. Item Helper uses them, which is
both seamless and 100% accurate (it reads the exact item id — no name
guessing):

1. In GW2, open chat and **shift‑click** an item to drop its link in.
2. Select that link and press **Ctrl+C** to copy it.
3. Item Helper sees it and shows what the item is for.

There are three ways to trigger a lookup:

| Trigger | How |
| --- | --- |
| **Auto‑watch clipboard** (default on) | Just copy an item link — the window pops up automatically. Toggle in settings. |
| **Hotkey** | Default `Ctrl+Shift+I`. Rebind in module settings. |
| **Corner icon / paste box** | Click the magnifying‑glass corner icon to open the window, then click "Look up copied item" or paste a chat link / type an item id. |

## Building

This is a standard Blish HUD module (C#, .NET Framework 4.8). You need the
.NET Framework 4.8 developer pack (Windows, or via Visual Studio / Build Tools).

```bash
dotnet restore
dotnet build -c Release
```

The `Blish HUD` NuGet package's build targets produce a packaged module at:

```
ItemHelper/bin/Release/Gw2.ItemHelper.bhm
```

> Tip: if you point the build output at
> `Documents\Guild Wars 2\addons\blishhud\modules\`, Blish HUD will pick the
> module up the next time it loads.

## Installing / running

1. Install and run [Blish HUD](https://blishhud.com) (overlay on top of GW2).
2. Drop the built `Gw2.ItemHelper.bhm` into your Blish HUD `modules` folder
   (or use the in‑app module repo once published).
3. Enable **Item Helper** in Blish HUD's module list.
4. A magnifying‑glass icon appears in the top‑left corner — click it, or just
   copy an item chat link in game.

No API key is required: item, consumable and recipe data all come from the
public endpoints of the official GW2 API (`api.guildwars2.com`).

## Project layout

```
ItemHelper/
  manifest.json              Blish HUD module manifest
  ItemHelper.csproj          net48 project; references Blish HUD + Gw2Sharp
  Module.cs                  Entry point: corner icon, settings, hotkey, clipboard watch
  Services/
    ItemLinkParser.cs        Chat link / item id -> item id
    ItemExplainer.cs         Item data -> beginner-friendly explanation
  UI/
    ItemHelperWindow.cs      The window (lookup controls + explanation panel)
  ref/
    icon.png                 Corner icon (magnifying glass)
    background.png           Window background
tools/
  gen_assets.py              Regenerates the PNG assets above
```

## Regenerating the image assets

The icon and window background are generated with a tiny pure‑Python PNG
writer (no Pillow needed):

```bash
python3 tools/gen_assets.py
```

## Status

`v0.1.0` — first working version. Possible next steps:

- Name search (would require a downloadable item index).
- Richer container/recipe details and reward previews.
- Localization of the explanation text.
