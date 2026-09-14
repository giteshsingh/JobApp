<script setup>
import { computed, ref } from 'vue'

const props = defineProps({
  points: { type: Array, default: () => [] }, // [{date, count}]
  width: { type: Number, default: 280 },
  height: { type: Number, default: 56 },
})

const hoverIndex = ref(null)

const geometry = computed(() => {
  const pts = props.points
  const max = Math.max(1, ...pts.map((p) => p.count))
  const stepX = pts.length > 1 ? props.width / (pts.length - 1) : 0
  const coords = pts.map((p, i) => ({
    x: i * stepX,
    y: props.height - (p.count / max) * (props.height - 6) - 3,
    ...p,
  }))
  const line = coords.map((c) => `${c.x},${c.y}`).join(' ')
  const area = `0,${props.height} ${line} ${props.width},${props.height}`
  return { coords, line, area }
})
</script>

<template>
  <div class="spark" :style="{ width: width + 'px' }">
    <svg :width="width" :height="height" :viewBox="`0 0 ${width} ${height}`" role="img" aria-label="Jobs discovered per day, last 30 days">
      <polygon :points="geometry.area" fill="var(--cat-blue)" opacity="0.15" />
      <polyline :points="geometry.line" fill="none" stroke="var(--cat-blue)" stroke-width="2" stroke-linejoin="round" />
      <circle
        v-for="(c, i) in geometry.coords"
        :key="c.date"
        :cx="c.x"
        :cy="c.y"
        r="7"
        fill="transparent"
        @mouseenter="hoverIndex = i"
        @mouseleave="hoverIndex = null"
      />
      <circle
        v-if="hoverIndex !== null"
        :cx="geometry.coords[hoverIndex].x"
        :cy="geometry.coords[hoverIndex].y"
        r="3"
        fill="var(--cat-blue)"
        stroke="var(--surface-1)"
        stroke-width="2"
      />
    </svg>
    <div v-if="hoverIndex !== null" class="tip muted">
      {{ geometry.coords[hoverIndex].date }}: {{ geometry.coords[hoverIndex].count }} jobs
    </div>
    <div v-else class="tip muted">Jobs discovered, last 30 days</div>
  </div>
</template>

<style scoped>
.spark {
  display: inline-block;
}
.tip {
  font-size: 11px;
  margin-top: 2px;
}
</style>
