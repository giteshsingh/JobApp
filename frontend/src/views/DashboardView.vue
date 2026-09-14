<script setup>
import { ref, watch, onMounted } from 'vue'
import { RouterLink } from 'vue-router'
import { api } from '../api.js'
import { profileStore } from '../profileStore.js'
import JobCard from '../components/JobCard.vue'
import Sparkline from '../components/Sparkline.vue'

const summary = ref(null)
const loading = ref(true)
const errorMsg = ref(null)
const ingestBusy = ref(false)
const ingestMsg = ref(null)

async function load() {
  loading.value = true
  errorMsg.value = null
  try {
    summary.value = await api.dashboard.summary(profileStore.activeId)
  } catch (e) {
    errorMsg.value = e.message
  } finally {
    loading.value = false
  }
}

async function refreshNow() {
  ingestBusy.value = true
  ingestMsg.value = null
  try {
    const report = await api.ingestion.run()
    const triage = await api.ingestion.triage()
    ingestMsg.value = `Fetched ${report.fetched}, added ${report.inserted} new · scored ${triage.scored} (skipped ${triage.skipped})`
    await load()
  } catch (e) {
    ingestMsg.value = e.message
  } finally {
    ingestBusy.value = false
  }
}

onMounted(load)
watch(() => profileStore.activeId, load)

const budgetPct = () => {
  const s = summary.value
  if (!s?.monthlyBudgetUsd) return null
  return Math.min(100, Math.round((Number(s.spendThisMonthUsd) / Number(s.monthlyBudgetUsd)) * 100))
}
</script>

<template>
  <div class="page stack">
    <div class="row" style="justify-content: space-between">
      <h2 style="margin: 0">Overview</h2>
      <div class="row">
        <span v-if="ingestMsg" class="muted">{{ ingestMsg }}</span>
        <button class="primary" :disabled="ingestBusy" @click="refreshNow">
          {{ ingestBusy ? 'Refreshing…' : 'Refresh now' }}
        </button>
      </div>
    </div>

    <div v-if="errorMsg" style="color: var(--status-critical)">{{ errorMsg }}</div>
    <div v-else-if="loading" class="muted">Loading…</div>

    <template v-else-if="summary">
      <div class="tiles">
        <div class="card tile">
          <div class="muted">Fresh (window)</div>
          <div class="hero tnum">{{ summary.counts.freshTotal }}</div>
        </div>
        <div class="card tile">
          <div class="muted">Remote</div>
          <div class="hero tnum" style="color: var(--status-good)">{{ summary.counts.remote }}</div>
        </div>
        <div class="card tile">
          <div class="muted">Hybrid (within tolerance)</div>
          <div class="hero tnum" style="color: var(--status-warning)">{{ summary.counts.hybridWithinTolerance }}</div>
        </div>
        <div class="card tile">
          <div class="muted">Onsite</div>
          <div class="hero tnum" style="color: var(--status-serious)">{{ summary.counts.onsite }}</div>
        </div>
        <div class="card tile">
          <div class="muted">Strong matches</div>
          <div class="hero tnum" style="color: var(--seq-400)">{{ summary.counts.strongMatches }}</div>
        </div>
        <div class="card tile">
          <div class="muted">Needs work-model review</div>
          <div class="hero tnum">{{ summary.counts.needsWorkModelReview }}</div>
        </div>
      </div>

      <div class="row wrap" style="align-items: stretch">
        <div class="card" style="flex: 2; min-width: 280px">
          <div class="row" style="justify-content: space-between">
            <strong>Discovery trend</strong>
            <span class="muted">{{ summary.totalJobs }} total jobs tracked</span>
          </div>
          <Sparkline :points="summary.postedPerDay" :width="520" />
        </div>

        <div class="card" style="flex: 1; min-width: 220px">
          <strong>Claude spend (month to date)</strong>
          <div class="hero tnum" style="margin: 6px 0">${{ Number(summary.spendThisMonthUsd).toFixed(2) }}</div>
          <div v-if="budgetPct() !== null" class="spend-meter">
            <div :style="{ width: budgetPct() + '%' }" />
          </div>
          <div class="muted" v-if="summary.lastIngestedAt">
            Last ingested {{ new Date(summary.lastIngestedAt).toLocaleString() }}
          </div>
        </div>
      </div>

      <div class="card">
        <strong>Pipeline</strong>
        <div class="row wrap" style="margin-top: 8px">
          <span v-for="(count, stage) in summary.pipeline" :key="stage" class="badge" style="background: var(--surface-2)">
            {{ stage }}: {{ count }}
          </span>
          <RouterLink to="/pipeline">Open board →</RouterLink>
        </div>
      </div>

      <div class="stack">
        <div class="row" style="justify-content: space-between">
          <strong>Top matches this week</strong>
          <RouterLink to="/jobs">See all jobs →</RouterLink>
        </div>
        <p v-if="!summary.topMatches.length" class="muted">
          No scored matches yet — hit "Refresh now" to fetch and triage new listings.
        </p>
        <JobCard
          v-for="job in summary.topMatches"
          :key="job.id"
          :job="job"
          :profile-id="profileStore.activeId"
          @changed="load"
        />
      </div>
    </template>
  </div>
</template>

<style scoped>
.tiles {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 12px;
}
.tile {
  padding: 14px 16px;
}
.hero {
  font-size: 28px;
  font-weight: 700;
}
</style>
