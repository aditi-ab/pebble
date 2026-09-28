<script setup lang="ts">
import type { IdentityApi, IdentityProvider, IdentityProviderType, IdentityUser } from './types';
import { KeyRound, Pencil, Plus, Trash2 } from '@lucide/vue';
import { Alert, AlertDescription, AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle, Badge, Button, Card, Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, Field, FieldLabel, Input, Select, SelectContent, SelectItem, SelectTrigger, SelectValue, Switch, Table, TableBody, TableCell, TableEmpty, TableHead, TableHeader, TableRow, Tabs, TabsList, TabsTrigger, Tooltip, TooltipContent, TooltipTrigger } from '@pebble/ui';
import { computed, onMounted, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { identityMessages } from './messages';
import RoleSelector from './RoleSelector.vue';

const props = withDefaults(defineProps<{
  api: IdentityApi;
  formatDateTime?: (value: string) => string;
  providerTypes?: IdentityProviderType[];
  canDeleteProviders?: boolean;
  canDeleteProvider?: (provider: IdentityProvider) => boolean;
  eyebrow?: string;
  title?: string;
  lead?: string;
}>(), { providerTypes: () => ['ldap', 'oidc', 'entra'], canDeleteProviders: true });
const { t } = useI18n({ messages: identityMessages });
const users = ref<IdentityUser[]>([]);
const providers = ref<IdentityProvider[]>([]);
const roles = ref<string[]>([]);
const loading = ref(false);
const error = ref('');
const userDialog = ref(false);
const providerDialog = ref(false);
const editingUser = ref<IdentityUser>();
const editingProvider = ref<IdentityProvider>();
const username = ref('');
const displayName = ref('');
const selectedRoles = ref<string[]>([]);
const enabled = ref(true);
const temporaryPassword = ref('');
const providerSecret = ref('');
const testMessage = ref('');
const providerSettings = computed<any>(() => editingProvider.value?.settings);
const pendingAction = ref<
  | { kind: 'reset-user'; user: IdentityUser }
  | { kind: 'delete-user'; user: IdentityUser }
  | { kind: 'delete-provider'; provider: IdentityProvider }
>();

function emptyProvider(type: IdentityProviderType = 'ldap'): IdentityProvider {
  const common = { id: '', displayName: '', type, enabled: true, autoProvision: false, defaultRoles: ['Reader'], roleMappings: [], hasSecret: false, version: '' };
  if (type === 'ldap')
    return { ...common, settings: { server: '', port: 636, useSsl: true, bindDn: '', baseDn: '', userFilter: '(mail={username})', groupAttribute: 'memberOf' } };
  const claims = { authority: '', clientId: '', scopes: 'openid profile email', roleClaim: 'roles', nameClaim: 'name', emailClaim: type === 'entra' ? 'preferred_username' : 'email', subjectClaim: 'sub', usePkce: true };
  return { ...common, settings: type === 'oidc' ? { ...claims, metadataUrl: '' } : claims };
}

async function load() {
  loading.value = true;
  error.value = '';
  try {
    [users.value, providers.value, roles.value] = await Promise.all([props.api.users(), props.api.providers(), props.api.roles()]);
  }
  catch (reason) {
    error.value = reason instanceof Error ? reason.message : String(reason);
  }
  finally {
    loading.value = false;
  }
}

function createUser() {
  editingUser.value = undefined;
  username.value = '';
  displayName.value = '';
  selectedRoles.value = ['Reader'];
  enabled.value = true;
  userDialog.value = true;
}
function editUser(user: IdentityUser) {
  editingUser.value = user;
  username.value = user.username;
  displayName.value = user.displayName ?? '';
  selectedRoles.value = [...user.roles];
  enabled.value = user.enabled;
  userDialog.value = true;
}
async function saveUser() {
  try {
    if (editingUser.value)
      await props.api.updateUser(editingUser.value.id, { displayName: displayName.value || undefined, roles: selectedRoles.value, enabled: enabled.value, version: editingUser.value.version });
    else
      temporaryPassword.value = (await props.api.createUser({ username: username.value, displayName: displayName.value || undefined, roles: selectedRoles.value })).temporaryPassword;
    userDialog.value = false;
    await load();
  }
  catch (reason) {
    error.value = reason instanceof Error ? reason.message : String(reason);
  }
}
function resetUser(user: IdentityUser) {
  pendingAction.value = { kind: 'reset-user', user };
}
function removeUser(user: IdentityUser) {
  pendingAction.value = { kind: 'delete-user', user };
}
function createProvider() {
  editingProvider.value = emptyProvider(props.providerTypes[0] ?? 'ldap');
  providerSecret.value = '';
  testMessage.value = '';
  providerDialog.value = true;
}
function editProvider(provider: IdentityProvider) {
  editingProvider.value = structuredClone(provider);
  providerSecret.value = '';
  testMessage.value = '';
  providerDialog.value = true;
}
function changeProviderType(type: IdentityProviderType) {
  if (editingProvider.value)
    editingProvider.value = { ...emptyProvider(type), id: editingProvider.value.id, displayName: editingProvider.value.displayName };
}
async function saveProvider() {
  if (!editingProvider.value)
    return;
  try {
    await props.api.saveProvider(editingProvider.value, providerSecret.value || undefined);
    providerDialog.value = false;
    await load();
  }
  catch (reason) {
    error.value = reason instanceof Error ? reason.message : String(reason);
  }
}
async function testProvider() {
  if (!editingProvider.value)
    return;
  const result = await props.api.testProvider(editingProvider.value, providerSecret.value || undefined);
  testMessage.value = result.message;
}
function removeProvider(provider: IdentityProvider) {
  pendingAction.value = { kind: 'delete-provider', provider };
}
function addMapping() {
  editingProvider.value?.roleMappings.push({ source: '', role: roles.value[0] ?? '' });
}
async function confirmPendingAction() {
  const action = pendingAction.value;
  pendingAction.value = undefined;
  if (!action)
    return;
  if (action.kind === 'reset-user')
    temporaryPassword.value = (await props.api.resetPassword(action.user.id)).temporaryPassword;
  else if (action.kind === 'delete-user')
    await props.api.deleteUser(action.user.id);
  else
    await props.api.deleteProvider(action.provider.id);
  await load();
}
function pendingMessage() {
  const action = pendingAction.value;
  if (!action)
    return '';
  if (action.kind === 'reset-user')
    return t('identity.confirmReset', { name: action.user.username });
  return t('identity.confirmDelete', { name: action.kind === 'delete-user' ? action.user.username : action.provider.displayName });
}
onMounted(load);
</script>

<template>
  <div class="identity-management">
    <header class="identity-heading">
      <p>{{ eyebrow ?? t('identity.access') }}</p><h1>{{ title ?? t('identity.title') }}</h1><span>{{ lead ?? t('identity.lead') }}</span>
    </header>
    <Alert v-if="error" variant="destructive">
      <AlertDescription>{{ error }}</AlertDescription>
    </Alert>

    <section>
      <div class="identity-section-heading">
        <h2>{{ t('identity.localUsers') }}</h2><Button @click="createUser">
          <Plus />{{ t('identity.createUser') }}
        </Button>
      </div>
      <Card class="identity-table">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{{ t('identity.username') }}</TableHead><TableHead>{{ t('identity.displayName') }}</TableHead><TableHead>{{ t('identity.roles') }}</TableHead><TableHead>{{ t('identity.status') }}</TableHead><TableHead class="identity-actions">
                {{ t('identity.actions') }}
              </TableHead>
            </TableRow>
          </TableHeader><TableBody>
            <TableRow v-for="user in users" :key="user.id">
              <TableCell class="identity-strong">
                {{ user.username }}
              </TableCell><TableCell>{{ user.displayName }}</TableCell><TableCell>
                <div class="identity-badges">
                  <Badge v-for="role in user.roles" :key="role" variant="secondary">
                    {{ role }}
                  </Badge>
                </div>
              </TableCell><TableCell>
                <Badge variant="outline">
                  {{ user.enabled ? t('identity.enabled') : t('identity.disabled') }}
                </Badge>
              </TableCell><TableCell>
                <div class="identity-action-row">
                  <Tooltip>
                    <TooltipTrigger as-child>
                      <Button variant="ghost" size="icon" :aria-label="t('identity.edit')" @click="editUser(user)">
                        <Pencil />
                      </Button>
                    </TooltipTrigger><TooltipContent>{{ t('identity.edit') }}</TooltipContent>
                  </Tooltip><Tooltip>
                    <TooltipTrigger as-child>
                      <Button variant="ghost" size="icon" :aria-label="t('identity.reset')" @click="resetUser(user)">
                        <KeyRound />
                      </Button>
                    </TooltipTrigger><TooltipContent>{{ t('identity.reset') }}</TooltipContent>
                  </Tooltip><Tooltip>
                    <TooltipTrigger as-child>
                      <Button variant="ghost" size="icon" class="identity-danger" :aria-label="t('identity.delete')" @click="removeUser(user)">
                        <Trash2 />
                      </Button>
                    </TooltipTrigger><TooltipContent>{{ t('identity.delete') }}</TooltipContent>
                  </Tooltip>
                </div>
              </TableCell>
            </TableRow>
            <TableEmpty v-if="!loading && !users.length" :colspan="5">
              {{ t('identity.emptyUsers') }}
            </TableEmpty>
          </TableBody>
        </Table>
      </Card>
    </section>

    <section v-if="providerTypes.length">
      <div class="identity-section-heading">
        <h2>{{ t('identity.providers') }}</h2><Button @click="createProvider">
          <Plus />{{ t('identity.createProvider') }}
        </Button>
      </div>
      <Card class="identity-table">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{{ t('identity.providerName') }}</TableHead><TableHead>{{ t('identity.providerType') }}</TableHead><TableHead>{{ t('identity.status') }}</TableHead><TableHead class="identity-actions">
                {{ t('identity.actions') }}
              </TableHead>
            </TableRow>
          </TableHeader><TableBody>
            <TableRow v-for="provider in providers" :key="provider.id">
              <TableCell class="identity-strong">
                {{ provider.displayName }}
              </TableCell><TableCell>{{ provider.type.toUpperCase() }}</TableCell><TableCell>
                <Badge variant="outline">
                  {{ provider.enabled ? t('identity.enabled') : t('identity.disabled') }}
                </Badge>
              </TableCell><TableCell>
                <div class="identity-action-row">
                  <Tooltip>
                    <TooltipTrigger as-child>
                      <Button variant="ghost" size="icon" :aria-label="t('identity.edit')" @click="editProvider(provider)">
                        <Pencil />
                      </Button>
                    </TooltipTrigger><TooltipContent>{{ t('identity.edit') }}</TooltipContent>
                  </Tooltip><Tooltip v-if="canDeleteProvider?.(provider) ?? canDeleteProviders">
                    <TooltipTrigger as-child>
                      <Button variant="ghost" size="icon" class="identity-danger" :aria-label="t('identity.delete')" @click="removeProvider(provider)">
                        <Trash2 />
                      </Button>
                    </TooltipTrigger><TooltipContent>{{ t('identity.delete') }}</TooltipContent>
                  </Tooltip>
                </div>
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>
      </Card>
    </section>

    <Dialog v-model:open="userDialog">
      <DialogContent>
        <DialogHeader><DialogTitle>{{ editingUser ? t('identity.edit') : t('identity.createUser') }}</DialogTitle></DialogHeader><Field><FieldLabel>{{ t('identity.username') }}</FieldLabel><Input v-model="username" :disabled="!!editingUser" /></Field><Field><FieldLabel>{{ t('identity.displayName') }}</FieldLabel><Input v-model="displayName" /></Field><Field><FieldLabel>{{ t('identity.roles') }}</FieldLabel><RoleSelector v-model="selectedRoles" :roles="roles" :placeholder="t('identity.selectRoles')" :empty-label="t('identity.emptyRoles')" :toggle-label="t('identity.toggleRoles')" /></Field><div v-if="editingUser" class="identity-switch">
          <Switch v-model="enabled" /><span>{{ t('identity.enabled') }}</span>
        </div><DialogFooter>
          <Button variant="outline" @click="userDialog = false">
            {{ t('identity.cancel') }}
          </Button><Button :disabled="!username || !selectedRoles.length" @click="saveUser">
            {{ t('identity.save') }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <Dialog v-model:open="providerDialog">
      <DialogContent v-if="editingProvider" size="2xl" scrollable>
        <DialogHeader><DialogTitle>{{ t('identity.providers') }}</DialogTitle></DialogHeader><div data-slot="dialog-body" class="identity-dialog-body">
          <div class="identity-form-grid">
            <Field><FieldLabel>{{ t('identity.providerId') }}</FieldLabel><Input v-model="editingProvider.id" :disabled="!!editingProvider.version" /></Field><Field><FieldLabel>{{ t('identity.providerName') }}</FieldLabel><Input v-model="editingProvider.displayName" /></Field>
          </div>
          <Tabs :model-value="editingProvider.type" @update:model-value="changeProviderType($event as IdentityProviderType)">
            <TabsList>
              <TabsTrigger v-for="providerType in providerTypes" :key="providerType" :value="providerType">
                {{ providerType.toUpperCase() }}
              </TabsTrigger>
            </TabsList>
          </Tabs>
          <div class="identity-form-grid">
            <div class="identity-switch">
              <Switch v-model="editingProvider.enabled" /><span>{{ t('identity.enabled') }}</span>
            </div><div class="identity-switch">
              <Switch v-model="editingProvider.autoProvision" /><span>{{ t('identity.autoProvision') }}</span>
            </div>
          </div>
          <Field><FieldLabel>{{ t('identity.defaultRoles') }}</FieldLabel><RoleSelector v-model="editingProvider.defaultRoles" :roles="roles" :placeholder="t('identity.selectRoles')" :empty-label="t('identity.emptyRoles')" :toggle-label="t('identity.toggleRoles')" /></Field>
          <template v-if="editingProvider.type === 'ldap'">
            <div class="identity-form-grid">
              <Field><FieldLabel>{{ t('identity.server') }}</FieldLabel><Input v-model="providerSettings.server" /></Field><Field><FieldLabel>{{ t('identity.port') }}</FieldLabel><Input v-model.number="providerSettings.port" type="number" /></Field><Field><FieldLabel>{{ t('identity.bindDn') }}</FieldLabel><Input v-model="providerSettings.bindDn" /></Field><Field><FieldLabel>{{ t('identity.bindPassword') }}</FieldLabel><Input v-model="providerSecret" type="password" /></Field><Field><FieldLabel>{{ t('identity.baseDn') }}</FieldLabel><Input v-model="providerSettings.baseDn" /></Field><Field><FieldLabel>{{ t('identity.userFilter') }}</FieldLabel><Input v-model="providerSettings.userFilter" /></Field>
            </div><div class="identity-switch">
              <Switch v-model="providerSettings.useSsl" /><span>{{ t('identity.useSsl') }}</span>
            </div>
          </template>
          <template v-else>
            <div class="identity-form-grid">
              <Field><FieldLabel>{{ t('identity.authority') }}</FieldLabel><Input v-model="providerSettings.authority" /></Field><Field><FieldLabel>{{ t('identity.clientId') }}</FieldLabel><Input v-model="providerSettings.clientId" /></Field><Field><FieldLabel>{{ t('identity.clientSecret') }}</FieldLabel><Input v-model="providerSecret" type="password" /></Field><Field><FieldLabel>{{ t('identity.scopes') }}</FieldLabel><Input v-model="providerSettings.scopes" /></Field><Field><FieldLabel>{{ t('identity.roleClaim') }}</FieldLabel><Input v-model="providerSettings.roleClaim" /></Field>
            </div>
          </template>
          <div class="identity-section-heading">
            <h3>{{ t('identity.roleMappings') }}</h3><Button variant="outline" size="sm" @click="addMapping">
              <Plus />{{ t('identity.addMapping') }}
            </Button>
          </div><div v-for="(mapping, index) in editingProvider.roleMappings" :key="index" class="identity-mapping">
            <Input v-model="mapping.source" :placeholder="t('identity.sourceValue')" /><Select v-model="mapping.role">
              <SelectTrigger><SelectValue /></SelectTrigger><SelectContent>
                <SelectItem v-for="role in roles" :key="role" :value="role">
                  {{ role }}
                </SelectItem>
              </SelectContent>
            </Select><Tooltip>
              <TooltipTrigger as-child>
                <Button variant="ghost" size="icon" class="identity-danger" :aria-label="t('identity.delete')" @click="editingProvider.roleMappings.splice(index, 1)">
                  <Trash2 />
                </Button>
              </TooltipTrigger><TooltipContent>{{ t('identity.delete') }}</TooltipContent>
            </Tooltip>
          </div>
          <Alert v-if="testMessage">
            <AlertDescription>{{ testMessage }}</AlertDescription>
          </Alert>
        </div><DialogFooter>
          <Button variant="outline" @click="testProvider">
            {{ t('identity.test') }}
          </Button><Button variant="outline" @click="providerDialog = false">
            {{ t('identity.cancel') }}
          </Button><Button :disabled="!editingProvider.id || !editingProvider.displayName" @click="saveProvider">
            {{ t('identity.save') }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <Dialog :open="!!temporaryPassword">
      <DialogContent>
        <DialogHeader><DialogTitle>{{ t('identity.temporaryTitle') }}</DialogTitle><DialogDescription>{{ t('identity.temporaryLead') }}</DialogDescription></DialogHeader><Input :model-value="temporaryPassword" readonly /><DialogFooter>
          <Button @click="temporaryPassword = ''">
            {{ t('identity.saved') }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
    <AlertDialog :open="!!pendingAction">
      <AlertDialogContent>
        <AlertDialogHeader><AlertDialogTitle>{{ pendingAction?.kind === 'reset-user' ? t('identity.reset') : t('identity.delete') }}</AlertDialogTitle><AlertDialogDescription>{{ pendingMessage() }}</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter>
          <AlertDialogCancel @click="pendingAction = undefined">
            {{ t('identity.cancel') }}
          </AlertDialogCancel><AlertDialogAction @click="confirmPendingAction">
            {{ pendingAction?.kind === 'reset-user' ? t('identity.reset') : t('identity.delete') }}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  </div>
</template>

<style scoped>
.identity-management {
  display: grid;
  gap: 2rem;
}
.identity-heading p {
  color: var(--primary);
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.1em;
  text-transform: uppercase;
}
.identity-heading h1 {
  margin: 0.25rem 0;
  font-size: 2rem;
}
.identity-heading span {
  color: var(--muted-foreground);
}
.identity-section-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1rem;
}
.identity-table {
  overflow: hidden;
}
.identity-actions {
  text-align: right;
}
.identity-action-row,
.identity-badges {
  display: flex;
  justify-content: flex-end;
  gap: 0.25rem;
  flex-wrap: wrap;
}
.identity-strong {
  font-weight: 600;
}
.identity-danger {
  color: var(--destructive);
}
.identity-switch {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.identity-form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem;
}
.identity-mapping {
  display: grid;
  grid-template-columns: 1fr 1fr auto;
  gap: 0.5rem;
}
.identity-dialog-body {
  display: grid;
  min-height: 0;
  margin-inline: -1rem;
  padding-inline: 1rem;
  gap: 1rem;
  overflow-x: hidden;
  overflow-y: auto;
}
@media (max-width: 640px) {
  .identity-form-grid,
  .identity-mapping {
    grid-template-columns: 1fr;
  }
}
</style>
