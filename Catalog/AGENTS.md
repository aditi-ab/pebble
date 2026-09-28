# Pebble Catalog

These instructions apply to the standalone `@pebble/catalog` workspace.

## Package boundary

- Consume Pebble only through the public `@pebble/ui` package entry points and `@pebble/ui/styles.css`. Never import from `../Ui/src`, `../Ui/dist`, or another internal UI path.
- Keep catalog routes, metadata, examples, styles, and tests inside this workspace. Catalog code must never be exported by or bundled into `@pebble/ui`.
- Keep this package private. It is a development and documentation application, not a published npm package.

## Component documentation

- Group parent components and their supporting child primitives into one user-facing family page. For example, Card owns Card Title, Card Item, Card Text, and Card Actions. Do not create navigation entries for fragments that only make sense inside their parent.
- Give every family a purpose, usage guidance, realistic interactive examples, accessibility guidance, and related families. Place each family component's complete props, events, slots, and aliases behind component tabs in the shared API section.
- Map child-component and alias URLs to their family page instead of duplicating documentation.
- Define every showcased family in the example registry. Generate the rendered case and displayed source from the same realistic Vue example so they cannot diverge.
- Group related props, events, and slots into a small set of user-focused scenarios. Every public capability must be covered across those scenarios, but do not create mechanical one-prop-per-card examples.
- Name examples after the user outcome or interface pattern, not the prop, event, slot, or internal implementation concept.
- Every example must be interactive where the real component is interactive, use valid values, and show realistic content. Do not render placeholder text inside components that do not support content slots.
- Keep each distinct scenario in its own example card. Do not reuse generic examples across family pages.
- Demonstrate meaningful behavior and states, including disabled, loading, validation, empty, keyboard, responsive, and theme behavior when they apply. Keep data realistic but free of credentials, internal hosts, personal information, and other sensitive product data.

## Catalog UI

- Use the native ShadCN Vue components exported by `@pebble/ui` for catalog controls and visual surfaces. Use ShadCN semantic tokens and semantic HTML for document structure and layout.
- Preserve grouped navigation, stable hash routes, responsive mobile navigation, semantic heading order, keyboard operation, visible focus, accessible names, sufficient contrast, and reduced-motion behavior.
- Keep all user-facing catalog content in English until a catalog localization strategy is introduced.
- Do not use em dashes in literal user-facing text.

## Validation

- Build `@pebble/ui` before running the catalog in isolation because the catalog deliberately consumes the package's built public exports.
- After catalog changes, run `yarn lint:fix`, `yarn type-check`, `yarn test`, and `yarn build` from this workspace.
- When production UI code also changes, run the UI package's type-check, tests, and build before validating the catalog.
