<script setup>
import { ref, watch, onMounted } from 'vue'
import { api } from '../api.js'
import { profileStore } from '../profileStore.js'

const draft = ref(null)
const saving = ref(false)
const errorMsg = ref(null)
const savedMsg = ref(null)
const spend = ref(null)

function toDraft(p) {
  return {
    ...p,
    targetTitlesText: (p.targetTitles ?? []).join(', '),
    excludeKeywordsText: (p.excludeKeywords ?? []).join(', '),
    mustHaveSkillsText: (p.mustHaveSkills ?? []).join(', '),
    niceToHaveSkillsText: (p.niceToHaveSkills ?? []).join(', '),
    countryCodesText: (p.countryCodes ?? []).join(', '),
    acceptableCitiesText: (p.acceptableCities ?? []).join(', '),
  }
}

function loadDraftFromActive() {
  if (profileStore.active) draft.value = toDraft(profileStore.active)
}

onMounted(async () => {
  if (!profileStore.loaded) await profileStore.load()
  loadDraftFromActive()
  try {
    spend.value = await api.ingestion.spend()
  } catch {
    /* non-fatal */
  }
})
watch(() => profileStore.activeId, loadDraftFromActive)

function splitList(text) {
  return (text ?? '')
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean)
}

async function save() {
  saving.value = true
  errorMsg.value = null
  savedMsg.value = null
  const dto = {
    ...draft.value,
    targetTitles: splitList(draft.value.targetTitlesText),
    excludeKeywords: splitList(draft.value.excludeKeywordsText),
    mustHaveSkills: splitList(draft.value.mustHaveSkillsText),
    niceToHaveSkills: splitList(draft.value.niceToHaveSkillsText),
    countryCodes: splitList(draft.value.countryCodesText),
    acceptableCities: splitList(draft.value.acceptableCitiesText),
  }
  try {
    await api.profiles.update(draft.value.id, dto)
    await profileStore.load()
    savedMsg.value = 'Saved.'
  } catch (e) {
    errorMsg.value = e.message
  } finally {
    saving.value = false
  }
}

async function addProfile() {
  const created = await api.profiles.create({
    id: 0,
    name: 'New profile',
    isActive: false,
    targetTitles: [],
    excludeKeywords: [],
    mustHaveSkills: [],
    niceToHaveSkills: [],
    minYearsExperience: null,
    maxYearsExperience: null,
    countryCodes: ['in'],
    homeCity: null,
    acceptableCities: [],
    allowGlobalRemote: true,
    openToRelocation: false,
    trackVisaSponsorship: false,
    remoteWeight: 100,
    hybridWeight: 90,
    hybridOverLimitWeight: 45,
    onsiteWeight: 25,
    maxOfficeDaysPerWeek: 2,
    excludeOnsite: false,
    minSalary: null,
    salaryCurrency: 'INR',
    includeUndisclosedSalary: true,
    postedWithinDays: 7,
    minOverallScore: 55,
    skillFitWeight: 55,
    locationFitWeight: 30,
    freshnessWeight: 15,
  })
  await profileStore.load()
  profileStore.activeId = created.id
}

async function deleteProfile() {
  if (!draft.value) return
  if (!confirm(`Delete search profile "${draft.value.name}"?`)) return
  try {
    await api.profiles.remove(draft.value.id)
    profileStore.activeId = null
    await profileStore.load()
    loadDraftFromActive()
  } catch (e) {
    errorMsg.value = e.message
  }
}
</script>

<template>
  <div class="page stack">
    <div class="row" style="justify-content: space-between">
      <h2 style="margin: 0">Search profile settings</h2>
      <div class="row">
        <button @click="addProfile">+ New profile</button>
        <button class="danger" @click="deleteProfile">Delete this profile</button>
      </div>
    </div>

    <div v-if="spend" class="card muted">
      Claude spend this month: ${{ Number(spend.monthToDateUsd).toFixed(2) }}
    </div>

    <div v-if="draft" class="stack">
      <div class="card stack">
        <strong>Profile</strong>
        <label>Name <input v-model="draft.name" /></label>
        <label class="row"><input type="checkbox" v-model="draft.isActive" /> Active (polled by the background worker)</label>
      </div>

      <div class="card stack">
        <strong>Target roles</strong>
        <label>Target titles (comma-separated)
          <input v-model="draft.targetTitlesText" placeholder="Project Lead, Engineering Manager, Solution Architect" />
        </label>
        <label>Exclude keywords <input v-model="draft.excludeKeywordsText" placeholder="intern, PHP" /></label>
        <label>Must-have skills <input v-model="draft.mustHaveSkillsText" placeholder=".NET Core, Angular, AWS" /></label>
        <label>Nice-to-have skills <input v-model="draft.niceToHaveSkillsText" /></label>
        <div class="row">
          <label class="grow">Min years experience <input v-model.number="draft.minYearsExperience" type="number" /></label>
          <label class="grow">Max years experience <input v-model.number="draft.maxYearsExperience" type="number" /></label>
        </div>
      </div>

      <div class="card stack">
        <strong>Location</strong>
        <label>Country codes (comma-separated) <input v-model="draft.countryCodesText" placeholder="in" /></label>
        <label>Home city <input v-model="draft.homeCity" placeholder="Bangalore" /></label>
        <label>Other acceptable cities <input v-model="draft.acceptableCitiesText" placeholder="Pune, Hyderabad, NCR" /></label>
        <label class="row"><input type="checkbox" v-model="draft.allowGlobalRemote" /> Include fully-remote roles anywhere in the world</label>
        <label class="row"><input type="checkbox" v-model="draft.openToRelocation" /> Open to relocating countries</label>
        <label class="row"><input type="checkbox" v-model="draft.trackVisaSponsorship" /> Ask Claude to flag visa sponsorship mentions</label>
      </div>

      <div class="card stack">
        <strong>Work-model priority</strong>
        <label>Remote weight ({{ draft.remoteWeight }}) <input v-model.number="draft.remoteWeight" type="range" min="0" max="100" /></label>
        <label>Hybrid, within tolerance ({{ draft.hybridWeight }}) <input v-model.number="draft.hybridWeight" type="range" min="0" max="100" /></label>
        <label>Hybrid, over tolerance ({{ draft.hybridOverLimitWeight }}) <input v-model.number="draft.hybridOverLimitWeight" type="range" min="0" max="100" /></label>
        <label>Onsite ({{ draft.onsiteWeight }}) <input v-model.number="draft.onsiteWeight" type="range" min="0" max="100" /></label>
        <label>Max office days/week you'll accept <input v-model.number="draft.maxOfficeDaysPerWeek" type="number" min="0" max="5" style="width: 60px" /></label>
        <label class="row"><input type="checkbox" v-model="draft.excludeOnsite" /> Hide fully onsite roles entirely</label>
      </div>

      <div class="card stack">
        <strong>Compensation</strong>
        <div class="row">
          <label class="grow">Minimum salary floor <input v-model.number="draft.minSalary" type="number" placeholder="no floor" /></label>
          <label>Currency <input v-model="draft.salaryCurrency" style="width: 70px" /></label>
        </div>
        <label class="row"><input type="checkbox" v-model="draft.includeUndisclosedSalary" /> Include jobs with undisclosed salary (recommended)</label>
      </div>

      <div class="card stack">
        <strong>Freshness &amp; scoring</strong>
        <label>Default "recent" window (days) <input v-model.number="draft.postedWithinDays" type="number" style="width: 70px" /></label>
        <label>Minimum overall score to count as a "strong match" <input v-model.number="draft.minOverallScore" type="number" style="width: 70px" /></label>
        <label>Skill-fit weight <input v-model.number="draft.skillFitWeight" type="number" style="width: 70px" /></label>
        <label>Location-fit weight <input v-model.number="draft.locationFitWeight" type="number" style="width: 70px" /></label>
        <label>Freshness weight <input v-model.number="draft.freshnessWeight" type="number" style="width: 70px" /></label>
      </div>

      <div class="row">
        <button class="primary" :disabled="saving" @click="save">{{ saving ? 'Saving…' : 'Save profile' }}</button>
        <span v-if="savedMsg" style="color: var(--status-good)">{{ savedMsg }}</span>
        <span v-if="errorMsg" style="color: var(--status-critical)">{{ errorMsg }}</span>
      </div>
    </div>
    <p v-else class="muted">No search profile yet — click "+ New profile".</p>
  </div>
</template>

<style scoped>
label {
  display: block;
}
label input,
label select {
  margin-left: 8px;
}
</style>
