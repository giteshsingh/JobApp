<script setup>
import { ref, computed } from 'vue'
import { api } from '../api/client'

const props = defineProps({
  jobId: { type: Number, required: true },
  resumeId: { type: Number, default: null },
})

const session = ref(null)
const running = ref(false)
const error = ref(null)
const includeCoverLetter = ref(false)
const building = ref(false)
const built = ref(null)
const editingId = ref(null)
const editText = ref('')

async function run() {
  running.value = true
  error.value = null
  try {
    session.value = await api.createTailoring(props.jobId, props.resumeId, includeCoverLetter.value)
  } catch (e) {
    error.value = e.message
  } finally {
    running.value = false
  }
}

async function decide(suggestion, accepted) {
  const updated = await api.decideSuggestion(suggestion.id, accepted, null)
  Object.assign(suggestion, updated)
}

function startEdit(suggestion) {
  editingId.value = suggestion.id
  editText.value = suggestion.userEditedText || suggestion.suggestedText
}

async function saveEdit(suggestion) {
  const updated = await api.decideSuggestion(suggestion.id, true, editText.value)
  Object.assign(suggestion, updated)
  editingId.value = null
}

const acceptedCount = computed(
  () => session.value?.suggestions.filter((s) => s.accepted === true).length ?? 0,
)

async function build() {
  building.value = true
  error.value = null
  try {
    built.value = await api.buildTailoredResume(session.value.id)
  } catch (e) {
    error.value = e.message
  } finally {
    building.value = false
  }
}
</script>

<template>
  <div class="col">
    <div v-if="!session" class="row">
      <label class="row" style="gap: 6px">
        <input type="checkbox" v-model="includeCoverLetter" style="width: auto" />
        Also draft a cover letter
      </label>
      <button class="primary" @click="run" :disabled="running">
        <span v-if="running" class="spinner" />
        {{ running ? 'Tailoring…' : 'Customise resume for this job' }}
      </button>
    </div>

    <div v-if="error" class="error-banner">{{ error }}</div>

    <template v-if="session && session.status === 'Ready'">
      <div class="card">
        <h4>Tailored summary</h4>
        <p>{{ session.tailoredSummary }}</p>
        <h4>Skills line</h4>
        <p class="muted">{{ session.tailoredSkillsLine }}</p>
        <div v-if="session.keywordsToAdd?.length">
          <h4>Keywords worth adding</h4>
          <div class="row wrap">
            <span v-for="k in session.keywordsToAdd" :key="k" class="tag">{{ k }}</span>
          </div>
        </div>
        <div v-if="session.honestyNotes" class="honesty-note">
          <strong>What wasn't claimed:</strong> {{ session.honestyNotes }}
        </div>
      </div>

      <div class="card">
        <div class="row between">
          <h4>Bullet-by-bullet suggestions ({{ acceptedCount }}/{{ session.suggestions.length }} accepted)</h4>
        </div>

        <div v-for="s in session.suggestions" :key="s.id" class="suggestion">
          <div class="muted small">{{ s.sectionHeading || s.section }}</div>

          <div v-if="s.originalText" class="diff-line removed">− {{ s.originalText }}</div>
          <div v-if="editingId === s.id">
            <textarea v-model="editText" rows="2"></textarea>
            <div class="row" style="margin-top: 4px">
              <button class="primary small" @click="saveEdit(s)">Save</button>
              <button class="ghost small" @click="editingId = null">Cancel</button>
            </div>
          </div>
          <div v-else class="diff-line added">+ {{ s.userEditedText || s.suggestedText }}</div>

          <p v-if="s.rationale" class="muted small">{{ s.rationale }}</p>

          <div class="row" v-if="editingId !== s.id">
            <button
              class="small"
              :class="{ primary: s.accepted === true }"
              @click="decide(s, true)"
            >
              Accept
            </button>
            <button
              class="small"
              :class="{ danger: s.accepted === false }"
              @click="decide(s, false)"
            >
              Reject
            </button>
            <button class="ghost small" @click="startEdit(s)">Edit</button>
          </div>
        </div>

        <div v-if="session.coverLetterDraft" class="card" style="margin-top: 12px">
          <h4>Cover letter draft</h4>
          <p style="white-space: pre-wrap">{{ session.coverLetterDraft }}</p>
        </div>

        <div class="row" style="margin-top: 12px">
          <button class="primary" @click="build" :disabled="building || acceptedCount === 0">
            <span v-if="building" class="spinner" />
            Build tailored resume
          </button>
          <span v-if="acceptedCount === 0" class="muted small">Accept at least one suggestion first.</span>
        </div>

        <div v-if="built" class="row" style="margin-top: 8px">
          <span>Built "{{ built.name }}" — </span>
          <a :href="api.exportResumeUrl(built.id, 'docx')" target="_blank">Download .docx</a>
          <a :href="api.exportResumeUrl(built.id, 'pdf')" target="_blank">Download .pdf</a>
        </div>
      </div>
    </template>

    <div v-else-if="session && session.status === 'Failed'" class="error-banner">
      Tailoring failed: {{ session.error }}
    </div>
  </div>
</template>

<style scoped>
.suggestion {
  border-top: 1px solid var(--border);
  padding: 10px 0;
}

.diff-line {
  padding: 4px 8px;
  border-radius: 4px;
  font-family: Consolas, monospace;
  font-size: 12.5px;
  white-space: pre-wrap;
}

.diff-line.removed {
  background: rgba(248, 81, 73, 0.1);
  color: #ff9691;
  text-decoration: line-through;
}

.diff-line.added {
  background: rgba(63, 185, 80, 0.1);
  color: #7ee787;
}

.honesty-note {
  margin-top: 8px;
  padding: 8px 10px;
  border-left: 3px solid var(--amber);
  background: rgba(210, 153, 34, 0.08);
  font-size: 13px;
}
</style>
