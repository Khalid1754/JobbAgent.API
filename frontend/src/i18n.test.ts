import { describe, expect, it } from 'vitest'
import { translate } from './i18n'
import { en } from './i18n.en'

// Alla texter som skickas till t('…') eller msg('…') i appen.
const sources = import.meta.glob(['./**/*.{ts,tsx}', '!./**/*.test.ts', '!./i18n*'], { query: '?raw', import: 'default', eager: true })

function textsInApp() {
  const texts = new Set<string>()
  for (const source of Object.values(sources) as string[]) {
    for (const m of source.matchAll(/\b(?:t|msg)\(\s*'((?:[^'\\]|\\.)*)'/g)) texts.add(m[1])
  }
  return [...texts]
}

describe('översättningar', () => {
  it('har engelska för alla texter i appen', () => {
    const texts = textsInApp()
    expect(texts.length).toBeGreaterThan(50)
    expect(texts.filter((s) => !(s in en))).toEqual([])
  })

  it('behåller samma {platshållare} på engelska', () => {
    const holders = (s: string) => [...s.matchAll(/\{\w+\}/g)].map((m) => m[0]).sort()
    for (const [sv, text] of Object.entries(en)) expect(holders(text), sv).toEqual(holders(sv))
  })

  it('fyller i värden och faller tillbaka på svenska', () => {
    expect(translate('en', 'Sök inom {days} dagar', { days: 3 })).toBe('Apply within 3 days')
    expect(translate('sv', 'Sök inom {days} dagar', { days: 3 })).toBe('Sök inom 3 dagar')
    expect(translate('en', 'Text som saknas')).toBe('Text som saknas')
  })
})
