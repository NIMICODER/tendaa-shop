export function formatAge(isoDate: string): string {
  const minutes = Math.max(0, Math.floor((Date.now() - new Date(isoDate).getTime()) / 60000))
  if (minutes < 60) {
    return `${minutes}m`
  }
  const hours = Math.floor(minutes / 60)
  if (hours < 24) {
    return `${hours}h`
  }
  return `${Math.floor(hours / 24)}d`
}
