import { createContext, useContext } from 'react'
import { en } from './i18n.en'
import type { Theme } from './theme'

// Svenska är standard. Texterna skrivs på svenska i koden och slås upp i i18n.en.ts när engelska är valt,
// så en text som saknas där visas på svenska i stället för att försvinna.

export type Lang = 'sv' | 'en'

const STORAGE_KEY = 'jobbagent-lang'

function loadLang(): Lang {
  try {
    return localStorage.getItem(STORAGE_KEY) === 'en' ? 'en' : 'sv'
  } catch {
    return 'sv'
  }
}

let current: Lang = loadLang()

// Används av api.ts, som inte är en React-komponent.
export const currentLang = () => current

export function setCurrentLang(lang: Lang) {
  current = lang
  try {
    localStorage.setItem(STORAGE_KEY, lang)
  } catch {
    // inte viktigt om det inte går att spara
  }
}

// Markerar en text som ska översättas senare, t.ex. i en lista utanför komponenten.
export const msg = (text: string) => text

// t('Hej {name}', { name }) byter ut {name} mot värdet.
export function translate(lang: Lang, text: string, values?: Record<string, string | number>) {
  const result = lang === 'en' ? (en[text] ?? text) : text
  return values ? result.replace(/\{(\w+)\}/g, (m, key: string) => (key in values ? String(values[key]) : m)) : result
}

export const t = (text: string, values?: Record<string, string | number>) => translate(current, text, values)

// Datum och tal visas på svenskt eller brittiskt sätt (dag före månad i båda).
export const locale = (lang: Lang = current) => (lang === 'en' ? 'en-GB' : 'sv-SE')

export interface Prefs {
  lang: Lang
  setLang: (lang: Lang) => void
  theme: Theme
  setTheme: (theme: Theme) => void
  t: typeof t
  locale: string
}

export const PrefsContext = createContext<Prefs | null>(null)

export function useLang() {
  const prefs = useContext(PrefsContext)
  if (!prefs) throw new Error('useLang måste användas inuti PrefsProvider')
  return prefs
}
