// Build-time data for <RecipeTable />: the recipes the server actually ships, read from
// server/data/recipes.md, which server/scripts/gen-recipes-catalog.py generates from the built
// server. Nothing here is maintained by hand.
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const CATALOG = fileURLToPath(new URL('../../../../server/data/recipes.md', import.meta.url))

const MODES = {
  R: 'READ',
  RW: 'WRITE-PDM',
  RD: 'WRITE-DISK',
  RN: 'WRITE-PDM-NO-TRX'
}

export function parseCatalog(text) {
  const recipes = []
  for (const block of text.replace(/\r\n/g, '\n').split(/^## /m).slice(1)) {
    const name = block.split('\n', 1)[0].trim()
    const pattern = /^Pattern: (\w+)/m.exec(block)
    const description = /^Description: (.*)$/m.exec(block)
    if (!name || !pattern) continue
    recipes.push({
      name,
      mode: MODES[pattern[1]] || pattern[1],
      description: description ? description[1].trim() : ''
    })
  }
  return recipes
}

export default {
  watch: [CATALOG],
  load() {
    return parseCatalog(readFileSync(CATALOG, 'utf-8'))
  }
}
