<script setup lang="ts">
import type { DialogPosition } from '@pebble/ui';
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger, Alert, AlertDescription, Badge, Button, Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle, ConfigProvider, Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger, DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger, Field, FieldDescription, FieldLabel, Input, NavigationMenu, NavigationMenuContent, NavigationMenuItem, NavigationMenuLink, NavigationMenuList, NavigationMenuTrigger, navigationMenuTriggerStyle, Select, SelectContent, SelectItem, SelectTrigger, SelectValue, Switch, Table, TableBody, TableCell, TableEmpty, TableHead, TableHeader, TableRow, Tabs, TabsContent, TabsList, TabsTrigger, TagsInput, TagsInputInput, TagsInputItem, TagsInputItemDelete, TagsInputItemText, Textarea } from '@pebble/ui';
import { ref } from 'vue';

const dark = ref(localStorage.getItem('pebble-catalog-theme') === 'dark');
const dialogOpen = ref(false);
const dialogPosition = ref<DialogPosition>('top');
const notifications = ref(true);
const environment = ref('development');
const hosts = ref(['api.example.com', '*.partners.example.com']);

function setTheme(value: boolean) {
  dark.value = value;
  document.documentElement.classList.toggle('dark', value);
  document.documentElement.style.colorScheme = value ? 'dark' : 'light';
  localStorage.setItem('pebble-catalog-theme', value ? 'dark' : 'light');
}
setTheme(dark.value);
</script>

<template>
  <ConfigProvider :scroll-body="false">
    <div class="catalog-shell">
      <aside class="catalog-sidebar">
        <div><strong>ShadCN Vue</strong><small>Nova component catalog</small></div>
        <nav><a href="#foundations">Foundations</a><a href="#actions">Actions</a><a href="#forms">Forms</a><a href="#overlays">Overlays</a><a href="#data">Data display</a></nav>
        <div class="theme-row">
          <span>Dark theme</span><Switch :model-value="dark" @update:model-value="setTheme" />
        </div>
      </aside>
      <main class="catalog-main">
        <header>
          <Badge variant="secondary">
            Nova
          </Badge><h1>Native component catalog</h1><p>Stock ShadCN Vue components with Pebble's indigo actions and legacy blue-slate dark surfaces.</p>
        </header>

        <section id="foundations">
          <h2>Foundations</h2><Alert><AlertDescription>Customization is limited to semantic theme colors. Component markup and behavior come from the native Nova registry.</AlertDescription></Alert><div class="swatches">
            <div class="swatch primary">
              Primary
            </div><div class="swatch accent">
              Accent
            </div><div class="swatch surface">
              Surface
            </div>
          </div>
        </section>

        <section id="actions">
          <h2>Actions</h2><Card>
            <CardHeader><CardTitle>Buttons and menus</CardTitle><CardDescription>Native variants, focus behavior, portals, and transitions.</CardDescription></CardHeader><CardContent class="preview-row">
              <Button>Primary</Button><Button variant="secondary">
                Secondary
              </Button><Button variant="outline">
                Outline
              </Button><Badge variant="success">
                Online
              </Badge><Badge variant="warning">
                Needs attention
              </Badge><Badge variant="info">
                Pending
              </Badge><Button variant="destructive">
                Destructive
              </Button><DropdownMenu :modal="false">
                <DropdownMenuTrigger as-child>
                  <Button variant="outline">
                    Open menu
                  </Button>
                </DropdownMenuTrigger><DropdownMenuContent>
                  <DropdownMenuItem>Change password</DropdownMenuItem><DropdownMenuItem>Edit resource</DropdownMenuItem><DropdownMenuItem variant="destructive">
                    Delete resource
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu><NavigationMenu :viewport="false">
                <NavigationMenuList>
                  <NavigationMenuItem>
                    <NavigationMenuLink href="#actions" :class="navigationMenuTriggerStyle()">
                      Overview
                    </NavigationMenuLink>
                  </NavigationMenuItem><NavigationMenuItem>
                    <NavigationMenuTrigger>Access</NavigationMenuTrigger><NavigationMenuContent class="min-w-48">
                      <ul class="grid gap-1">
                        <li>
                          <NavigationMenuLink href="#forms">
                            Users
                          </NavigationMenuLink>
                        </li>
                        <li>
                          <NavigationMenuLink href="#forms">
                            Consumer keys
                          </NavigationMenuLink>
                        </li>
                        <li>
                          <NavigationMenuLink href="#forms">
                            Management keys
                          </NavigationMenuLink>
                        </li>
                      </ul>
                    </NavigationMenuContent>
                  </NavigationMenuItem><NavigationMenuItem>
                    <NavigationMenuTrigger>System</NavigationMenuTrigger><NavigationMenuContent class="min-w-48">
                      <ul class="grid gap-1">
                        <li>
                          <NavigationMenuLink href="#foundations">
                            Environments
                          </NavigationMenuLink>
                        </li>
                        <li>
                          <NavigationMenuLink href="#data">
                            Gateway instances
                          </NavigationMenuLink>
                        </li>
                        <li>
                          <NavigationMenuLink href="#forms">
                            Settings
                          </NavigationMenuLink>
                        </li>
                      </ul>
                    </NavigationMenuContent>
                  </NavigationMenuItem>
                </NavigationMenuList>
              </NavigationMenu>
            </CardContent>
          </Card>
        </section>

        <section id="forms">
          <h2>Forms</h2><Card>
            <CardHeader><CardTitle>Fields and selection</CardTitle></CardHeader><CardContent class="form-grid">
              <Field><FieldLabel>Gateway name</FieldLabel><Input placeholder="Public API" /><FieldDescription>Shown in administration views.</FieldDescription></Field><Field>
                <FieldLabel>Environment</FieldLabel><Select v-model="environment">
                  <SelectTrigger><SelectValue /></SelectTrigger><SelectContent>
                    <SelectItem value="development">
                      Development
                    </SelectItem><SelectItem value="staging">
                      Staging
                    </SelectItem><SelectItem value="production">
                      Production
                    </SelectItem>
                  </SelectContent>
                </Select>
              </Field><Field class="wide">
                <FieldLabel>Incoming hosts</FieldLabel><TagsInput v-model="hosts" add-on-paste add-on-tab :delimiter="/[\n,]+/">
                  <TagsInputItem v-for="host in hosts" :key="host" :value="host">
                    <TagsInputItemText /><TagsInputItemDelete />
                  </TagsInputItem><TagsInputInput placeholder="Add a host" />
                </TagsInput><FieldDescription>Press Enter after each host.</FieldDescription>
              </Field><Field class="wide">
                <FieldLabel>Description</FieldLabel><Textarea placeholder="Describe this gateway" />
              </Field><div class="theme-row">
                <Switch v-model="notifications" /><span>Enable notifications</span>
              </div>
            </CardContent>
          </Card>
        </section>

        <section id="overlays">
          <h2>Overlays and disclosure</h2><div class="preview-grid">
            <Card>
              <CardHeader><CardTitle>Dialog</CardTitle><CardDescription>Opens at the top by default and supports top, center, and bottom placement.</CardDescription></CardHeader><CardContent>
                <Field>
                  <FieldLabel>Position</FieldLabel><Select v-model="dialogPosition">
                    <SelectTrigger><SelectValue /></SelectTrigger><SelectContent>
                      <SelectItem value="top">
                        Top (default)
                      </SelectItem><SelectItem value="center">
                        Center
                      </SelectItem><SelectItem value="bottom">
                        Bottom
                      </SelectItem>
                    </SelectContent>
                  </Select>
                </Field>
              </CardContent><CardFooter>
                <Dialog v-model:open="dialogOpen">
                  <DialogTrigger as-child>
                    <Button>Open dialog</Button>
                  </DialogTrigger><DialogContent size="lg" scrollable :position="dialogPosition">
                    <DialogHeader><DialogTitle>Publish configuration?</DialogTitle><DialogDescription>This creates a new immutable gateway revision.</DialogDescription></DialogHeader><div data-slot="dialog-body" class="-mx-4 grid gap-4 overflow-y-auto px-4">
                      <p>Review the configuration changes that will be included in this revision.</p>
                      <div v-for="change in 24" :key="change" class="rounded-md border p-3">
                        Route configuration change {{ change }}
                      </div>
                    </div><DialogFooter>
                      <Button variant="outline" @click="dialogOpen = false">
                        Cancel
                      </Button><Button @click="dialogOpen = false">
                        Publish
                      </Button>
                    </DialogFooter>
                  </DialogContent>
                </Dialog>
              </CardFooter>
            </Card><Card>
              <CardHeader><CardTitle>Accordion</CardTitle></CardHeader><CardContent>
                <Accordion type="single" collapsible>
                  <AccordionItem value="advanced">
                    <AccordionTrigger>Advanced upstream</AccordionTrigger><AccordionContent>HTTP version, pooling, health checks, and destination settings.</AccordionContent>
                  </AccordionItem>
                </Accordion>
              </CardContent>
            </Card>
          </div>
        </section>

        <section id="data">
          <h2>Data display</h2><div class="preview-grid">
            <Card class="table-card">
              <CardHeader class="border-b py-4">
                <CardTitle>Routes</CardTitle>
              </CardHeader>
              <Table>
                <TableHeader><TableRow><TableHead>Route</TableHead><TableHead>Environment</TableHead><TableHead>Status</TableHead></TableRow></TableHeader><TableBody>
                  <TableRow>
                    <TableCell>Public API</TableCell><TableCell>Development</TableCell><TableCell>
                      <Badge variant="outline" class="status-ok">
                        Online
                      </Badge>
                    </TableCell>
                  </TableRow><TableRow>
                    <TableCell>Partner API</TableCell><TableCell>Staging</TableCell><TableCell>
                      <Badge variant="outline">
                        Draining
                      </Badge>
                    </TableCell>
                  </TableRow>
                </TableBody>
              </Table>
            </Card><Card class="table-card">
              <Table>
                <TableHeader><TableRow><TableHead>Route</TableHead><TableHead>Environment</TableHead><TableHead>Status</TableHead></TableRow></TableHeader><TableBody>
                  <TableEmpty :colspan="3">
                    No routes match the current filters.
                  </TableEmpty>
                </TableBody>
              </Table>
            </Card>
          </div>
        </section>

        <section>
          <h2>Tabs</h2><Tabs default-value="usage">
            <TabsList>
              <TabsTrigger value="usage">
                Usage
              </TabsTrigger><TabsTrigger value="api">
                API
              </TabsTrigger>
            </TabsList><TabsContent value="usage">
              <Card>
                <CardContent class="tab-content">
                  Import the exact primitives a screen uses and compose them directly.
                </CardContent>
              </Card>
            </TabsContent><TabsContent value="api">
              <Card>
                <CardContent class="tab-content">
                  Public exports mirror the generated component folders.
                </CardContent>
              </Card>
            </TabsContent>
          </Tabs>
        </section>
      </main>
    </div>
  </ConfigProvider>
</template>
