<script setup>
import { computed } from 'vue'

// Encodes the remote > 2-day-hybrid > onsite priority ordering the whole
// dashboard is built around, straight into a single glanceable chip.
const props = defineProps({
  workModel: { type: String, required: true },
  officeDaysPerWeek: { type: Number, default: null },
  confidence: { type: Number, default: 0 },
  tolerance: { type: Number, default: 2 },
})

const cls = computed(() => {
  if (props.workModel === 'Remote') return 'remote'
  if (props.workModel === 'Hybrid') {
    if (props.officeDaysPerWeek == null) return 'hybrid-ok'
    return props.officeDaysPerWeek <= props.tolerance ? 'hybrid-ok' : 'hybrid-over'
  }
  if (props.workModel === 'Onsite') return 'onsite'
  return 'unknown'
})

const label = computed(() => {
  if (props.workModel === 'Hybrid' && props.officeDaysPerWeek != null) {
    return `Hybrid · ${props.officeDaysPerWeek}d/wk`
  }
  if (props.workModel === 'Unknown') return 'Unclear'
  return props.workModel
})

const low = computed(() => props.workModel !== 'Unknown' && props.confidence > 0 && props.confidence < 0.7)
</script>

<template>
  <span class="tag" :class="cls" :title="low ? `Low confidence (${Math.round(confidence * 100)}%)` : ''">
    {{ label }}
    <span v-if="low">?</span>
  </span>
</template>
