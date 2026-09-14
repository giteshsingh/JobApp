<script setup>
import { computed } from 'vue'

const props = defineProps({
  workModel: { type: String, required: true }, // Remote | Hybrid | Onsite | Unknown
  officeDaysPerWeek: { type: Number, default: null },
  confidence: { type: Number, default: 1 },
})

const spec = computed(() => {
  switch (props.workModel) {
    case 'Remote':
      return { color: 'var(--status-good)', label: 'Remote', icon: '🌐' }
    case 'Hybrid':
      return {
        color: 'var(--status-warning)',
        label: props.officeDaysPerWeek ? `Hybrid · ${props.officeDaysPerWeek}d/wk` : 'Hybrid',
        icon: '🏢',
        dark: true,
      }
    case 'Onsite':
      return { color: 'var(--status-serious)', label: 'Onsite', icon: '📍', dark: true }
    default:
      return { color: 'var(--text-muted)', label: 'Unknown', icon: '?' }
  }
})

const lowConfidence = computed(() => props.workModel !== 'Unknown' && props.confidence < 0.7)
</script>

<template>
  <span
    class="badge"
    :style="{
      background: spec.color,
      color: spec.dark ? '#1a1a19' : '#ffffff',
      opacity: lowConfidence ? 0.65 : 1,
      borderStyle: lowConfidence ? 'dashed' : 'solid',
    }"
    :title="lowConfidence ? 'Low-confidence classification — needs review' : spec.label"
  >
    <span aria-hidden="true">{{ spec.icon }}</span>
    {{ spec.label }}
    <span v-if="lowConfidence">·?</span>
  </span>
</template>
