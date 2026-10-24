import type { Application, Stage } from './api'
import { msg, t } from './i18n'

export const STAGES: { id: Stage; label: string; icon: string }[] = [
  { id: 'saved', label: msg('Sparade'), icon: '⭐' },
  { id: 'applied', label: msg('Sökta'), icon: '📨' },
  { id: 'interview', label: msg('Intervju'), icon: '🗣️' },
  { id: 'offer', label: msg('Erbjudande'), icon: '🎉' },
  { id: 'rejected', label: msg('Nej tack'), icon: '✖️' },
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
    if (days >= 0) return { text: days === 0 ? t('Intervju idag') : days === 1 ? t('Intervju imorgon') : t('Intervju om {days} dagar', { days }), urgent: days <= 2 }
  }
  if (a.stage === 'saved' && a.deadline) {
    const days = daysBetween(now, new Date(a.deadline))
    if (days < 0) return { text: t('Sista ansökningsdag har passerat'), urgent: false }
    return { text: days === 0 ? t('Sista dag att söka idag') : t('Sök inom {days} dagar', { days }), urgent: days <= 3 }
  }
  if (a.stage === 'applied' && a.appliedAt) {
    const days = daysBetween(new Date(a.appliedAt), now)
    if (days >= 14) return { text: t('Sökt för {days} dagar sedan, dags att höra av dig?', { days }), urgent: false }
  }
  return null
}

export const countByStage = (applications: Application[]) =>
  Object.fromEntries(STAGES.map(s => [s.id, applications.filter(a => a.stage === s.id).length])) as Record<Stage, number>
