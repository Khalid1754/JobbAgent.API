import { useEffect, useState, type ReactNode } from 'react'
import { currentLang, locale, PrefsContext, setCurrentLang, translate, type Lang, type Prefs } from '../i18n'
import { applyTheme, loadTheme, saveTheme, type Theme } from '../theme'

// Håller valt språk och tema för hela appen.
export default function PrefsProvider({ children }: { children: ReactNode }) {
  const [lang, setLangState] = useState<Lang>(currentLang)
  const [theme, setThemeState] = useState<Theme>(loadTheme)
  const [, setSystemChanged] = useState(0)

  useEffect(() => {
    document.documentElement.lang = lang
  }, [lang])

  useEffect(() => applyTheme(theme), [theme])

  // Följer telefonens/datorns inställning när den ändras, t.ex. när den går över till mörkt på kvällen.
  useEffect(() => {
    if (typeof matchMedia !== 'function') return
    const media = matchMedia('(prefers-color-scheme: dark)')
    const onChange = () => setSystemChanged((n) => n + 1)
    media.addEventListener('change', onChange)
    return () => media.removeEventListener('change', onChange)
  }, [])

  function setLang(next: Lang) {
    setCurrentLang(next)
    setLangState(next)
  }

  function setTheme(next: Theme) {
    saveTheme(next)
    setThemeState(next)
  }

  const value: Prefs = {
    lang,
    setLang,
    theme,
    setTheme,
    t: (text, values) => translate(lang, text, values),
    locale: locale(lang),
  }
  return <PrefsContext.Provider value={value}>{children}</PrefsContext.Provider>
}
