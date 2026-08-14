# Delta for landing-page

## MODIFIED Requirements

### Requirement: LANDING-4 — Secondary Outbound Links

The landing page MUST provide outbound links to the GitHub repository, the README, the
presentation deck, and the user guide. These MUST be visually subordinate to the primary sign-up
CTA. The guide link MUST be locale-aware, resolving to `/guide/en/` or `/guide/es/` based on
`useLocaleStore().locale`. This is an explicit, scoped exception to this area's convention that
outbound link URLs are not translated (GitHub/README/deck remain single, locale-independent
URLs) — the guide is the first outbound target with two genuinely localized artifacts.
(Previously: covered GitHub, README, and deck only, with no locale-aware link.)

#### Scenario: Outbound links present and functional

- GIVEN the landing page renders
- WHEN a visitor clicks the GitHub, README, deck, or guide link
- THEN the corresponding resource opens

#### Scenario: Outbound links are visually secondary

- GIVEN the landing page renders
- WHEN the CTA area and outbound links are compared
- THEN outbound links do not share the sign-up button's primary styling

#### Scenario: Guide link resolves per active locale

- GIVEN the landing page renders with `useLocaleStore().locale` set to `en`
- WHEN the visitor switches locale to `es` via `LanguageSwitcher`
- THEN the guide link's `href` updates from `/guide/en/` to `/guide/es/` without a page reload
