import { useState, type ChangeEvent } from 'react'
import { api, type Contact, type Cv } from '../api'
import { msg, useLang } from '../i18n'

const CONTACT_FIELDS: { key: keyof Contact; label: string; type: string; placeholder: string }[] = [
  { key: 'fullName', label: msg('Namn'), type: 'text', placeholder: msg('För- och efternamn') },
  { key: 'email', label: msg('E-post'), type: 'email', placeholder: msg('namn@exempel.se') },
  { key: 'phone', label: msg('Telefon'), type: 'tel', placeholder: '070-123 45 67' },
  { key: 'city', label: msg('Ort'), type: 'text', placeholder: 'Stockholm' },
  { key: 'link', label: msg('LinkedIn, GitHub eller portfolio'), type: 'text', placeholder: 'linkedin.com/in/…' },
]

const NEW = 0

interface Props {
  cvs: Cv[]
  onChange: () => Promise<void>
}

// Dina CV-versioner: ladda upp en fil eller klistra in texten. Agenterna använder standard-CV:t om du inte väljer ett annat.
export default function CvPanel({ cvs, onChange }: Props) {
  const { t, locale } = useLang()
  const [selectedId, setSelectedId] = useState<number>(cvs.find(c => c.isDefault)?.id ?? NEW)
  const cv = cvs.find(c => c.id === selectedId)
  const [draft, setDraft] = useState<Cv>(() => cv ?? empty(cvs, t))
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)

  function choose(id: number) {
    setSelectedId(id)
    setDraft(cvs.find(c => c.id === id) ?? empty(cvs, t))
    setMessage(null)
  }

  async function run(action: () => Promise<Cv | void>, done: string) {
    setBusy(true)
    setMessage(null)
    try {
      const saved = await action()
      await onChange()
      if (saved) {
        setSelectedId(saved.id)
        setDraft(saved)
      }
      setMessage({ ok: true, text: done })
    } catch (err) {
      setMessage({ ok: false, text: (err as Error).message })
    } finally {
      setBusy(false)
    }
  }

  function upload(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (file) run(() => api.uploadCv(file, cv?.id), t('Läste in {file}. Kontrollera texten nedan.', { file: file.name }))
  }

  async function remove() {
    if (!cv) return
    await run(() => api.deleteCv(cv.id), t('{name} är borttaget.', { name: cv.name }))
    choose(cvs.find(c => c.id !== cv.id)?.id ?? NEW)
  }

  const changed = !cv || draft.name !== cv.name || draft.cvText !== cv.cvText ||
    CONTACT_FIELDS.some(f => (draft[f.key] ?? '') !== (cv[f.key] ?? ''))

  return (
    <div className="card form">
      <h2>{t('Mina CV')}</h2>
      <p className="muted">
        {t('Agenterna använder bara det som står i CV:t. Har du olika CV för olika slags jobb kan du spara flera och välja vilket som ska användas när du söker eller skriver brev.')}
      </p>
      <div className="tabs small wrap">
        {cvs.map(c => (
          <button key={c.id} type="button" className={c.id === selectedId ? 'active' : ''} onClick={() => choose(c.id)}>
            {c.name}{c.isDefault && ' ★'}
          </button>
        ))}
        <button type="button" className={selectedId === NEW ? 'active' : ''} onClick={() => choose(NEW)}>+ {t('Nytt CV')}</button>
      </div>
      {cv && (
        <p className="meta">
          {cv.fileName
            ? t('Senast sparat {date} från {file}.', { date: new Date(cv.updatedAt).toLocaleString(locale), file: cv.fileName })
            : t('Senast sparat {date}.', { date: new Date(cv.updatedAt).toLocaleString(locale) })}
          {cv.isDefault ? ' ' + t('Det här är ditt standard-CV.') : ''}
        </p>
      )}
      <label className="upload">
        <span className="button secondary">{busy ? t('Läser…') : cv ? t('Ersätt med fil (PDF, Word eller text)') : t('Ladda upp CV (PDF, Word eller text)')}</span>
        <input type="file" accept=".pdf,.docx,.txt,.md" onChange={upload} disabled={busy} hidden />
      </label>
      <label>
        {t('Namn på CV:t')}
        <input value={draft.name} maxLength={60} onChange={e => setDraft(d => ({ ...d, name: e.target.value }))} placeholder={t('t.ex. Utvecklare')} />
      </label>
      <label>
        {cv ? t('Texten i CV:t') : t('Eller klistra in texten')}
        <textarea rows={16} value={draft.cvText} onChange={e => setDraft(d => ({ ...d, cvText: e.target.value }))}
          placeholder={t('Namn, kontaktuppgifter, erfarenhet, utbildning, kompetenser…')} />
      </label>
      <fieldset>
        <legend>{t('Kontaktuppgifter i brevhuvudet (Word och PDF)')}</legend>
        <div className="row">
          {CONTACT_FIELDS.map(f => (
            <label key={f.key}>
              {t(f.label)}
              <input type={f.type} value={draft[f.key] ?? ''} placeholder={t(f.placeholder)}
                onChange={e => setDraft(d => ({ ...d, [f.key]: e.target.value }))} />
            </label>
          ))}
        </div>
      </fieldset>
      <div className="actions start">
        <button disabled={busy || draft.cvText.trim().length < 50 || !changed}
          onClick={() => run(() => (cv ? api.updateCv(draft) : api.createCv(draft)), t('CV:t är sparat.'))}>
          {t('Spara CV')}
        </button>
        {cv && !cv.isDefault && (
          <button className="secondary" disabled={busy} onClick={() => run(() => api.setDefaultCv(cv.id), t('{name} är nu standard.', { name: cv.name }))}>
            {t('Använd som standard')}
          </button>
        )}
        {cv && cvs.length > 1 && <button className="danger" disabled={busy} onClick={remove}>{t('Ta bort')}</button>}
        {message && <span className={message.ok ? 'ok' : 'error'}>{message.text}</span>}
      </div>
    </div>
  )
}

// Ett nytt CV får samma kontaktuppgifter som standard-CV:t.
function empty(cvs: Cv[], t: (text: string) => string): Cv {
  const template = cvs.find(c => c.isDefault)
  return {
    id: NEW, name: cvs.length === 0 ? t('Mitt CV') : '', isDefault: false, cvText: '', updatedAt: '',
    fullName: template?.fullName, email: template?.email, phone: template?.phone, city: template?.city, link: template?.link,
  }
}
