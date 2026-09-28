# Pebble

Pebble is an open-source administration UI and identity toolkit for Vue and ASP.NET Core applications. It is maintained independently and included in product repositories as the `Packages/Pebble` Git submodule.

## Packages

- `@pebble/ui`: Vue 3 administration components, design tokens, and compiled styles.
- `@pebble/identity`: Vue identity screens, the transport-independent `IdentityApi` contract, and a configurable REST client.
- `@pebble/catalog`: Private component documentation and interactive examples for the public UI package.
- `Pebble.Identity.AspNetCore`: ASP.NET Core identity services and configurable minimal API endpoints.

## Development

Install all JavaScript dependencies from this directory:

```sh
yarn install
```

Run the shared checks with `yarn lint`, `yarn type-check`, `yarn test`, and `yarn build`.

Run the component catalog with `yarn dev:catalog`, or create its static build with `yarn build:catalog`. The catalog consumes the built public `@pebble/ui` package and is never published to npm.

The identity integration and HTTP contract are documented in [Identity/README.md](Identity/README.md). The ASP.NET Core setup is documented in [Identity.AspNetCore/README.md](Identity.AspNetCore/README.md).

## Publishing

Maintainers publish versioned packages through the repository workflow. Release credentials and registry policy belong in the hosting platform's protected settings and must never be committed or documented here. Keep the npm and NuGet contract versions aligned when a release changes identity request or response shapes.

Before the first Pebble release, configure publishing access and trusted publishers for the `@pebble` npm scope and the `Pebble.Identity.AspNetCore` NuGet package. The source repository is [aditi-ab/pebble](https://github.com/aditi-ab/pebble).

## Integration

Update npm dependencies and imports to `@pebble/ui` and `@pebble/identity`, .NET project references to `Pebble.Identity.AspNetCore.csproj`, namespaces to `Pebble.Identity`, and identity registration and endpoint methods to `AddPebbleIdentity` and `MapPebbleIdentity*`. Source integrations now use `Packages/Pebble`.

Authentication cookies, schemes, claim identifiers, and data-protection purposes use the Pebble name. This is a fresh start: sessions and encrypted provider secrets from earlier development builds are not migrated. Sign in again and re-enter provider secrets if reusing development data.

## License

Pebble is licensed under the [Apache License 2.0](LICENSE).
