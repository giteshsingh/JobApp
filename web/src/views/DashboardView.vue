<script setup>
import { ref, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import { useProfilesStore } from '../stores/profiles'
import { api } from '../api/client'
import ScorePill from '../components/ScorePill.vue'
import WorkModelTag from '../components/WorkModelTag.vue'
import Sparkline from '../components/Sparkline.vue'

const profiles = useProfilesStore()
const { activeProfileId, activeProfile } = storeToRefs(profiles)
const router = useRouter()

const summary = ref(null)
const loading = ref(false)
const error = ref(null)
const refreshing = ref(false)
const refreshMessage = ref(null)

async function load() {
  loading.value = true
  error.value = null
  try {
    summary.value = await api.summary(activeProfileId.value)
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

async function refreshNow() {
  refreshing.value = true
  refreshMessage.value = null
  try {
    const ingestion = await api.runIngestion()
    const triage = await api.runTriage()
    refreshMessage.value =
      `Fetched ${ingestion.fetched}, added ${ingestion.inserted} new jobs. ` +
      `Scored ${triage.scored}${triage.failed ? `, ${triage.failed} failed` : ''} ($${triage.costUsd.toFixed(4)}).`
    await load()
  } catch (e) {
    refreshMessage.value = e.message
  } finally {
    refreshing.value = false
  }
}

function openJob(id) {
  router.push({ path: '/jobs', query: { open: id } })
}

onMounted(load)
watch(activeProfileId, load)
</script>

<template>
  <div class="col">
    <div class="row between">
      <div>
        <h1>Overview</h1>
        <p class="muted" v-if="activeProfile">
          {{ activeProfile.name }} · last {{ activeProfile.postedWithinDays }} days · remote &gt; hybrid ≤
          {{ activeProfile.maxOfficeDaysPerWeek }}d &gt; onsite
        </p>
      </div>
      <div class="row">
        <button @click="refreshNow" :disabled="refreshing" class="primary">
          <span v-if="refreshing" class="spinner" />
          {{ refreshing ? 'Refreshing…' : 'Refresh now' }}
        </button>
      </div>
    </div>

    <p v-if="refreshMessage" class="muted small">{{ refreshMessage }}</p>
    <div v-if="error" class="error-banner">{{ error }}</div>

    <template v-if="summary">
      <div class="grid stat-grid">
        <div class="card stat">
          <div class="stat-value">{{ summary.counts.freshTotal }}</div>
          <div class="stat-label">Open roles (fresh window)</div>
        </div>
        <div class="card stat">
          <div class="stat-value" style="color: var(--green)">{{ summary.counts.remote }}</div>
          <div class="stat-label">Remote</div>
        </div>
        <div class="card stat">
          <div class="stat-value" style="color: var(--accent)">{{ summary.counts.hybridWithinTolerance }}</div>
          <div class="stat-label">Hybrid within tolerance</div>
        </div>
        <div class="card stat">
          <div class="stat-value" style="color: var(--amber)">{{ summary.counts.hybridOverTolerance }}</div>
          <div class="stat-label">Hybrid over tolerance</div>
        </div>
        <div class="card stat">
          <div class="stat-value" style="color: var(--red)">{{ summary.counts.onsite }}</div>
          <div class="stat-label">Onsite</div>
        </div>
        <div class="card stat">
          <div class="stat-value">{{ summary.counts.strongMatches }}</div>
          <div class="stat-label">Strong matches</div>
        </div>
      </div>

      <div class="grid two-col">
        <div class="card">
          <div class="row between">
            <h3>Top matches</h3>
            <RouterLink to="/jobs">View all →</RouterLink>
          </div>
          <div v-if="summary.topMatches.length === 0" class="empty-state small">
            No scored matches yet. Configure a Claude API key and click Refresh to triage new jobs.
          </div>
          <div v-else class="match-list">
            <div
              v-for="job in summary.topMatches"
              :key="job.id"
              class="match-row"
              @click="openJob(job.id)"
            >
              <ScorePill :score="job.overallScore" />
              <div class="match-info">
                <div class="match-title">{{ job.title }}</div>
                <div class="muted small">{{ job.company }} · {{ job.location || 'Location n/a' }}</div>
              </div>
              <WorkModelTag
                :work-model="job.workModel"
                :office-days-per-week="job.officeDaysPerWeek"
                :confidence="job.workModelConfidence"
                :tolerance="activeProfile?.maxOfficeDaysPerWeek"
              />
            </div>
          </div>
        </div>

        <div class="col">
          <div class="card">
            <h3>New postings, last 30 days</h3>
            <Sparkline v-if="summary.postedPerDay.length" :points="summary.postedPerDay" :width="320" />
            <p v-else class="muted small">No data yet.</p>
          </div>

          <div class="card">
            <h3>Claude spend this month</h3>
            <div class="stat-value">${{ summary.spendThisMonthUsd.toFixed(4) }}</div>
            <p class="muted small">{{ summary.counts.unscored }} jobs waiting on triage</p>
            <p v-if="summary.counts.needsWorkModelReview" class="muted small">
              {{ summary.counts.needsWorkModelReview }} jobs have an unclear work model — review them under Jobs.
            </p>
          </div>

          <div class="card">
            <h3>Sources this window</h3>
            <div class="row wrap">
              <span v-for="(count, source) in summary.bySource" :key="source" class="tag">
                {{ source }}: {{ count }}
              </span>
              <span v-if="Object.keys(summary.bySource).length === 0" class="muted small">No jobs yet.</span>
            </div>
          </div>

          <div class="card">
            <h3>Pipeline snapshot</h3>
            <div class="row wrap">
              <span v-for="(count, stage) in summary.pipeline" :key="stage" class="tag">
                {{ stage }}: {{ count }}
              </span>
              <span v-if="Object.keys(summary.pipeline).length === 0" class="muted small">
                Nothing tracked yet.
              </span>
            </div>
          </div>
        </div>
      </div>
    </template>

    <div v-else-if="loading" class="skeleton" style="height: 300px" />
  </div>
</template>

<style scoped>
.stat-grid {
  grid-template-columns: repeat(6, 1fr);
}

.stat {
  text-align: center;
}

.stat-value {
  font-size: 26px;
  font-weight: 700;
}

.stat-label {
  color: var(--text-dim);
  font-size: 12px;
  margin-top: 4px;
}

.two-col {
  grid-template-columns: 2fr 1fr;
  align-items: start;
}

.match-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-top: 8px;
}

.match-row {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px;
  border-radius: 8px;
  cursor: pointer;
}

.match-row:hover {
  background: var(--panel-2);
}

.match-info {
  flex: 1;
  min-width: 0;
}

.match-title {
  font-weight: 600;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

@media (max-width: 1100px) {
  .stat-grid {
    grid-template-columns: repeat(3, 1fr);
  }
  .two-col {
    grid-template-columns: 1fr;
  }
}
</style>
