<script setup>
// Searchable table of the recipes the server ships, built from server/data/recipes.md.
import { ref, computed } from 'vue'
import { data as recipes } from './recipes.data.mjs'

const query = ref('')
const mode = ref('')
const modes = ['READ', 'WRITE-PDM', 'WRITE-PDM-NO-TRX', 'WRITE-DISK']

const fold = s => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase()

const shown = computed(() => {
  const words = fold(query.value).split(/\s+/).filter(Boolean)
  return recipes.filter(r => {
    if (mode.value && r.mode !== mode.value) return false
    const text = fold(r.name + ' ' + r.description)
    return words.every(w => text.includes(w))
  })
})

const count = m => recipes.filter(r => r.mode === m).length
</script>

<template>
  <div class="recipe-table">
    <div class="controls">
      <input v-model="query" type="search" placeholder="Chercher (nom ou description, ex. : export step)" aria-label="Chercher une recette" />
      <div class="modes" role="group" aria-label="Filtrer par mode">
        <button :class="{ on: mode === '' }" @click="mode = ''">Toutes ({{ recipes.length }})</button>
        <button v-for="m in modes" :key="m" :class="{ on: mode === m }" @click="mode = m">{{ m }} ({{ count(m) }})</button>
      </div>
    </div>
    <p class="summary">{{ shown.length }} recette(s)</p>
    <table>
      <thead>
        <tr><th>Recette</th><th>Mode</th><th>Description</th></tr>
      </thead>
      <tbody>
        <tr v-for="r in shown" :key="r.name">
          <td><code>{{ r.name }}</code></td>
          <td><span class="mode" :data-mode="r.mode">{{ r.mode }}</span></td>
          <td>{{ r.description }}</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.controls { display: flex; flex-direction: column; gap: 8px; margin: 16px 0 8px; }
input {
  width: 100%; padding: 8px 12px; font-size: 14px;
  border: 1px solid var(--vp-c-divider); border-radius: 6px;
  background: var(--vp-c-bg-soft); color: var(--vp-c-text-1);
}
.modes { display: flex; flex-wrap: wrap; gap: 6px; }
button {
  padding: 4px 10px; font-size: 12px; border-radius: 12px;
  border: 1px solid var(--vp-c-divider); color: var(--vp-c-text-2); background: transparent; cursor: pointer;
}
button.on { border-color: var(--vp-c-brand-1); color: var(--vp-c-brand-1); }
.summary { font-size: 13px; color: var(--vp-c-text-2); margin: 4px 0; }
table { display: table; width: 100%; font-size: 13px; }
td:first-child { white-space: nowrap; }
.mode { font-size: 11px; font-weight: 600; white-space: nowrap; }
.mode[data-mode='READ'] { color: var(--vp-c-green-1); }
.mode[data-mode='WRITE-PDM'], .mode[data-mode='WRITE-PDM-NO-TRX'] { color: var(--vp-c-yellow-1); }
.mode[data-mode='WRITE-DISK'] { color: var(--vp-c-indigo-1); }
</style>
