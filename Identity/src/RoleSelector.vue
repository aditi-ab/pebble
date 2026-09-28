<script setup lang="ts">
import { Check, ChevronsUpDown } from '@lucide/vue';
import { Button, ListboxContent, ListboxFilter, ListboxItem, ListboxItemIndicator, ListboxRoot, Popover, PopoverAnchor, PopoverContent, PopoverTrigger, TagsInput, TagsInputInput, TagsInputItem, TagsInputItemDelete, TagsInputItemText } from '@pebble/ui';
import { computed, ref } from 'vue';

const props = defineProps<{
  roles: string[];
  placeholder: string;
  emptyLabel: string;
  toggleLabel: string;
}>();

const selectedRoles = defineModel<string[]>({ default: () => [] });
const open = ref(false);
const search = ref('');
const filteredRoles = computed(() => {
  const query = search.value.trim().toLocaleLowerCase();
  return query
    ? props.roles.filter(role => role.toLocaleLowerCase().includes(query))
    : props.roles;
});
</script>

<template>
  <Popover v-model:open="open">
    <ListboxRoot v-model="selectedRoles" multiple highlight-on-hover>
      <PopoverAnchor as-child>
        <TagsInput v-model="selectedRoles" class="role-selector">
          <TagsInputItem v-for="role in selectedRoles" :key="role" :value="role">
            <TagsInputItemText /><TagsInputItemDelete />
          </TagsInputItem>
          <ListboxFilter v-model="search" as-child>
            <TagsInputInput
              :placeholder="selectedRoles.length ? '' : placeholder"
              @focus="open = true"
              @keydown.enter.prevent
            />
          </ListboxFilter>
          <PopoverTrigger as-child>
            <Button type="button" variant="ghost" size="icon-sm" class="role-selector-toggle" :aria-label="toggleLabel">
              <ChevronsUpDown />
            </Button>
          </PopoverTrigger>
        </TagsInput>
      </PopoverAnchor>
      <PopoverContent align="start" class="role-selector-popover">
        <ListboxContent class="role-selector-list">
          <div v-if="!filteredRoles.length" class="role-selector-empty">
            {{ emptyLabel }}
          </div>
          <ListboxItem v-for="role in filteredRoles" :key="role" :value="role" class="role-selector-option">
            <span>{{ role }}</span>
            <ListboxItemIndicator class="role-selector-indicator">
              <Check />
            </ListboxItemIndicator>
          </ListboxItem>
        </ListboxContent>
      </PopoverContent>
    </ListboxRoot>
  </Popover>
</template>

<style scoped>
.role-selector {
  min-height: 2.25rem;
  gap: 0.25rem;
  padding-right: 0.25rem;
}
.role-selector-toggle {
  flex: 0 0 auto;
  margin-left: auto;
}
.role-selector-toggle :deep(svg) {
  width: 1rem;
  height: 1rem;
  color: var(--muted-foreground);
}
:global(.role-selector-popover) {
  width: var(--reka-popover-trigger-width);
  padding: 0.25rem;
}
.role-selector-list {
  max-height: 15rem;
  overflow-y: auto;
}
.role-selector-option {
  position: relative;
  display: flex;
  align-items: center;
  min-height: 2rem;
  cursor: default;
  user-select: none;
  border-radius: calc(var(--radius) - 4px);
  padding: 0.375rem 2rem 0.375rem 0.5rem;
  font-size: 0.875rem;
  outline: none;
}
.role-selector-option[data-highlighted] {
  background: var(--accent);
  color: var(--accent-foreground);
}
.role-selector-indicator {
  position: absolute;
  right: 0.5rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}
.role-selector-indicator :deep(svg) {
  width: 1rem;
  height: 1rem;
}
.role-selector-empty {
  padding: 1.5rem 0.5rem;
  text-align: center;
  color: var(--muted-foreground);
  font-size: 0.875rem;
}
</style>
