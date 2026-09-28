# @pebble/ui

Native ShadCN Vue components generated with the default Nova style and the Reka UI base.

## Usage

Import the shared stylesheet once, then import only the compound primitives a screen uses:

```ts
import { Button, Dialog, DialogContent, Input } from '@pebble/ui';
import '@pebble/ui/styles.css';
```

The public API mirrors the generated folders under `src/components/ui`. Components keep their stock ShadCN markup, variants, portal behavior, accessibility, and transitions. Product screens compose these primitives directly and use semantic HTML plus Tailwind utilities for layout.

Dialogs open near the top of the viewport by default. Use the shared `position` prop when a workflow needs another placement:

```vue
<DialogContent position="center">
  <!-- Dialog content -->
</DialogContent>
```

The supported positions are `top`, `center`, and `bottom`.

## Theme

The stylesheet uses ShadCN semantic tokens. The only project-level customization is the color palette: indigo primary colors, neutral light surfaces, and bluish dark surfaces. Toggle the `dark` class on the document element to select the dark theme.

Do not add `Ui*` wrappers, compatibility aliases, component-specific CSS overrides, or product behavior to this package. Add primitives through the ShadCN Vue CLI using the repository's `components.json` configuration.
