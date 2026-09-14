<script setup>
import { ref, watch } from 'vue'
import { api } from '../api/client'
import ScorePill from './ScorePill.vue'
import WorkModelTag from './WorkModelTag.vue'
import TailoringPanel from './TailoringPanel.vue'

const props = defineProps({
  jobId: { type: Number, required: true },
  profileId: { type: Number, default: null },
  tolerance: { type: Number, default: 2 },
})
const emit = defineEmits(['close', 'changed'])

const job = ref(null)
const description = ref('')
const loading = ref(true)
const error = ref(null)
const deepRunning = ref(false)
const overrideModel = ref('Remote')
const overrideDays = ref(null)

async function load() {
  loading.value = true
  error.value = null
  try {
    ;[job.value, description.value] = await Promise.all([
      api.getJob(props.jobId, props.profileId),
      api.getJobDescription(props.jobId),
    ])
    overrideModel.value = job.value.workModel
    overrideDays.value = job.value.officeDaysPerWeek
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

watch(() => props.jobId, load, { immediate: true })

async function deepAnalyse() {
  deepRunning.value = true
  error.value = null
  try {
    await api.deepAnalyse(props.jobId, props.profileId)
    await load()
    emit('changed')
  } catch (e) {
    error.value = e.message
  } finally {
    deepRunning.value = false
  }
}

async function saveWorkModel() {
  await api.setWorkModel(props.jobId, overrideModel.value, overrideDays.value)
  await load()
  emit('changed')
}

async function track() {
  await api.trackJob(props.jobId, 'Saved')
  await load()
  emit('changed')
}
</script>

<template>
  <div class="overlay" @click.self="$emit('close')">
    <div class="drawer">
      <button class="ghost close-btn" @click="$emit('close')">✕</button>

      <div v-if="loading" class="skeleton" style="height: 200px" />
      <div v-else-if="error" class="error-banner">{{ error }}</div>

      <template v-else-if="job">
        <div class="row" style="align-items: flex-start">
          <ScorePill :score="job.overallScore" />
          <div style="flex: 1">
            <h2>{{ job.title }}</h2>
            <p class="muted">{{ job.company }} · {{ job.location || 'Location n/a' }} · {{ job.source }}</p>
          </div>
          <a v-if="job.url" :href="job.url" target="_blank"><button>Open posting ↗</button></a>
        </div>

        <div class="row wrap" style="margin: 10px 0">
          <WorkModelTag
            :work-model="job.workModel"
            :office-days-per-week="job.officeDaysPerWeek"
            :confidence="job.workModelConfidence"
            :tolerance="tolerance"
          />
          <span class="tag">Origin: {{ job.workModelOrigin }}</span>
          <span v-if="job.stage" class="tag">{{ job.stage }}</span>
          <button class="ghost small" v-if="!job.stage" @click="track">Save to pipeline</button>
        </div>

        <div v-if="job.workModelEvidence" class="muted small" style="margin-bottom: 10px">
          "{{ job.workModelEvidence }}"
        </div>

        <details class="card" style="margin-bottom: 12px">
          <summary>Correct the work model</summary>
          <div class="row" style="margin-top: 8px">
            <select v-model="overrideModel">
              <option>Remote</option>
              <option>Hybrid</option>
              <option>Onsite</option>
              <option>Unknown</option>
            </select>
            <input
              v-if="overrideModel === 'Hybrid'"
              type="number"
              min="0"
              max="5"
              v-model.number="overrideDays"
              placeholder="Days/week"
              style="width: 120px"
            />
            <button class="primary" @click="saveWorkModel">Save</button>
          </div>
        </details>

        <div v-if="job.isScored" class="card" style="margin-bottom: 12px">
          <div class="row wrap">
            <span class="tag">Skill {{ job.skillScore }}</span>
            <span class="tag">Seniority {{ job.seniorityScore }}</span>
            <span class="tag">Location {{ job.locationScore }}</span>
            <span class="tag">Freshness {{ job.freshnessScore }}</span>
            <span v-if="job.isDeepAnalysis" class="tag">Deep analysis</span>
          </div>
          <p v-if="job.rationale" style="margin-top: 8px">{{ job.rationale }}</p>

          <div v-if="job.matchedSkills?.length">
            <strong class="small">Matched:</strong>
            <span class="row wrap" style="display: inline-flex">
              <span v-for="s in job.matchedSkills" :key="s" class="tag remote">{{ s }}</span>
            </span>
          </div>
          <div v-if="job.missingMustHaves?.length" style="margin-top: 6px">
            <strong class="small">Missing must-haves:</strong>
            <span v-for="s in job.missingMustHaves" :key="s" class="tag onsite">{{ s }}</span>
          </div>
          <div v-if="job.missingResumeKeywords?.length" style="margin-top: 6px">
            <strong class="small">ATS gap (not in your resume):</strong>
            <span v-for="s in job.missingResumeKeywords" :key="s" class="tag hybrid-over">{{ s }}</span>
          </div>
          <div v-if="job.redFlags?.length" style="margin-top: 6px">
            <strong class="small">Red flags:</strong>
            <span v-for="s in job.redFlags" :key="s" class="tag onsite">{{ s }}</span>
          </div>
        </div>
        <div v-else class="muted small" style="margin-bottom: 12px">
          Not scored yet — runs automatically on the next background triage cycle.
        </div>

        <button @click="deepAnalyse" :disabled="deepRunning">
          <span v-if="deepRunning" class="spinner" />
          {{ deepRunning ? 'Analysing…' : 'Deep re-analyse (Sonnet)' }}
        </button>

        <details class="card" style="margin: 16px 0">
          <summary>Full description</summary>
          <pre class="description">{{ description }}</pre>
        </details>

        <div class="card">
          <h3>Customise resume for this job</h3>
          <TailoringPanel :job-id="job.id" />
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped>
.overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  justify-content: flex-end;
  z-index: 50;
}

.drawer {
  width: min(760px, 100%);
  background: var(--bg);
  height: 100%;
  overflow-y: auto;
  padding: 24px;
  position: relative;
  border-left: 1px solid var(--border);
}

.close-btn {
  position: absolute;
  top: 16px;
  right: 16px;
}

.description {
  white-space: pre-wrap;
  font-family: inherit;
  font-size: 13px;
  max-height: 400px;
  overflow-y: auto;
}

summary {
  cursor: pointer;
  font-weight: 600;
}
</style>
