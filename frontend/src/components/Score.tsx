import { scoreClass } from '../agents'
import { useLang } from '../i18n'

export default function Score({ value, large }: { value: number; large?: boolean }) {
  const { t } = useLang()
  return <span className={`score ${scoreClass(value)} ${large ? 'large' : ''}`} title={t('Hur väl du matchar annonsen')}>{value} %</span>
}
