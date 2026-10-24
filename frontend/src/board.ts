import type { Application, Stage } from './api'

export const STAGES: { id: Stage; label: string; icon: string }[] = [
  { id: 'saved', label: 'Sparade', icon: '⭐' },
  { id: 'applied', label: 'Sökta', icon: '📨' },
  { id: 'interview', label: 'Intervju', icon: '🗣️' },
  { id: 'offer', label: 'Erbjudande', icon: '🎉' },
  { id: 'rejected', label: 'Nej tack', icon: '✖️' },
]

const DAY = 24 * 60 * 60 * 1000

// Hela dagar mellan två tidpunkter, räknat på kalenderdatum.
export function daysBetween(from: Date, to: Date) {
  const start = Date.UTC(from.getFullYear(), from.getMonth(), from.getDate())
  const end = Date.UTC(to.getFullYear(), to.getMonth(), to.getDate())
  return Math.round((end - start) / DAY)
}

// En kort rad om det viktigaste som händer härnäst för jobbet.
export function nextUp(a: Application, now = new Date()): { text: string; urgent: boolean } | null {
  if (a.stage === 'interview' && a.interviewAt) {
    const days = daysBetween(now, new Date(a.interviewAt))
    if (days >= 0) return { text: days === 0 ? 'Intervju idag' : days === 1 ? 'Intervju imorgon' : `Intervju om ${days} dagar`, urgent: days <= 2 }
  }
  if (a.stage === 'saved' && a.deadline) {
    const days = daysBetween(now, new Date(a.deadline))
    if (days < 0) return { text: 'Sista ansökningsdag har passerat', urgent: false }
    return { text: days === 0 ? 'Sista dag att söka idag' : `Sök inom ${days} dagar`, urgent: days <= 3 }
  }
  if (a.stage === 'applied' && a.appliedAt) {
    const days = daysBetween(new Date(a.appliedAt), now)
    if (days >= 14) return { text: `Sökt för ${days} dagar sedan, dags att höra av dig?`, urgent: false }
  }
  return null
}

export const countByStage = (applications: Application[]) =>
  Object.fromEntries(STAGES.map(s => [s.id, applications.filter(a => a.stage === s.id).length])) as Record<Stage, number>
