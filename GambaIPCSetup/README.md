# GambaWhere IPC v2 integration

This guide is for plugin developers who want their game to appear inside GambaWhere: an opened
window prompt plus live "automatic rules" that a host can pick up without typing anything by hand.

GambaWhere v2 is a push model. Your plugin calls two public gates that GambaWhere provides. You do
not need to expose any gates of your own, and GambaWhere needs no code changes to support a new game.

## The two gates

| Gate | Signature | Purpose |
| --- | --- | --- |
| `GambaWhere.WindowOpened` | `Func<string pluginName, string category, bool accepted>` | Tells GambaWhere your window just opened so it can offer to start a session. |
| `GambaWhere.SubmitRules` | `Func<string pluginName, string category, object payload, bool accepted>` | Pushes your live game settings as rules. |

Both return `true` when accepted and `false` when rejected. A missing gate (GambaWhere not installed,
or an older version) throws, so wrap every call in try/catch and simply try again on your next tick.

Subscribe with `pluginInterface.GetIpcSubscriber<...>(gateName)` and call `InvokeFunc(...)`.

## Categories

The `category` argument must match one of GambaWhere's categories exactly (case sensitive):

- `Bingo`
- `Blackjack`
- `Chocobo Racing`
- `Mini Games`
- `Poker`
- `Roulette`
- `Scratchcards`
- `Spin the Wheel`

An unknown category is rejected (the call returns `false`).

## Plugin name

`pluginName` is the label shown to the host. It is trimmed, must be 1 to 32 characters, and must not
contain URLs or HTML (those submissions are rejected).

If your game already ships inside GambaWhere's built in catalogue, send the exact same name as that
catalogue entry's companion plugin name. GambaWhere then replaces the old entry with your live one
instead of showing both. If you are a brand new game, any valid name is fine.

## Window opened

Call `GambaWhere.WindowOpened` the instant your window opens, in real time. GambaWhere shows your
plugin in the host's rules list straight away (as "No Session Found" until rules arrive) and prints a
one time chat prompt offering to start a session. Repeat opens within two seconds are coalesced, so
you do not need to throttle it yourself.

See `GambaWhereWindowOpened.cs` for a drop in class.

## Rules

Build a payload and push it through `GambaWhere.SubmitRules` roughly every 30 seconds. GambaWhere keeps
your last rules (and your listing) for 45 seconds after the most recent successful push, then reverts to
"No Session Found". The 15 second grace means an occasional late push will not make the rules flicker.

If you have no active session, simply do not push (or push nothing). GambaWhere then shows "No Session".

See `GambaWhereRules.cs` for a drop in class.

### Payload shape

The payload is any object whose public shape matches the following (GambaWhere reads it by reflection,
so a plain C# class works; the property names must match):

```csharp
public sealed class GambaWhereRulesPayload
{
    public List<GambaWhereRuleEntry> Rules { get; set; } = new();
}

public sealed class GambaWhereRuleEntry
{
    public string Label { get; set; } = string.Empty;
    public object? Value { get; set; }
}
```

### Validation

A submission is rejected (returns `false`) unless it passes all of these:

- `Rules` contains between 1 and 10 entries.
- Each `Label` is non empty, 48 characters or fewer, and free of URLs or HTML.
- Each `Value` is a `string`, `bool`, `int`, `long` or `double`. Any other type is dropped.
- A `string` value is 64 characters or fewer and free of URLs or HTML.

Entries that fail are dropped. If nothing valid remains, the whole submission is rejected.

### How values are displayed

- `Label` is the row heading. A label that already contains spaces is shown as is, so prefer readable
  text such as "Boosted Pot". A camelCase label with no spaces is auto spaced (for example `boostedPot`
  becomes "Boosted Pot").
- If a label contains the word "odds" and the value is a `double`, GambaWhere appends an "x" (for
  example `5.0` shows as "5.00x"). Send odds as `double`.
- `int` and `long` values are shown with thousands separators.
- `bool` values show as Yes or No.

## Threading

Invoke the gates from the framework thread, for example inside your `Framework.Update` handler or a UI
event such as a window's `OnOpen`. This is the standard pattern for Dalamud IPC.

## Quick start

1. Copy `GambaWhereWindowOpened.cs` and `GambaWhereRules.cs` into your plugin (rename the namespace).
2. Fill in the `// TODO` markers: your plugin name, your category, and your rule mappings.
3. Construct both objects once during plugin start up and dispose them on unload.
4. Raise `NotifyWindowOpened()` when your window opens.

That is all. GambaWhere does the rest.
