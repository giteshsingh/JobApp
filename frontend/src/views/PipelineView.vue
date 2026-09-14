<script setup>
import { ref, onMounted, watch } from 'vue'
import { api } from '../api.js'
import { profileStore } from '../profileStore.js'

const STAGES = ['Saved', 'Applied', 'Screen', 'Interview', 'Offer', 'Rejected', 'Dismissed']
const STAGE_COLOR = {
  Saved: 'var(--cat-blue)',
  Applied: 'var(--cat-orange)',
  Screen: 'var(--cat-aqua)',
  Interview: 'var(--cat-yellow)',
  Offer: 'var(--status-good)',
  Rejected: 'var(--status-critical)',
  Dismissed: 'var(--text-muted)',
}

const board = ref({})
const loading = ref(true)
const errorMsg = ref(null)
const dragCard = ref(null)
const editing = ref(null) // application id currently expanded
const draft = ref({ notes: '', nextAction: '', nextActionAt: '' })

async function load() {
  loading.value = true
  errorMsg.value = null
  try {
    board.value = await api.pipeline.board(profileStore.activeId)
  } catch (e) {
    errorMsg.value = e.message
  } finally {
    loading.value = false
  }
}

onMounted(load)
watch(() => profileStore.activeId, load)

function onDragStart(card) {
  dragCard.value = card
}

async function onDrop(stage) {
  if (!dragCard.value || dragCard.value.stage === stage) {
    dragCard.value = null
    return
  }
  const card = dragCard.value
  dragCard.value = null
  try {
    await api.pipeline.move(card.id, stage, null, null)
    await load()
  } catch (e) {
    errorMsg.value = e.message
  }
}

function startEdit(card) {
  editing.value = card.id
  draft.value = {
    notes: card.notes ?? '',
    nextAction: card.nextAction ?? '',
    nextActionAt: card.nextActionAt ? card.nextActionAt.slice(0, 10) : '',
  }
}

async function saveEdit(card) {
  try {
    await api.pipeline.update(card.id, {
      notes: draft.value.notes,
      nextAction: draft.value.nextAction,
      nextActionAt: draft.value.nextActionAt ? new Date(draft.value.nextActionAt).toISOString() : null,
    })
    editing.value = null
    await load()
  } catch (e) {
    errorMsg.value = e.message
  }
}

async function removeCard(card) {
  try {
    await api.pipeline.remove(card.id)
    await load()
  } catch (e) {
    errorMsg.value = e.message
  }
}
</script>

<template>
  <div class="page stack">
    <h2 style="margin: 0">Pipeline</h2>
    <div v-if="errorMsg" style="color: var(--status-critical)">{{ errorMsg }}</div>
    <div v-else-if="loading" class="muted">Loading…</div>

    <div v-else class="board">
      <div
        v-for="stage in STAGES"
        :key="stage"
        class="column"
        @dragover.prevent
        @drop="onDrop(stage)"
      >
        <div class="column-head" :style="{ borderColor: STAGE_COLOR[stage] }">
          <span class="dot" :style="{ background: STAGE_COLOR[stage] }" />
          {{ stage }}
          <span class="muted">({{ (board[stage] ?? []).length }})</span>
        </div>

        <div
          v-for="card in board[stage] ?? []"
          :key="card.id"
          class="card kcard"
          draggable="true"
          @dragstart="onDragStart(card)"
        >
          <div class="row" style="justify-content: space-between">
            <strong>{{ card.title }}</strong>
            <button @click="removeCard(card)" title="Remove from pipeline">✕</button>
          </div>
          <div class="secondary">{{ card.company }}</div>
          <div class="muted">{{ card.location ?? '—' }} · {{ card.workModel }}</div>
          <div class="row wrap" style="margin-top: 4px">
            <span v-if="card.overallScore != null" class="badge" style="background: var(--seq-500); color: #fff">
              {{ card.overallScore }}
            </span>
            <span class="muted">{{ card.daysInStage }}d in stage</span>
          </div>
          <a v-if="card.url" :href="card.url" target="_blank" rel="noopener">Listing ↗</a>

          <template v-if="editing === card.id">
            <textarea v-model="draft.notes" rows="2" placeholder="Notes" />
            <input v-model="draft.nextAction" placeholder="Next action" />
            <input v-model="draft.nextActionAt" type="date" />
            <div class="row">
              <button class="primary" @click="saveEdit(card)">Save</button>
              <button @click="editing = null">Cancel</button>
            </div>
          </template>
          <template v-else>
            <div v-if="card.notes" class="muted">📝 {{ card.notes }}</div>
            <div v-if="card.nextAction" class="muted">
              → {{ card.nextAction }}<span v-if="card.nextActionAt"> by {{ new Date(card.nextActionAt).toLocaleDateString() }}</span>
            </div>
            <button @click="startEdit(card)">Edit</button>
          </template>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.board {
  display: flex;
  gap: 12px;
  overflow-x: auto;
  padding-bottom: 8px;
}
.column {
  flex: 0 0 240px;
  background: var(--surface-2);
  border-radius: 10px;
  padding: 10px;
  min-height: 200px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.column-head {
  font-weight: 700;
  padding-bottom: 8px;
  border-bottom: 2px solid;
  display: flex;
  align-items: center;
  gap: 6px;
}
.dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
}
.kcard {
  cursor: grab;
  padding: 10px;
}
.kcard textarea,
.kcard input {
  width: 100%;
  margin-top: 4px;
}
</style>
