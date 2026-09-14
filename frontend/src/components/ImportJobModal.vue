<script setup>
import { ref } from 'vue'
import { api } from '../api.js'

const emit = defineEmits(['close', 'imported'])

const mode = ref('url') // 'url' | 'text'
const url = ref('')
const text = ref('')
const busy = ref(false)
const message = ref(null)
const errorMsg = ref(null)

async function submit() {
  busy.value = true
  message.value = null
  errorMsg.value = null
  try {
    const result =
      mode.value === 'url'
        ? await api.jobs.importUrl(url.value.trim())
        : await api.jobs.importText(text.value.trim(), url.value.trim() || null)
    message.value = result.message ?? 'Imported.'
    emit('imported')
    if (!result.alreadyExisted) {
      url.value = ''
      text.value = ''
    }
  } catch (e) {
    errorMsg.value = e.message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="overlay" @click.self="emit('close')">
    <div class="card modal">
      <div class="row" style="justify-content: space-between">
        <strong>Import a job</strong>
        <button @click="emit('close')">✕</button>
      </div>
      <p class="secondary">
        LinkedIn and Naukri don't offer a public API, so paste the listing URL (we'll try to fetch it)
        or paste the job description text directly when a site blocks fetching.
      </p>
      <div class="row">
        <button :class="{ primary: mode === 'url' }" @click="mode = 'url'">Paste URL</button>
        <button :class="{ primary: mode === 'text' }" @click="mode = 'text'">Paste text</button>
      </div>

      <input v-model="url" placeholder="https://www.linkedin.com/jobs/view/..." />
      <textarea
        v-if="mode === 'text'"
        v-model="text"
        rows="10"
        placeholder="Paste the full job description here…"
      />

      <div class="row">
        <button
          class="primary"
          :disabled="busy || (mode === 'url' ? !url.trim() : !text.trim())"
          @click="submit"
        >
          {{ busy ? 'Importing…' : 'Import' }}
        </button>
      </div>

      <div v-if="message" style="color: var(--status-good)">{{ message }}</div>
      <div v-if="errorMsg" style="color: var(--status-critical)">{{ errorMsg }}</div>
    </div>
  </div>
</template>

<style scoped>
.overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 100;
}
.modal {
  width: min(560px, 92vw);
  display: flex;
  flex-direction: column;
  gap: 10px;
}
</style>
