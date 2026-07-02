> **Status:** Accepted
>
> **Version:** 0.1
>
> **Last Updated:** 2026-07-01
>
> **Applies To:** Deadbelt Operations Platform (DOP)
>
> **Audience:** Contributors, Architects, Maintainers
>
> **Related Documents:**
> - docs/architecture/design-principles.md
> - docs/architecture/technology-stack.md
> - docs/adr/ADR-0007-WPF-Desktop-For-Initial-Client.md

# ADR-0009: Desktop Branding Strategy

## Status

Accepted

## Context

Deadbelt has an established visual identity built around a segmented hexagonal "D" logo and a dark green, gray, black, and white palette.

As the desktop application becomes usable, it should visually reflect the Deadbelt identity rather than appearing like a default WPF application.

The project needs a consistent branding strategy for:

- Desktop application header
- Dialogs
- Application icon
- GitHub assets
- Discord assets
- Documentation
- Installer assets
- Future website assets

## Decision

DOP will use the Deadbelt segmented hexagonal "D" logo as the canonical primary brand mark.

The desktop application will use a dark-theme-first visual style aligned with the approved Deadbelt color palette.

The primary brand colors are:

- Green: `#5FAF3C`
- Dark gray: `#22272E`
- Medium gray: `#4A5563`
- Light gray: `#D8DEE6`
- White: `#FFFFFF`

The logo will be used in the main desktop header and later reused in dialogs, application icons, installer assets, documentation, and web/community surfaces.

## WPF Resource Decision

The desktop logo is stored as a WPF Resource with:

    Build Action: Resource
    Copy to Output Directory: Do not copy

Because `MainWindow.xaml` is located under the `Views` folder, the working XAML reference is:

    ../Assets/Images/deadbelt-logo.png

The final working implementation uses an `ImageBrush` in the header layout.

This approach was selected because it rendered correctly at runtime and avoids copying branding assets separately into the output directory.

## Alternatives Considered

### Text-Only Branding

Rejected because the application did not visually reflect the project identity.

### Multiple Logo Variations

Rejected for the MVP because it risks inconsistent branding across GitHub, Discord, desktop, documentation, and installer assets.

### Content Asset with Copy to Output Directory

Tested but not selected as the final approach for the current implementation. It introduced ambiguity between designer-time and runtime paths.

### Pack URI with Assembly Component Path

Tested but did not consistently render during early runtime validation.

### Direct Image Control

Tested but the `ImageBrush` approach proved more reliable during the initial WPF layout work.

## Consequences

### Positive

- Desktop application now visually reflects the Deadbelt brand.
- Screenshots are more polished and recognizable.
- Future UI work has a stronger visual foundation.
- A single canonical logo reduces branding inconsistency.
- Branding can be reused across app, repository, Discord, documentation, and installer assets.

### Negative

- Branding assets must be maintained carefully.
- Future logo changes may require updates across multiple surfaces.
- WPF resource paths must be handled carefully when XAML files are located in subfolders.

## Implementation Notes

The initial desktop branding includes:

- Logo in the main application header
- Dark theme
- Green primary action color
- Gray surface and border palette
- Branded workspace dashboard and navigation shell

Future branding tasks may include:

- Application `.ico` file
- Taskbar icon
- Dialog header logos
- About dialog
- Installer graphics
- Documentation screenshots