<script setup lang="ts">
import type { IdentityApi, IdentityStatus } from './types';
import { Alert, AlertDescription, Avatar, AvatarFallback, Button, Card, CardContent, Input, Label, Select, SelectContent, SelectItem, SelectTrigger, SelectValue, Separator } from '@pebble/ui';
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { identityMessages } from './messages';

const props = defineProps<{ api: IdentityApi; status: IdentityStatus; productName: string; productIconUrl: string }>();
const emit = defineEmits<{ authenticated: [] }>();
const { t } = useI18n({ messages: identityMessages });
const username = ref('');
const password = ref('');
const confirmPassword = ref('');
const providerId = ref('local');
const error = ref('');
const loading = ref(false);
const passwordProviders = computed(() => [{ id: 'local', displayName: t('identity.local') }, ...props.status.providers.filter(provider => provider.type === 'ldap')]);
const externalProviders = computed(() => props.status.providers.filter(provider => provider.type !== 'ldap'));

async function submit() {
  loading.value = true;
  error.value = '';
  try {
    if (props.status.bootstrapRequired && password.value !== confirmPassword.value) {
      error.value = t('identity.passwordMismatch');
      return;
    }
    if (props.status.bootstrapRequired)
      await props.api.bootstrap(username.value, password.value);
    else
      await props.api.login(username.value, password.value, providerId.value === 'local' ? undefined : providerId.value);
    emit('authenticated');
  }
  catch (reason) {
    error.value = reason instanceof Error ? reason.message : String(reason);
  }
  finally {
    loading.value = false;
  }
}

async function external(providerId: string) {
  loading.value = true;
  error.value = '';
  try {
    window.location.href = await props.api.startExternalLogin(providerId, window.location.href);
  }
  catch (reason) {
    error.value = reason instanceof Error ? reason.message : String(reason);
    loading.value = false;
  }
}
</script>

<template>
  <div class="identity-sign-in identity-sign-in-page">
    <Card class="identity-sign-in__card identity-sign-in-card">
      <CardContent>
        <div class="identity-sign-in__brand">
          <Avatar class="identity-sign-in__logo">
            <img :src="productIconUrl" alt="" class="identity-sign-in__logo-image">
            <AvatarFallback>{{ productName.slice(0, 1) }}</AvatarFallback>
          </Avatar>
          <div>
            <strong>{{ productName }}</strong><div class="aui-text-muted">
              {{ status.bootstrapRequired ? t('identity.bootstrap') : t('identity.signIn') }}
            </div>
          </div>
        </div>
        <Alert v-if="error" variant="destructive" class="aui-mt-4">
          <AlertDescription>{{ error }}</AlertDescription>
        </Alert>
        <form class="aui-stack aui-mt-5" @submit.prevent="submit">
          <div class="identity-field">
            <Label for="identity-username">{{ t('identity.username') }}</Label><Input id="identity-username" v-model="username" autocomplete="username" />
          </div>
          <div v-if="!status.bootstrapRequired && passwordProviders.length > 1" class="identity-field">
            <Label for="identity-provider">{{ t('identity.provider') }}</Label>
            <Select v-model="providerId">
              <SelectTrigger id="identity-provider">
                <SelectValue :placeholder="t('identity.provider')" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem v-for="provider in passwordProviders" :key="provider.id" :value="provider.id">
                  {{ provider.displayName }}
                </SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div class="identity-field">
            <Label for="identity-password">{{ t('identity.password') }}</Label><Input id="identity-password" v-model="password" type="password" :autocomplete="status.bootstrapRequired ? 'new-password' : 'current-password'" />
          </div>
          <div v-if="status.bootstrapRequired" class="identity-field">
            <Label for="identity-confirm-password">{{ t('identity.confirmPassword') }}</Label><Input id="identity-confirm-password" v-model="confirmPassword" type="password" autocomplete="new-password" />
          </div>
          <p v-if="status.bootstrapRequired" class="aui-text-muted">
            {{ t('identity.passwordHint') }}
          </p>
          <Button type="submit" class="w-full" :disabled="loading || !username || !password">
            {{ status.bootstrapRequired ? t('identity.bootstrap') : t('identity.signIn') }}
          </Button>
        </form>
        <div v-if="!status.bootstrapRequired && externalProviders.length" class="aui-stack aui-mt-5">
          <Separator />
          <Button v-for="provider in externalProviders" :key="provider.id" class="w-full" variant="outline" :disabled="loading" @click="external(provider.id)">
            {{ t('identity.externalSignIn', { provider: provider.displayName }) }}
          </Button>
        </div>
      </CardContent>
    </Card>
  </div>
</template>

<style>
.identity-sign-in-page {
  display: grid;
  grid-template-columns: minmax(0, 38.75rem);
  width: 100%;
  min-width: 0;
  min-height: 100%;
  padding: clamp(1rem, 5vw, 2.5rem);
  place-content: center;
}
.identity-sign-in {
  grid-template-columns: minmax(0, 28rem);
  min-height: 100dvh;
}
.identity-sign-in-card {
  width: 100%;
  min-width: 0;
  max-width: 100%;
}
.identity-sign-in__brand {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}
.identity-sign-in__brand img {
  width: 100%;
  height: 100%;
  object-fit: contain;
}
.identity-sign-in__logo {
  display: inline-flex;
  width: 3rem;
  height: 3rem;
  align-items: center;
  justify-content: center;
}
.identity-sign-in__logo-image {
  display: block;
}
.identity-field {
  display: grid;
  gap: 0.5rem;
}
</style>
