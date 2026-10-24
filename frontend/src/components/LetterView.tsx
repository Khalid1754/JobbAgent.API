import { useState } from 'react'
import { api, type Run } from '../api'
import Score from './Score'

export default function LetterView({ run }: { run: Run }) {
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
