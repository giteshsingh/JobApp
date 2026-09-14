import { defineStore } from 'pinia'
import { api } from '../api/client'

// The active search profile drives every view — jobs, dashboard counts, and
// the pipeline's score column all read profileId from here so switching
// profiles (e.g. "Bangalore hybrid" vs "Anywhere remote") updates everything.
export const useProfilesStore = defineStore('profiles', {
  state: () => ({
    profiles: [],
    activeProfileId: null,
    loading: false,
    error: null,
  }),
  getters: {
    activeProfile: (state) => state.profiles.find((p) => p.id === state.activeProfileId) ?? null,
  },
  actions: {
    async load() {
      this.loading = true
      this.error = null
      try {
        this.profiles = await api.listProfiles()
        if (!this.activeProfileId || !this.profiles.some((p) => p.id === this.activeProfileId)) {
          const active = this.profiles.find((p) => p.isActive) ?? this.profiles[0]
          this.activeProfileId = active?.id ?? null
        }
      } catch (e) {
        this.error = e.message
      } finally {
        this.loading = false
      }
    },
    setActive(id) {
      this.activeProfileId = id
    },
    async save(dto) {
      const saved = dto.id ? await api.updateProfile(dto.id, dto) : await api.createProfile(dto)
      await this.load()
      return saved
    },
    async remove(id) {
      await api.deleteProfile(id)
      await this.load()
    },
  },
})
