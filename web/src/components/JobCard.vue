<script setup>
import ScorePill from './ScorePill.vue'
import WorkModelTag from './WorkModelTag.vue'

const props = defineProps({
  job: { type: Object, required: true },
  tolerance: { type: Number, default: 2 },
})

defineEmits(['open', 'dismiss', 'track'])

function fmtAge(days) {
  if (days == null) return ''
  if (days === 0) return 'today'
  if (days === 1) return '1 day ago'
  return `${days} days ago`
}

function fmtSalary(job) {
  if (!job.salaryMin && !job.salaryMax) return null
  const fmt = (n) => Math.round(n).toLocaleString()
  const range =
    job.salaryMin && job.salaryMax
      ? `${fmt(job.salaryMin)}–${fmt(job.salaryMax)}`
      : fmt(job.salaryMin || job.salaryMax)
  return `${range} ${job.salaryCurrency || ''}${job.salaryIsEstimate ? ' (est.)' : ''}`
}
</script>

<template>
  <div class="job-card" @click="$emit('open', job.id)">
    <ScorePill :score="job.overallScore" />

    <div class="job-main">
      <div class="row between">
        <div class="job-title">{{ job.title }}</div>
        <span class="muted small">{{ fmtAge(job.ageDays) }}</span>
      </div>
      <div class="muted small">
        {{ job.company }} · {{ job.location || 'Location n/a' }} · {{ job.source }}
      </div>

      <div class="row wrap" style="margin-top: 6px">
        <WorkModelTag
          :work-model="job.workModel"
          :office-days-per-week="job.officeDaysPerWeek"
          :confidence="job.workModelConfidence"
          :tolerance="tolerance"
        />
        <span v-if="fmtSalary(job)" class="tag">{{ fmtSalary(job) }}</span>
        <span v-if="job.stage" class="tag">{{ job.stage }}</span>
        <span v-if="job.redFlags?.length" class="tag onsite">⚠ {{ job.redFlags.length }} flag(s)</span>
      </div>

      <div v-if="job.matchedSkills?.length" class="row wrap" style="margin-top: 6px">
        <span v-for="s in job.matchedSkills.slice(0, 6)" :key="s" class="tag small">{{ s }}</span>
      </div>
    </div>

    <div class="job-actions" @click.stop>
      <button class="ghost small" v-if="!job.stage" @click="$emit('track', job.id)">Save</button>
      <button class="ghost small" @click="$emit('dismiss', job.id)">Dismiss</button>
    </div>
  </div>
</template>

<style scoped>
.job-card {
  display: flex;
  gap: 14px;
  padding: 14px;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: var(--panel);
  cursor: pointer;
  align-items: flex-start;
}

.job-card:hover {
  border-color: var(--accent);
}

.job-main {
  flex: 1;
  min-width: 0;
}

.job-title {
  font-weight: 600;
}

.job-actions {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
</style>
