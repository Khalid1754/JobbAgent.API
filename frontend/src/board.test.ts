import { describe, expect, it } from 'vitest'
import type { Application } from './api'
import { countByStage, daysBetween, nextUp } from './board'

const app = (over: Partial<Application>): Application =>
  ({ id: '1', title: 'Utvecklare', stage: 'saved', createdAt: '', stageChangedAt: '', ...over })
const now = new Date(2026, 9, 24, 15, 0)

describe('board', () => {
  it('räknar hela kalenderdagar', () => {
    expect(daysBetween(new Date(2026, 9, 24, 23, 0), new Date(2026, 9, 25, 1, 0))).toBe(1)
  })

  it('påminner om sista ansökningsdag', () => {
    expect(nextUp(app({ deadline: '2026-10-26' }), now)).toEqual({ text: 'Sök inom 2 dagar', urgent: true })
    expect(nextUp(app({ deadline: '2026-10-20' }), now)?.text).toBe('Sista ansökningsdag har passerat')
  })

  it('visar när intervjun är', () => {
    expect(nextUp(app({ stage: 'interview', interviewAt: '2026-10-25T09:00:00' }), now)?.text).toBe('Intervju imorgon')
  })

  it('föreslår att höra av sig efter två veckor', () => {
    expect(nextUp(app({ stage: 'applied', appliedAt: '2026-10-01' }), now)?.text).toContain('23 dagar')
    expect(nextUp(app({ stage: 'applied', appliedAt: '2026-10-20' }), now)).toBeNull()
  })

  it('räknar jobben per steg', () => {
    expect(countByStage([app({}), app({ stage: 'offer' }), app({})])).toMatchObject({ saved: 2, offer: 1, applied: 0 })
  })
})
