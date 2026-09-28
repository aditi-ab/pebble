# @pebble/identity

Reusable Vue 3 sign-in and identity-management screens for `@pebble/ui`. The components accept an `IdentityApi`, so the backend transport and URL structure remain application-controlled.

## Installation

```sh
yarn add @pebble/identity @pebble/ui vue vue-i18n
```

Import both packages' styles at the application entry point. Import UI components by name where they are used:

```ts
import '@pebble/ui/styles.css';
import '@pebble/identity/styles.css';

createApp(App).use(i18n).mount('#app');
```

## Default REST client

`createIdentityApi()` uses `/admin` as its base URL and the standard Pebble route contract:

```ts
import { createIdentityApi } from '@pebble/identity';

const identity = createIdentityApi();
```

The base URL, endpoint paths, fetch implementation, credentials, headers, and antiforgery header are configurable. Endpoint overrides are partial, so unspecified routes retain their defaults.

```ts
const identity = createIdentityApi({
  baseUrl: '/control-plane',
  endpoints: {
    status: '/session',
    login: '/session/password',
    users: '/access/users',
    user: id => `/access/users/${encodeURIComponent(id)}`,
  },
  credentials: 'same-origin',
  headers: { 'X-Application': 'administration-console' },
  antiforgeryHeader: 'X-Antiforgery',
});
```

An endpoint override may be an absolute URL. Cross-origin cookie authentication also requires compatible server CORS, cookie, and antiforgery settings. The secure default remains same-origin requests.

The legacy `createIdentityApi('/admin')` form remains supported.

## Implementing another backend

For GraphQL, RPC, or another REST shape, implement `IdentityApi` directly and pass it to `IdentitySignIn` or `IdentityManagement`. The UI does not inspect URLs or perform requests itself.

```ts
import type { IdentityApi } from '@pebble/identity';

export const identityApi: IdentityApi = {
  status: () => client.getSession(),
  bootstrap: (username, password) => client.createFirstAdministrator({ username, password }),
  login: (username, password, providerId) => client.signIn({ username, password, providerId }),
  startExternalLogin: (providerId, returnUrl) => client.externalLoginUrl(providerId, returnUrl),
  logout: () => client.signOut(),
  changePassword: (currentPassword, newPassword) => client.changePassword({ currentPassword, newPassword }),
  roles: () => client.listRoles(),
  users: () => client.listUsers(),
  createUser: input => client.createUser(input),
  updateUser: (id, input) => client.updateUser(id, input),
  resetPassword: id => client.resetPassword(id),
  deleteUser: id => client.deleteUser(id),
  providers: () => client.listProviders(),
  saveProvider: (provider, secret) => client.saveProvider(provider, secret),
  deleteProvider: id => client.deleteProvider(id),
  testProvider: (provider, secret) => client.testProvider(provider, secret),
};
```

Implementations must preserve the TypeScript return types exported from the package. Mutation methods returning `Promise<void>` must reject on failure. Temporary passwords are returned only by `createUser` and `resetPassword`.

## Standard HTTP contract

All paths below are relative to the configured base URL.

| Method          | Default path                          | Purpose                                                            |
| --------------- | ------------------------------------- | ------------------------------------------------------------------ |
| `GET`           | `/auth/status`                        | Return session state, enabled providers, and an antiforgery token. |
| `POST`          | `/auth/bootstrap`                     | Create and sign in the first administrator.                        |
| `POST`          | `/auth/login`                         | Sign in with a local or LDAP password provider.                    |
| `POST`          | `/auth/external/{providerId}/start`   | Return `{ "url": "..." }` for an external redirect.                |
| `POST`          | `/auth/logout`                        | End the current session.                                           |
| `POST`          | `/auth/change-password`               | Change the current local password.                                 |
| `GET`           | `/identity/roles`                     | Return a JSON string array of assignable roles.                    |
| `GET`, `POST`   | `/identity/users`                     | List or create local users.                                        |
| `PUT`, `DELETE` | `/identity/users/{id}`                | Update or delete a user.                                           |
| `POST`          | `/identity/users/{id}/reset-password` | Reset a user and return a temporary password.                      |
| `GET`           | `/identity/providers`                 | List configured identity providers.                                |
| `PUT`, `DELETE` | `/identity/providers/{id}`            | Save or delete a provider.                                         |
| `POST`          | `/identity/providers/test`            | Validate provider settings and credentials.                        |

The status response has this shape:

```json
{
  "authenticated": true,
  "bootstrapRequired": false,
  "username": "administrator",
  "mustChangePassword": false,
  "roles": ["Administrator"],
  "providers": [{ "id": "entra", "displayName": "Microsoft Entra", "type": "entra" }],
  "antiforgeryToken": "request-token"
}
```

The default client sends cookies according to `credentials` and sends the status response's token in `X-CSRF-TOKEN` on later requests. Servers should return JSON errors with `message` or `details`, otherwise the client reports the HTTP status.

User updates and provider saves include a `version` value for optimistic concurrency. Provider responses expose `hasSecret`, never the stored secret. Provider types are `ldap`, `oidc`, and `entra`.
