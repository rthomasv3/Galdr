import { defineStore } from 'pinia'
import { ref } from 'vue'

const THEMES = ['system', 'light', 'dark']
const THEME_CLASSES = ['light', 'dark']
const STORAGE_KEY = 'GaldrApp-theme'

function getSystemPreference() {
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

function resolveTheme(theme) {
  return theme === 'system' ? getSystemPreference() : theme
}

function applyTheme(resolved) {
  const html = document.documentElement
  html.classList.remove(...THEME_CLASSES)
  html.classList.add(resolved)
}

export const useThemeStore = defineStore('theme', () => {
  const saved = localStorage.getItem(STORAGE_KEY)
  const theme = ref(THEMES.includes(saved) ? saved : 'system')

  function setTheme(value) {
    if (THEMES.includes(value)) {
      theme.value = value
      localStorage.setItem(STORAGE_KEY, value)
      applyTheme(resolveTheme(value))
    }
  }

  function init() {
    applyTheme(resolveTheme(theme.value))

    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
      if (theme.value === 'system') {
        applyTheme(resolveTheme('system'))
      }
    })
  }

  return { theme, setTheme, init, THEMES }
})
