<script setup>
import { computed } from 'vue'

const props = defineProps({
  score: { type: Number, default: null },
})

const spec = computed(() => {
  const s = props.score
  if (s == null) return { bg: 'transparent', fg: 'var(--text-muted)', label: '—', border: true }
  if (s < 40) return { bg: 'var(--seq-250)', fg: '#0b0b0b', label: String(s) }
  if (s < 60) return { bg: 'var(--seq-400)', fg: '#ffffff', label: String(s) }
  if (s < 80) return { bg: 'var(--seq-500)', fg: '#ffffff', label: String(s) }
  return { bg: 'var(--seq-600)', fg: '#ffffff', label: String(s) }
})
</script>

<template>
  <span
    class="badge tnum"
    :style="{
      background: spec.bg,
      color: spec.fg,
      border: spec.border ? '1px dashed var(--border)' : 'none',
    }"
    :title="score == null ? 'Not yet scored' : `Fit score ${score}/100`"
  >
    {{ spec.label }}
  </span>
</template>
