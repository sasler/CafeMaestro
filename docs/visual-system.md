# CafeMaestro Visual System

The shared Direction B foundation: semantic colour, design tokens, component recipes and
a vector icon set. Pages consume it; they never invent colours, spacing or glyphs.

## Where things live

| File | Holds |
| --- | --- |
| `Resources/Styles/DesignTokens.xaml` | Spacing, radii, type scale, control sizing, icon sizes, breakpoints, content measures |
| `Resources/Styles/DarkTheme.xaml` | Semantic colours, dark |
| `Resources/Styles/LightTheme.xaml` | The same keys, light |
| `Resources/Styles/ComponentStyles.xaml` | Cards, fields, action bars, chips, icon buttons, empty/error/loading, Shell tinting |
| `Resources/Styles/IconGeometries.xaml` | Path data for every glyph, on a 24 x 24 grid |
| `Controls/IconView.cs` | Renders one glyph at a requested size and semantic colour |

`App.xaml` merges DarkTheme, IconGeometries and ComponentStyles.
`ComponentStyles.xaml` merges `DesignTokens.xaml` itself, so its `StaticResource` lookups
resolve while it is being parsed — and the tokens become available application-wide.

## Colour

Every colour is a semantic key, never a literal. The families are Surface,
ElevatedSurface, RaisedSurface, PrimaryText, SecondaryText, Muted, Border, Roast,
Cooling, Ready, Attention, Danger, Focus, Disabled and Scrim, plus an `On<Family>Color`
for the text that sits on a filled action.

| Meaning | Key | Used for |
| --- | --- | --- |
| Roasting / primary action | `RoastColor` | The live roast, Drop, Import, Add |
| Cooling | `CoolingColor` | Cooling countdowns |
| Complete / ready | `ReadyColor` | Needs weight, Save, confirm |
| Attention | `AttentionColor` | Low inventory, unresolved mappings |
| Destructive / error | `DangerColor` | Discard, delete, failures |

Rules the dictionaries and `VisualSystemTests` keep honest:

- Dark and light expose an identical key set.
- Text and status colours clear 4.5:1 against `SurfaceColor` in both themes.
- Labels on filled actions clear 4.5:1 against their fill.
- Status is always colour **plus** a word **plus** a shape (channel edge or chip); colour
  alone never carries meaning.

Pre-redesign key names (`PrimaryColor`, `CardBackgroundColor`, `ItemDetailTextColor`, …)
remain in both dictionaries as aliases of the semantic palette, so existing pages keep
rendering while later tickets migrate them.

## Theme selection

Dark is the fallback for an install that has never expressed a preference.
`ThemePreferencePolicy.FromStoredValue` resolves the stored value, and any explicit
choice — including `System` — is preserved. While System is selected, the app listens
for `RequestedThemeChanged` and swaps only the colour dictionary; explicit Dark or Light
choices ignore system changes. Tokens, geometries and component styles stay merged and
re-resolve their `DynamicResource` colours without accumulating stale dictionaries.

The theme dictionaries also own `PlatformStatusBarColor` and
`PlatformNavigationBarColor`. Android applies these semantic tokens to native system
chrome and independently selects light or dark system-bar icons from the actual bar
luminance, eliminating the template purple in both themes.

## Icons

One 24 x 24 grid, a fixed 1.75 dp rendered stroke, round caps and joins, no fills.

```xml
<controls:IconView Data="{StaticResource IconDropData}"
                   IconSize="{StaticResource IconSizeMd}"
                   IconColor="{DynamicResource RoastColor}"
                   Description="Drop beans" />
```

`IconView` scales the authored path to the requested size and compensates the path stroke,
so 18, 24 and 32 dp glyphs retain the same optical stroke weight. Its colour contract is
explicit colour first, then the semantic `PrimaryTextColor`; there is no unthemed fallback.

Two deliberate exceptions to the outline rule:

- **First Crack is the only filled glyph** (`IsFilled="True"`). A hollow bolt loses its
  silhouette at 18 dp, and it is the one mark hunted for under time pressure.
- **Settings keeps the mechanical cog**, redrawn hollow at 1.75.

One hard rule: **the circular arrow means Reset and nothing else.** Starting the next
batch is `IconNewBatchData`, a bean with a plus badge. `IconSystemTests` asserts that no
two glyphs share geometry, which is what stops the arrow being reused.

Glyphs are decorative by default (`InputTransparent`, no accessible name). Give an
icon-only control its name on the control, not the glyph.

### Icon buttons

Put the `Button` and the `IconView` in the same `Grid` cell, **Button first** so the
glyph draws over it. The button keeps focus, press states and the accessible name; the
glyph stays a themeable vector.

```xml
<Grid>
    <Button Style="{StaticResource IconButtonStyle}" SemanticProperties.Description="Search" />
    <controls:IconView Data="{StaticResource IconSearchData}" />
</Grid>
```

## Shell icons

Four monochrome assets — `tab_roast_icon.svg`, `tab_log_icon.svg`, `tab_beans_icon.svg`,
`tab_settings_icon.svg` — drawn on the same 24 grid and registered as `MauiImage` with
`BaseSize="24,24"`. XAML references the build output as `.png`; the raw `.svg` name is
never an image source, and `VisualSystemTests` enforces that.

Tinting is supplied by `Shell.TabBarForegroundColor` (selected) and
`Shell.TabBarUnselectedColor` (unselected), set by the implicit `Shell` style in
`ComponentStyles.xaml`. Current Android screenshots verify selected and unselected tinting,
and the isolated Windows target builds cleanly. A fresh Windows screenshot could not be
retained because the desktop capture session was locked, so Windows visual verification
remains an explicit follow-up rather than a claim of current evidence. If a future platform
stops tinting, the fallback is an `AppThemeBinding` on
`ShellContent.Icon` with a light and a dark variant of each asset.

## Sizing and dynamic text

Controls declare `MinimumHeightRequest`, never a fixed height, so text scaling grows a
card or an action instead of clipping it. Minimum touch target is 48 dp
(`TouchTargetMin`); the primary action is 56 dp (`PrimaryActionHeight`).

`FontFamilyTabular` selects the platform's monospace face (Android `monospace`, Windows
`Cascadia Mono`, Apple `Menlo`). `NumericValueStyle` and status chips use it for stable
times, weights, percentages, quantities and counts.

Verified on Android at 150% system font scale: cards grow to two lines, chips and error
text wrap, and nothing truncates.

## Component gallery

`Views/ComponentGalleryPage` renders every component and every glyph declared in
`IconGeometries.xaml`, with a Dark/Light/System switch. It is registered **only in Debug
builds** (`MauiProgram` and `AppShell.AddDebugDestinations`), so it can never reach a
release shell. Reach it from the last tab — on Android that is behind **More**.

Add a new glyph to `IconGeometries.xaml` and it appears in the gallery automatically.

The existing platform **More** overflow remains a light surface when the app is dark.
That mismatch is transitional and intentionally belongs to the four-tab navigation work,
not this foundation ticket.

## What is covered by tests, and what is not

Automated (`VisualSystemTests`, `IconSystemTests`):

- Dark and light expose an identical key set.
- Text and status colours meet 4.5:1 on `SurfaceColor`; action labels meet 4.5:1 on their fill.
- Token sizing honours the 48 dp / 56 dp interaction contract and the 18/24/32 icon steps.
- Rendered icon strokes stay fixed at 1.75 dp across all three icon sizes.
- Bean/New batch, Drop/Import, Cooling/Log, Weigh and Drum retain their distinct geometry invariants.
- Status cards expose reusable phase glyph, word and channel-edge variants, including a neutral-card pattern.
- Numeric values and status data consume the platform tabular-font token.
- Every glyph parses, stays inside the 24 grid, fills a usable share of it, and uses only
  the path commands both parsers understand.
- No two glyphs share geometry — the guard that keeps the circular arrow Reset-only.
- Every `StaticResource`/`DynamicResource` reference in app XAML resolves to a declared key.
- No image source names a raw `.svg`.
- `ThemePreferencePolicy` defaults to dark only when nothing was stored and resolves live
  system-theme changes only while System is selected.
- App resource merging makes every visual-system key reachable, and Android native colour
  resources contain no template purple.

The remaining perceptual checks were verified by rendering the component gallery on an
Android emulator in both themes at normal and 150% system font scale. The Windows target
was build-verified with zero warnings and errors; its fresh screenshot pass was blocked by
the locked desktop session and is not represented in the retained evidence:

- The chosen hues, surface elevations and border weights.
- Spacing rhythm, type scale and card padding.
- Whether each glyph reads as its meaning at 18, 24 and 32 dp.
- Visual-state feedback (pressed, pointer-over, disabled, focused).
- Shell tab tinting for selected and unselected states.

`dotnet build -c Release` is exercised in CI rather than locally: a Release restore needs
the iOS and MacCatalyst workloads, which are not installed on every dev machine.

## Content measures on wide screens

A phone page that fills its width becomes a stretched band on a tablet. `Layouts/ResponsiveLayout`
is an attached property that caps a layout's content and centres the remainder as symmetric
padding, on top of whatever padding the layout already declares:

```xml
<VerticalStackLayout Padding="{StaticResource PagePadding}"
                     layouts:ResponsiveLayout.MaxContentWidth="{StaticResource ReadableContentWidth}">
```

| Token | Value | Use |
| --- | --- | --- |
| `ReadableContentWidth` | 680 | Forms, settings and reading columns |
| `ConsoleContentWidth` | 840 | Console screens pairing a large readout with its controls |
| `ListPaneMaxWidth` | 460 | The list side of a master/detail split |

Below the cap nothing changes, so phone layouts are untouched. Master/detail pages keep using
their own `SizeChanged` handler and the 600 dp `BreakpointMedium`, because they switch structure
rather than just measure.
