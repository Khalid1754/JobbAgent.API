import { scoreClass } from '../agents'

export default function Score({ value, large }: { value: number; large?: boolean }) {
  return <span className={`score ${scoreClass(value)} ${large ? 'large' : ''}`} title="Hur väl du matchar annonsen">{value} %</span>
}
