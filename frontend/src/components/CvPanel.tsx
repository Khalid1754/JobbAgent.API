import { useState, type ChangeEvent } from 'react'
import { api, type Contact, type Cv } from '../api'

const CONTACT_FIELDS: { key: keyof Contact; label: string; type: string; placeholder: string }[] = [
  { key: 'fullName', label: 'Namn', type: 'text', placeholder: 'För- och efternamn' },
  { key: 'email', label: 'E-post', type: 'email', placeholder: 'namn@exempel.se' },
  { key: 'phone', label: 'Telefon', type: 'tel', placeholder: '070-123 45 67' },
  { key: 'city', label: 'Ort', type: 'text', placeholder: 'Stockholm' },
  { key: 'link', label: 'LinkedIn, GitHub eller portfolio', type: 'text', placeholder: 'linkedin.com/in/…' },
]

const NEW = 0

interface Props {
  cvs: Cv[]
  onChange: () => Promise<void>
}

// Dina CV-versioner: ladda upp en fil eller klistra in texten. Agenterna använder standard-CV:t om du inte väljer ett annat.
export default function CvPanel({ cvs, onChange }: Props) {
  const [selectedId, setSelectedId] = useState<number>(cvs.find(c => c.isDefault)?.id ?? NEW)
  const cv = cvs.find(c => c.id === selectedId)
  const [draft, setDraft] = useState<Cv>(() => cv ?? empty(cvs))
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)

  function choose(id: number) {
    setSelectedId(id)
    setDraft(cvs.find(c => c.id === id) ?? empty(cvs))
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
    if (file) run(() => api.uploadCv(file, cv?.id), `Läste in ${file.name}. Kontrollera texten nedan.`)
  }

  async function remove() {
    if (!cv) return
    await run(() => api.deleteCv(cv.id), `${cv.name} är borttaget.`)
    choose(cvs.find(c => c.id !== cv.id)?.id ?? NEW)
  }

  const changed = !cv || draft.name !== cv.name || draft.cvText !== cv.cvText ||
    CONTACT_FIELDS.some(f => (draft[f.key] ?? '') !== (cv[f.key] ?? ''))

  return (
    <div className="card form">
      <h2>Mina CV</h2>
      <p className="muted">
        Agenterna använder bara det som står i CV:t. Har du olika CV för olika slags jobb kan du spara flera och välja
        vilket som ska användas när du söker eller skriver brev.
      </p>
      <div className="tabs small wrap">
        {cvs.map(c => (
          <button key={c.id} type="button" className={c.id === selectedId ? 'active' : ''} onClick={() => choose(c.id)}>
            {c.name}{c.isDefault && ' ★'}
          </button>
        ))}
        <button type="button" className={selectedId === NEW ? 'active' : ''} onClick={() => choose(NEW)}>+ Nytt CV</button>
      </div>
      {cv && (
        <p className="meta">
          Senast sparat {new Date(cv.updatedAt).toLocaleString('sv-SE')}{cv.fileName && <> från {cv.fileName}</>}.
          {cv.isDefault ? ' Det här är ditt standard-CV.' : ''}
        </p>
      )}
      <label className="upload">
        <span className="button secondary">{busy ? 'Läser…' : cv ? 'Ersätt med fil (PDF, Word eller text)' : 'Ladda upp CV (PDF, Word eller text)'}</span>
        <input type="file" accept=".pdf,.docx,.txt,.md" onChange={upload} disabled={busy} hidden />
      </label>
      <label>
        Namn på CV:t
        <input value={draft.name} maxLength={60} onChange={e => setDraft(d => ({ ...d, name: e.target.value }))} placeholder="t.ex. Utvecklare" />
      </label>
      <label>
        {cv ? 'Texten i CV:t' : 'Eller klistra in texten'}
        <textarea rows={16} value={draft.cvText} onChange={e => setDraft(d => ({ ...d, cvText: e.target.value }))}
          placeholder="Namn, kontaktuppgifter, erfarenhet, utbildning, kompetenser…" />
      </label>
      <fieldset>
        <legend>Kontaktuppgifter i brevhuvudet (Word och PDF)</legend>
        <div className="row">
          {CONTACT_FIELDS.map(f => (
            <label key={f.key}>
              {f.label}
              <input type={f.type} value={draft[f.key] ?? ''} placeholder={f.placeholder}
                onChange={e => setDraft(d => ({ ...d, [f.key]: e.target.value }))} />
            </label>
          ))}
        </div>
      </fieldset>
      <div className="actions start">
        <button disabled={busy || draft.cvText.trim().length < 50 || !changed}
          onClick={() => run(() => (cv ? api.updateCv(draft) : api.createCv(draft)), 'CV:t är sparat.')}>
          Spara CV
        </button>
        {cv && !cv.isDefault && (
          <button className="secondary" disabled={busy} onClick={() => run(() => api.setDefaultCv(cv.id), `${cv.name} är nu standard.`)}>
            Använd som standard
          </button>
        )}
        {cv && cvs.length > 1 && <button className="danger" disabled={busy} onClick={remove}>Ta bort</button>}
        {message && <span className={message.ok ? 'ok' : 'error'}>{message.text}</span>}
      </div>
    </div>
  )
}

// Ett nytt CV får samma kontaktuppgifter som standard-CV:t.
function empty(cvs: Cv[]): Cv {
  const template = cvs.find(c => c.isDefault)
  return {
    id: NEW, name: cvs.length === 0 ? 'Mitt CV' : '', isDefault: false, cvText: '', updatedAt: '',
    fullName: template?.fullName, email: template?.email, phone: template?.phone, city: template?.city, link: template?.link,
  }
}
