<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, RouterLink, RouterView } from 'vue-router'

const route = useRoute()

const sections = [
  { label: 'Overview', to: '/admin/overview' },
  { label: 'Users', to: '/admin/users' },
  { label: 'Activity', to: '/admin/logs' },
]

const activePath = computed(() => route.path)
</script>

<template>
  <div class="admin-shell">
    <div class="admin-head">
      <h1 class="page-title">Admin</h1>
      <p class="admin-sub">Manage users and monitor usage.</p>
    </div>

    <div class="admin-body">
      <aside class="admin-aside">
        <nav class="admin-nav">
          <RouterLink
            v-for="s in sections"
            :key="s.to"
            :to="s.to"
            class="admin-nav-item"
            :class="{ 'is-active': activePath === s.to || activePath.startsWith(s.to + '/') }"
          >
            {{ s.label }}
          </RouterLink>
        </nav>
      </aside>

      <section class="admin-content">
        <RouterView />
      </section>
    </div>
  </div>
</template>

<style scoped>
.admin-shell {
  display: flex;
  flex-direction: column;
  gap: 28px;
  padding: 32px 8px 48px;
  max-width: 1100px;
  margin: 0 auto;
  width: 100%;
}
.page-title { font-size: 28px; }
.admin-sub {
  margin: 6px 0 0;
  color: var(--ink-2);
  font-size: 14px;
}

.admin-body {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
@media (min-width: 768px) {
  .admin-body { flex-direction: row; gap: 32px; }
}

.admin-aside { flex-shrink: 0; }
@media (min-width: 768px) {
  .admin-aside { width: 200px; }
}

.admin-nav {
  display: flex;
  gap: 2px;
  overflow-x: auto;
}
@media (min-width: 768px) {
  .admin-nav { flex-direction: column; gap: 1px; }
}

.admin-nav-item {
  display: flex;
  align-items: center;
  padding: 7px 12px;
  border-radius: 8px;
  font-size: 14px;
  color: var(--ink-1);
  text-decoration: none;
  white-space: nowrap;
  transition: background .12s, color .12s;
}
.admin-nav-item:hover { background: var(--paper-2); color: var(--ink-0); }
.admin-nav-item.is-active {
  background: var(--paper-2);
  color: var(--ink-0);
  font-weight: 600;
}

.admin-content { min-width: 0; flex: 1; }
</style>
