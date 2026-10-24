import { useLang } from '../i18n'
import { isDark } from '../theme'

// Snabbknappar i sidhuvudet: mörkt/ljust läge och språk. Mer val finns under Profil.
export default function Preferences() {
  const { lang, setLang, theme, setTheme, t } = useLang()
  const dark = isDark(theme)

  function toggleTheme() {
    setTheme(dark ? 'light' : 'dark')
  }

  return (
    <div className="prefs">
      <button type="button" className="icon" onClick={toggleTheme}
        title={dark ? t('Byt till ljust läge') : t('Byt till mörkt läge')} aria-label={dark ? t('Byt till ljust läge') : t('Byt till mörkt läge')}>
        {dark ? '☀️' : '🌙'}
      </button>
      <button type="button" className="icon lang" onClick={() => setLang(lang === 'sv' ? 'en' : 'sv')}
        title={lang === 'sv' ? 'Switch to English' : 'Byt till svenska'} lang={lang === 'sv' ? 'en' : 'sv'}>
        {lang === 'sv' ? 'EN' : 'SV'}
      </button>
    </div>
  )
}
