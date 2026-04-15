import DefaultTheme from 'vitepress/theme'
import RecipeTable from './components/RecipeTable.vue'

export default {
  extends: DefaultTheme,
  enhanceApp({ app }) {
    app.component('RecipeTable', RecipeTable)
  }
}
