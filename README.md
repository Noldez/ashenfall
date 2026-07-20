# Ashenfall

A persistent RPG/MMO game mode for Counter-Strike 2, built on [ModSharp](https://github.com/Kxnrl/modsharp-public).

Players join an always-on world instead of matches: a town hub, level-gated monster zones, party dungeons, world bosses, an auction house, guilds and gear enhancement. Progression is stored in a database and persists across sessions and map changes.

## Status

Design phase complete; implementation starting with server infrastructure and a vertical slice.

## Stack

- ModSharp (C#/.NET plugin framework for Source 2)
- MySQL/MariaDB for persistent state
- Source 2 Hammer and CS2 Workshop Tools for the world map and assets

## Repository layout

```
src/     plugin source (coming with phase 1)
configs/ data-driven content: monsters, items, loot tables (coming with phase 1)
```

## Configuration and secrets

The repository is public. Credentials and infrastructure details are never committed: runtime configuration lives in untracked `*.secrets.json` / `.env` files, with `*.example` templates committed instead.

## License

AGPL-3.0, matching the ModSharp framework this project builds on.
