<script setup>
import { ref, watch, onMounted } from 'vue'
import { api } from '../api.js'
import { profileStore } from '../profileStore.js'
import JobCard from '../components/JobCard.vue'
import ImportJobModal from '../components/ImportJobModal.vue'

const items = ref([])
const total = ref(0)
const counts = ref(null)
const page = ref(1)
const pageSize = 20
const loading = ref(false)
const errorMsg = ref(null)
const showImport = ref(false)

const filters = ref({
  postedWithinDays: 7,
  workModels: [], // subset of Remote/Hybrid/Onsite/Unknown
  minScore: null,
  search: '',
  withinOfficeDayTolerance: false,
  needsWorkModelReview: false,
  hideTracked: true,
  sort: 'score',
})

const WORK_MODELS = ['Remote', 'Hybrid', 'Onsite', 'Unknown']

function toggleWorkModel(m) {
  const idx = filters.value.workModels.indexOf(m)
  if (idx === -1) filters.value.workModels.push(m)
  else filters.value.workModels.splice(idx, 1)
  page.value = 1
}

async function load() {
  loading.value = true
  errorMsg.value = null
  try {
    const result = await api.jobs.list({
      profileId: profileStore.activeId,
      postedWithinDays: filters.value.postedWithinDays,
      workModels: filters.value.workModels.join(','),
      minScore: filters.value.minScore,
      search: filters.value.search,
      withinOfficeDayTolerance: filters.value.withinOfficeDayTolerance,
      needsWorkModelReview: filters.value.needsWorkModelReview,
      hideTracked: filters.value.hideTracked,
      sort: filters.value.sort,
      page: page.value,
      pageSize,
    })
    items.value = result.items
    total.value = result.total
    counts.value = result.counts
  } catch (e) {
    errorMsg.value = e.message
  } finally {
    loading.value = false
  }
}

onMounted(load)
watch(() => profileStore.activeId, () => { page.value = 1; load() })
watch(filters, () => { page.value = 1; load() }, { deep: true })
watch(page, load)

let searchDebounce
watch(() => filters.value.search, () => {
  clearTimeout(searchDebounce)
  searchDebounce = setTimeout(load, 350)
})

const totalPages = () => Math.max(1, Math.ceil(total.value / pageSize))
</script>

<template>
  <div class="page stack">
    <div class="row" style="justify-content: space-between">
      <h2 style="margin: 0">Jobs</h2>
      <button class="primary" @click="showImport = true">+ Import from URL/text</button>
    </div>

    <div class="card stack">
      <div class="row wrap">
        <label class="row">Posted within
          <select v-model.number="filters.postedWithinDays">
            <option :value="1">1 day</option>
            <option :value="3">3 days</option>
            <option :value="7">7 days</option>
            <option :value="14">14 days</option>
            <option :value="30">30 days</option>
          </select>
        </label>
        <label class="row">Min score
          <input v-model.number="filters.minScore" type="number" min="0" max="100" style="width: 64px" />
        </label>
        <label class="row">Sort
          <select v-model="filters.sort">
            <option value="score">Fit score</option>
            <option value="posted">Most recent</option>
            <option value="salary">Salary</option>
          </select>
        </label>
        <input v-model="filters.search" placeholder="Search title, company, location…" class="grow" />
      </div>

      <div class="row wrap">
        <button
          v-for="m in WORK_MODELS"
          :key="m"
          :class="{ primary: filters.workModels.includes(m) }"
          @click="toggleWorkModel(m)"
        >
          {{ m }}
        </button>
        <label class="row">
          <input type="checkbox" v-model="filters.withinOfficeDayTolerance" /> Within office-day tolerance
        </label>
        <label class="row">
          <input type="checkbox" v-model="filters.needsWorkModelReview" /> Needs review only
        </label>
        <label class="row">
          <input type="checkbox" v-model="filters.hideTracked" /> Hide already-tracked
        </label>
      </div>

      <div v-if="counts" class="row wrap muted">
        <span>{{ counts.freshTotal }} fresh</span>
        <span>· {{ counts.remote }} remote</span>
        <span>· {{ counts.hybridWithinTolerance }} hybrid-in-range</span>
        <span>· {{ counts.hybridOverTolerance }} hybrid-over</span>
        <span>· {{ counts.onsite }} onsite</span>
        <span>· {{ counts.unknownWorkModel }} unknown</span>
        <span>· {{ counts.unscored }} unscored</span>
      </div>
    </div>

    <div v-if="errorMsg" style="color: var(--status-critical)">{{ errorMsg }}</div>
    <div v-else-if="loading" class="muted">Loading…</div>
    <div v-else-if="!items.length" class="muted">No jobs match these filters.</div>

    <div class="stack">
      <JobCard v-for="job in items" :key="job.id" :job="job" :profile-id="profileStore.activeId" @changed="load" />
    </div>

    <div v-if="total > pageSize" class="row" style="justify-content: center">
      <button :disabled="page <= 1" @click="page--">← Prev</button>
      <span class="muted">Page {{ page }} of {{ totalPages() }}</span>
      <button :disabled="page >= totalPages()" @click="page++">Next →</button>
    </div>

    <ImportJobModal v-if="showImport" @close="showImport = false" @imported="load" />
  </div>
</template>
