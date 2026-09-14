<script setup>
import { ref, onMounted } from 'vue'
import { api } from '../api.js'

const resumes = ref([])
const loading = ref(true)
const errorMsg = ref(null)
const uploading = ref(false)
const isCoverLetter = ref(false)
const fileInput = ref(null)

async function load() {
  loading.value = true
  try {
    resumes.value = await api.resumes.list()
  } catch (e) {
    errorMsg.value = e.message
  } finally {
    loading.value = false
  }
}
onMounted(load)

async function onFileChange(e) {
  const file = e.target.files?.[0]
  if (!file) return
  uploading.value = true
  errorMsg.value = null
  try {
    await api.resumes.upload(file, isCoverLetter.value)
    await load()
  } catch (err) {
    errorMsg.value = err.message
  } finally {
    uploading.value = false
    e.target.value = ''
  }
}

async function setMaster(r) {
  try {
    await api.resumes.setMaster(r.id)
    await load()
  } catch (e) {
    errorMsg.value = e.message
  }
}

async function remove(r) {
  try {
    await api.resumes.remove(r.id)
    await load()
  } catch (e) {
    errorMsg.value = e.message
  }
}
</script>

<template>
  <div class="page stack">
    <h2 style="margin: 0">Resumes &amp; cover letters</h2>

    <div class="card row wrap">
      <label class="row"><input type="checkbox" v-model="isCoverLetter" /> This upload is a cover letter</label>
      <input ref="fileInput" type="file" accept=".docx,.txt,.md" @change="onFileChange" :disabled="uploading" />
      <span v-if="uploading" class="muted">Uploading…</span>
    </div>

    <div v-if="errorMsg" style="color: var(--status-critical)">{{ errorMsg }}</div>
    <div v-else-if="loading" class="muted">Loading…</div>

    <div class="stack">
      <div v-for="r in resumes" :key="r.id" class="card row wrap" style="justify-content: space-between">
        <div>
          <strong>{{ r.name }}</strong>
          <span v-if="r.isMaster" class="badge" style="background: var(--status-good); color: #fff; margin-left: 6px">master</span>
          <span v-if="r.isCoverLetter" class="badge" style="background: var(--cat-violet); color: #fff; margin-left: 6px">cover letter</span>
          <div class="muted">
            {{ r.sectionCount }} sections · {{ r.extractedSkills.length }} skills detected
            <span v-if="r.yearsExperience != null"> · ~{{ r.yearsExperience }}y experience</span>
            · updated {{ new Date(r.updatedAt).toLocaleDateString() }}
          </div>
        </div>
        <div class="row">
          <button v-if="!r.isMaster && !r.isCoverLetter" @click="setMaster(r)">Set as master</button>
          <a :href="api.tailoring.exportUrl(r.id, 'docx')">.docx</a>
          <a :href="api.tailoring.exportUrl(r.id, 'pdf')">.pdf</a>
          <button class="danger" @click="remove(r)">Delete</button>
        </div>
      </div>
      <p v-if="!loading && !resumes.length" class="muted">No resumes uploaded yet.</p>
    </div>
  </div>
</template>
