import { reactive } from 'vue'
import { api } from './api.js'

// Single shared "which search profile am I viewing" — every view reads this
// instead of re-fetching the profile list independently.
export const profileStore = reactive({
  profiles: [],
  activeId: null,
  loaded: false,

  async load() {
    this.profiles = await api.profiles.list()
    this.loaded = true
    if (this.activeId == null && this.profiles.length) {
      const active = this.profiles.find((p) => p.isActive) ?? this.profiles[0]
      this.activeId = active.id
    }
  },

  get active() {
    return this.profiles.find((p) => p.id === this.activeId) ?? null
  },
})
