<script setup>
import { computed } from 'vue'

const props = defineProps({ score: { type: Number, default: null } })

const cls = computed(() => {
  if (props.score == null) return 'score-none'
  if (props.score >= 80) return 'score-90'
  if (props.score >= 60) return 'score-70'
  if (props.score >= 40) return 'score-50'
  return 'score-0'
})
</script>

<template>
  <span class="score-pill" :class="cls">{{ score ?? '—' }}</span>
</template>
