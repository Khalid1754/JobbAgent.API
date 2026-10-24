// Ljust, mörkt eller samma som telefonen/datorn. Valet sparas i webbläsaren.
// index.html sätter temat redan innan React startar, så att sidan inte blinkar vit.

export type Theme = 'system' | 'light' | 'dark'

const STORAGE_KEY = 'jobbagent-theme'

export function loadTheme(): Theme {
  try {
    const saved = localStorage.getItem(STORAGE_KEY)
    return saved === 'light' || saved === 'dark' ? saved : 'system'
  } catch {
    return 'system'
  }
}

export function saveTheme(theme: Theme) {
  try {
    if (theme === 'system') localStorage.removeItem(STORAGE_KEY)
    else localStorage.setItem(STORAGE_KEY, theme)
  } catch {
    // inte viktigt om det inte går att spara
  }
  applyTheme(theme)
}

export function applyTheme(theme: Theme) {
  if (theme === 'system') delete document.documentElement.dataset.theme
  else document.documentElement.dataset.theme = theme
}

const systemDark = () => typeof matchMedia === 'function' && matchMedia('(prefers-color-scheme: dark)').matches

// Det tema som faktiskt syns just nu.
export const isDark = (theme: Theme) => (theme === 'system' ? systemDark() : theme === 'dark')
