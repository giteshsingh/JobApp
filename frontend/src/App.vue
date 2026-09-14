<script setup>
import { onMounted } from 'vue'
import { RouterLink, RouterView } from 'vue-router'
import { profileStore } from './profileStore.js'

onMounted(() => {
  profileStore.load()
})
</script>

<template>
  <div class="shell">
    <header class="topbar">
      <div class="brand">Jarvis · Job Dashboard</div>
      <nav class="row">
        <RouterLink to="/">Overview</RouterLink>
        <RouterLink to="/jobs">Jobs</RouterLink>
        <RouterLink to="/pipeline">Pipeline</RouterLink>
        <RouterLink to="/resumes">Resumes</RouterLink>
        <RouterLink to="/settings">Settings</RouterLink>
      </nav>
      <select
        v-if="profileStore.profiles.length"
        v-model.number="profileStore.activeId"
        title="Active search profile"
      >
        <option v-for="p in profileStore.profiles" :key="p.id" :value="p.id">
          {{ p.name }}
        </option>
      </select>
    </header>
    <RouterView />
  </div>
</template>

<style scoped>
.topbar {
  display: flex;
  align-items: center;
  gap: 24px;
  padding: 12px 24px;
  border-bottom: 1px solid var(--border);
  background: var(--surface-1);
  position: sticky;
  top: 0;
  z-index: 10;
}

.brand {
  font-weight: 700;
  white-space: nowrap;
}

nav {
  gap: 16px;
}

nav a {
  color: var(--text-secondary);
  font-weight: 500;
}

nav a.router-link-active {
  color: var(--text-primary);
}

select {
  margin-left: auto;
}
</style>
