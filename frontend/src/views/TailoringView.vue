<script setup>
import { ref, onMounted } from 'vue'
import { api } from '../api.js'

const props = defineProps({ jobId: { type: [String, Number], required: true } })

const job = ref(null)
const resumes = ref([])
const sessions = ref([])
const session = ref(null)
const resumeId = ref(null)
const includeCoverLetter = ref(false)
const generating = ref(false)
const building = ref(false)
const errorMsg = ref(null)
const builtResume = ref(null)

async function loadContext() {
  const [jobData, resumeList, existing] = await Promise.all([
    api.jobs.get(Number(props.jobId)),
    api.resumes.list(),
    api.tailoring.byJob(Number(props.jobId)),
  ])
  job.value = jobData
  resumes.value = resumeList
  sessions.value = existing
  const master = resumeList.find((r) => r.isMaster)
  resumeId.value = master?.id ?? resumeList[0]?.id ?? null
  if (existing.length) session.value = existing[0]
}

onMounted(loadContext)

async function generate() {
  if (!resumeId.value) return
  generating.value = true
  errorMsg.value = null
  try {
    session.value = await api.tailoring.create(Number(props.jobId), resumeId.value, includeCoverLetter.value)
    builtResume.value = null
  } catch (e) {
    errorMsg.value = e.message
  } finally {
    generating.value = false
  }
}

async function decide(s, accepted) {
  try {
    const updated = await api.tailoring.decide(s.id, accepted, null)
    const idx = session.value.suggestions.findIndex((x) => x.id === s.id)
    session.value.suggestions[idx] = updated
  } catch (e) {
    errorMsg.value = e.message
  }
}

async function saveEdit(s, text) {
  try {
    const updated = await api.tailoring.decide(s.id, true, text)
    const idx = session.value.suggestions.findIndex((x) => x.id === s.id)
    session.value.suggestions[idx] = updated
  } catch (e) {
    errorMsg.value = e.message
  }
}

async function build() {
  building.value = true
  errorMsg.value = null
  try {
    builtResume.value = await api.tailoring.build(session.value.id)
  } catch (e) {
    errorMsg.value = e.message
  } finally {
    building.value = false
  }
}

function editingText(s) {
  return s.userEditedText ?? s.suggestedText
}
</script>

<template>
  <div class="page stack">
    <h2 style="margin: 0">Tailor resume</h2>
    <div v-if="job" class="card">
      <strong>{{ job.title }}</strong> · {{ job.company }} · {{ job.location }}
    </div>

    <div class="card row wrap">
      <label class="row">Base resume
        <select v-model.number="resumeId">
          <option v-for="r in resumes" :key="r.id" :value="r.id">{{ r.name }}{{ r.isMaster ? ' (master)' : '' }}</option>
        </select>
      </label>
      <label class="row"><input type="checkbox" v-model="includeCoverLetter" /> Draft a cover letter too</label>
      <button class="primary" :disabled="generating || !resumeId" @click="generate">
        {{ generating ? 'Asking Claude…' : (session ? 'Regenerate' : 'Generate tailoring') }}
      </button>
      <select v-if="sessions.length > 1" @change="session = sessions.find(s => s.id === Number($event.target.value))">
        <option value="">Previous runs…</option>
        <option v-for="s in sessions" :key="s.id" :value="s.id">
          {{ new Date(s.createdAt).toLocaleString() }} ({{ s.status }})
        </option>
      </select>
    </div>

    <div v-if="errorMsg" style="color: var(--status-critical)">{{ errorMsg }}</div>

    <template v-if="session">
      <div v-if="session.status === 'Failed'" style="color: var(--status-critical)">{{ session.error }}</div>

      <div v-if="session.tailoredSummary" class="card stack">
        <strong>Suggested summary</strong>
        <p class="secondary">{{ session.tailoredSummary }}</p>
        <div v-if="session.tailoredSkillsLine"><span class="muted">Skills line:</span> {{ session.tailoredSkillsLine }}</div>
        <div v-if="session.keywordsToAdd?.length" class="row wrap">
          <span class="muted">Keywords to weave in:</span>
          <span v-for="k in session.keywordsToAdd" :key="k" class="badge" style="background: var(--seq-400); color: #fff">{{ k }}</span>
        </div>
        <p v-if="session.honestyNotes" class="muted">⚠ {{ session.honestyNotes }}</p>
      </div>

      <div v-if="session.coverLetterDraft" class="card">
        <strong>Cover letter draft</strong>
        <p class="secondary" style="white-space: pre-wrap">{{ session.coverLetterDraft }}</p>
      </div>

      <div class="stack">
        <strong>Bullet-by-bullet suggestions ({{ session.suggestions.length }})</strong>
        <div v-for="s in session.suggestions" :key="s.id" class="card diff-row">
          <div class="muted">{{ s.sectionHeading ?? s.section }}</div>
          <div class="diff-cols">
            <div>
              <div class="muted">Original</div>
              <p class="secondary">{{ s.originalText || '(new bullet)' }}</p>
            </div>
            <div>
              <div class="muted">Suggested</div>
              <textarea
                :value="editingText(s)"
                rows="3"
                @change="saveEdit(s, $event.target.value)"
              />
            </div>
          </div>
          <p v-if="s.rationale" class="muted">{{ s.rationale }}</p>
          <div v-if="s.keywordsAdded?.length" class="row wrap">
            <span v-for="k in s.keywordsAdded" :key="k" class="badge" style="background: var(--seq-400); color: #fff">{{ k }}</span>
          </div>
          <div class="row">
            <button :class="{ primary: s.accepted === true }" @click="decide(s, true)">Accept</button>
            <button :class="{ danger: s.accepted === false }" @click="decide(s, false)">Reject</button>
          </div>
        </div>
      </div>

      <div class="card row">
        <button class="primary" :disabled="building" @click="build">
          {{ building ? 'Building…' : 'Build tailored resume from accepted suggestions' }}
        </button>
      </div>

      <div v-if="builtResume" class="card row wrap">
        <strong>{{ builtResume.name }}</strong>
        <a :href="api.tailoring.exportUrl(builtResume.id, 'docx')">Download .docx</a>
        <a :href="api.tailoring.exportUrl(builtResume.id, 'pdf')">Download .pdf</a>
      </div>
    </template>
  </div>
</template>

<style scoped>
.diff-cols {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}
.diff-row textarea {
  width: 100%;
}
</style>
