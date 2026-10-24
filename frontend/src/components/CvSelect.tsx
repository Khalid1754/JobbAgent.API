import type { Cv } from '../api'

// Välj vilket CV agenterna ska använda. Visas bara när det finns fler än ett.
export default function CvSelect({ cvs, value, onChange }: { cvs: Cv[]; value?: number; onChange: (id?: number) => void }) {
  if (cvs.length < 2) return null
  return (
    <label>
      CV
      <select value={value ?? ''} onChange={e => onChange(e.target.value ? Number(e.target.value) : undefined)}>
        {cvs.map(cv => <option key={cv.id} value={cv.isDefault ? '' : cv.id}>{cv.name}{cv.isDefault ? ' (standard)' : ''}</option>)}
      </select>
    </label>
  )
}
