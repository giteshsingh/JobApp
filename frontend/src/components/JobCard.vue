<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import WorkModeBadge from './WorkModeBadge.vue'
import ScoreBadge from './ScoreBadge.vue'
import { api } from '../api.js'

const props = defineProps({
  job: { type: Object, required: true },
  profileId: { type: Number, default: null },
})
const emit = defineEmits(['changed'])

const router = useRouter()
const expanded = ref(false)
const description = ref(null)
const busy = ref(false)
const error = ref(null)

async function toggleExpand() {
  expanded.value = !expanded.value
  if (expanded.value && description.value == null) {
    try {
      description.value = await api.jobs.description(props.job.id)
    } catch (e) {
      description.value = '(failed to load description)'
    }
  }
}

async function deepAnalyse() {
  busy.value = true
  error.value = null
  try {
    await api.jobs.deepAnalyse(props.job.id, props.profileId)
    emit('changed')
  } catch (e) {
    error.value = e.message
  } finally {
    busy.value = false
  }
}

async function track(stage) {
  busy.value = true
  try {
    await api.pipeline.track(props.job.id, stage)
    emit('changed')
  } catch (e) {
    error.value = e.message
  } finally {
    busy.value = false
  }
}

async function dismiss() {
  busy.value = true
  try {
    await api.jobs.dismiss(props.job.id)
    emit('changed')
  } catch (e) {
    error.value = e.message
  } finally {
    busy.value = false
  }
}

function tailor() {
  router.push({ name: 'tailor', params: { jobId: props.job.id } })
}

function formatSalary(j) {
  if (j.salaryMin == null && j.salaryMax == null) return null
  const cur = j.salaryCurrency ?? ''
  const fmt = (n) => (n >= 100000 ? `${(n / 100000).toFixed(1)}L` : n.toLocaleString())
  const range =
    j.salaryMin != null && j.salaryMax != null
      ? `${fmt(j.salaryMin)}–${fmt(j.salaryMax)}`
      : fmt(j.salaryMin ?? j.salaryMax)
  return `${cur} ${range}${j.salaryIsEstimate ? ' (est.)' : ''}`
}
</script>

<template>
  <div class="card job-card">
    <div class="row wrap" style="align-items: flex-start">
      <div class="grow">
        <div class="row wrap" style="gap: 10px">
          <strong>{{ job.title }}</strong>
          <ScoreBadge :score="job.overallScore" />
          <WorkModeBadge
            :work-model="job.workModel"
            :office-days-per-week="job.officeDaysPerWeek"
            :confidence="job.workModelConfidence"
          />
          <span v-if="job.stage" class="badge" style="background: var(--surface-2); color: var(--text-secondary)">
            {{ job.stage }}
          </span>
        </div>
        <div class="secondary">
          {{ job.company }} · {{ job.location ?? 'Location n/a' }} · {{ job.source }}
          <span v-if="job.ageDays != null"> · {{ job.ageDays === 0 ? 'today' : `${job.ageDays}d ago` }}</span>
        </div>
        <div v-if="formatSalary(job)" class="muted">{{ formatSalary(job) }}</div>
      </div>

      <div class="row" style="flex-shrink: 0">
        <a :href="job.url" target="_blank" rel="noopener">Listing ↗</a>
        <button @click="toggleExpand">{{ expanded ? 'Hide' : 'Details' }}</button>
      </div>
    </div>

    <div v-if="expanded" class="stack expand">
      <div v-if="job.rationale" class="secondary">{{ job.rationale }}</div>

      <div v-if="job.matchedSkills?.length" class="row wrap">
        <span class="muted">Matched:</span>
        <span v-for="s in job.matchedSkills" :key="s" class="badge" style="background: var(--status-good); color: #fff">{{ s }}</span>
      </div>
      <div v-if="job.missingMustHaves?.length" class="row wrap">
        <span class="muted">Missing must-haves:</span>
        <span v-for="s in job.missingMustHaves" :key="s" class="badge" style="background: var(--status-critical); color: #fff">{{ s }}</span>
      </div>
      <div v-if="job.redFlags?.length" class="row wrap">
        <span class="muted">Flags:</span>
        <span v-for="s in job.redFlags" :key="s" class="badge" style="background: var(--status-warning); color: #1a1a19">{{ s }}</span>
      </div>

      <p v-if="description" class="secondary" style="white-space: pre-wrap; max-height: 260px; overflow-y: auto">
        {{ description }}
      </p>

      <div class="row wrap">
        <button v-if="!job.isDeepAnalysis" class="primary" :disabled="busy" @click="deepAnalyse">
          Deep-analyse fit
        </button>
        <button :disabled="busy" @click="tailor">Tailor my resume</button>
        <button :disabled="busy" @click="track('Saved')">Save</button>
        <button :disabled="busy" @click="track('Applied')">Mark applied</button>
        <button class="danger" :disabled="busy" @click="dismiss">Dismiss</button>
      </div>
      <div v-if="error" style="color: var(--status-critical)">{{ error }}</div>
    </div>
  </div>
</template>

<style scoped>
.job-card {
  border-radius: 10px;
}
.expand {
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid var(--border);
}
</style>
