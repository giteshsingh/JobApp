<script setup>
import { onMounted } from 'vue'
import { useProfilesStore } from './stores/profiles'

const profiles = useProfilesStore()
onMounted(() => profiles.load())
</script>

<template>
  <div class="shell">
    <aside class="sidebar">
      <div class="brand">
        <span class="brand-dot" />
        Jarvis Jobs
      </div>

      <nav class="nav">
        <RouterLink to="/" exact-active-class="active">Overview</RouterLink>
        <RouterLink to="/jobs" active-class="active">Jobs</RouterLink>
        <RouterLink to="/pipeline" active-class="active">Pipeline</RouterLink>
        <RouterLink to="/resumes" active-class="active">Resumes</RouterLink>
        <RouterLink to="/settings" active-class="active">Settings</RouterLink>
      </nav>

      <div class="profile-switch" v-if="profiles.profiles.length">
        <label>Search profile</label>
        <select
          :value="profiles.activeProfileId"
          @change="profiles.setActive(Number($event.target.value))"
        >
          <option v-for="p in profiles.profiles" :key="p.id" :value="p.id">
            {{ p.name }}{{ p.isActive ? '' : ' (paused)' }}
          </option>
        </select>
      </div>
    </aside>

    <main class="content">
      <RouterView />
    </main>
  </div>
</template>

<style scoped>
.shell {
  display: grid;
  grid-template-columns: 220px 1fr;
  min-height: 100vh;
}

.sidebar {
  background: var(--panel);
  border-right: 1px solid var(--border);
  padding: 20px 16px;
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.brand {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 700;
  font-size: 16px;
}

.brand-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: var(--accent);
  box-shadow: 0 0 8px var(--accent);
}

.nav {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.nav a {
  color: var(--text-dim);
  padding: 8px 10px;
  border-radius: 6px;
}

.nav a:hover {
  color: var(--text);
  background: var(--panel-2);
  text-decoration: none;
}

.nav a.active {
  color: var(--text);
  background: var(--accent-dim);
  font-weight: 600;
}

.profile-switch {
  margin-top: auto;
}

.content {
  padding: 24px 32px;
  max-width: 1400px;
}
</style>
