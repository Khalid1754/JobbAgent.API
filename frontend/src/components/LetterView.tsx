import { useState } from 'react'
import { api, type Application, type NewApplication, type Run, type Stage } from '../api'
import { STAGES } from '../board'
import Score from './Score'

interface Props {
  run: Run
  application?: Application
  onSaveJob: (job: NewApplication) => Promise<void>
  onPrepareInterview: () => Promise<void>
}

export default function LetterView({ run, application, onSaveJob, onPrepareInterview }: Props) {
  const [letter, setLetter] = useState(run.letter ?? '')
  const [saved, setSaved] = useState(run.letter ?? '')
  const [message, setMessage] = useState<string | null>(null)
  const match = run.match

  async function save() {
    try {
      await api.saveLetter(run.id, letter)
      setSaved(letter)
      flash('Sparat!')
    } catch (err) {
      flash((err as Error).message)
    }
  }

  async function copy() {
    await navigator.clipboard.writeText(letter)
    flash('Kopierat!')
  }

  function download() {
    const url = URL.createObjectURL(new Blob([letter], { type: 'text/plain;charset=utf-8' }))
    const name = `Personligt brev - ${run.company ?? run.title}.txt`.replace(/[\\/:*?"<>|]/g, '')
    Object.assign(document.createElement('a'), { href: url, download: name }).click()
    URL.revokeObjectURL(url)
  }

  async function track(stage: Stage) {
    try {
      await onSaveJob({ letterRunId: run.id, stage })
      flash(stage === 'applied' ? 'Markerat som sökt på tavlan!' : 'Sparat på tavlan!')
    } catch (err) {
      flash((err as Error).message)
    }
  }

  function flash(text: string) {
    setMessage(text)
    setTimeout(() => setMessage(null), 2000)
  }

  return (
    <>
      {match && (
        <div className="card match">
          <Score value={match.score} large />
          <div>
            <p>{match.summary}</p>
            <div className="columns">
              <div>
                <h3>Det här passar</h3>
                <ul>{match.matches.map(m => <li key={m.requirement}><b>{m.requirement}:</b> {m.evidence}</li>)}</ul>
              </div>
              {match.gaps.length > 0 && (
                <div>
                  <h3>Det här saknas</h3>
                  <ul>{match.gaps.map(g => <li key={g}>{g}</li>)}</ul>
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {match?.cvTips && match.cvTips.length > 0 && (
        <div className="card">
          <h3>Anpassa ditt CV för jobbet</h3>
          <p className="muted">Matcharens förslag bygger bara på det som redan står i ditt CV.</p>
          <ul className="tips">
            {match.cvTips.map(t => (
              <li key={t.section + t.suggestion}>
                <span className="chip-label">{t.section}</span> {t.suggestion}
                {t.example && (
                  <div className="example">
                    <q>{t.example}</q>
                    <button className="secondary small" onClick={() => navigator.clipboard.writeText(t.example).then(() => flash('Kopierat!'))}>Kopiera</button>
                  </div>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="card">
        <div className="actions">
          <h3>Personligt brev</h3>
          {message && <span className="ok">{message}</span>}
          <button className="secondary" disabled={letter === saved || letter.trim().length === 0} onClick={save}>Spara ändringar</button>
          <button className="secondary" onClick={copy}>Kopiera</button>
          <button className="secondary" onClick={download}>Ladda ner</button>
        </div>
        <textarea className="letter" rows={22} value={letter} onChange={e => setLetter(e.target.value)} />
        <p className="meta">Läs alltid igenom brevet och gör det till ditt eget innan du skickar det.</p>
        <div className="actions start">
          {application ? (
            <span className="muted">På ansökningstavlan: {STAGES.find(s => s.id === application.stage)?.icon} {STAGES.find(s => s.id === application.stage)?.label}</span>
          ) : (
            <>
              <button onClick={() => track('applied')}>📨 Jag har sökt jobbet</button>
              <button className="secondary" onClick={() => track('saved')}>⭐ Spara på tavlan</button>
            </>
          )}
          <button className="secondary" onClick={() => onPrepareInterview().catch(err => flash((err as Error).message))}>🗣️ Förbered intervju</button>
        </div>
      </div>

      {run.adText && (
        <div className="card">
          <details>
            <summary><b>Annonsen</b>{run.adUrl && <> · <a href={run.adUrl} target="_blank" rel="noreferrer">öppna originalet</a></>}</summary>
            <p className="ad">{run.adText}</p>
          </details>
        </div>
      )}
    </>
  )
}
