<script setup>
import { computed } from 'vue'

// Tiny inline SVG sparkline — no charting library needed for one line.
const props = defineProps({
  points: { type: Array, required: true }, // [{ date, count }]
  width: { type: Number, default: 280 },
  height: { type: Number, default: 48 },
})

const path = computed(() => {
  const values = props.points.map((p) => p.count)
  if (values.length === 0) return ''

  const max = Math.max(1, ...values)
  const stepX = props.width / Math.max(1, values.length - 1)

  return values
    .map((v, i) => {
      const x = i * stepX
      const y = props.height - (v / max) * (props.height - 4) - 2
      return `${i === 0 ? 'M' : 'L'}${x.toFixed(1)},${y.toFixed(1)}`
    })
    .join(' ')
})
</script>

<template>
  <svg :width="width" :height="height" class="sparkline">
    <path :d="path" fill="none" stroke="var(--accent)" stroke-width="2" stroke-linecap="round" />
  </svg>
</template>

<style scoped>
.sparkline {
  display: block;
}
</style>
