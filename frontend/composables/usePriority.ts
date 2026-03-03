import { IssuePriority } from '~/types'

export const usePriority = () => {
  const priorityIcon = (p: IssuePriority | string): string => {
    const map: Record<string, string> = {
      [IssuePriority.Urgent]: '🔴',
      [IssuePriority.VeryHigh]: '🟠',
      [IssuePriority.High]: '🟡',
      [IssuePriority.Medium]: '🟢',
      [IssuePriority.Low]: '🔵',
      [IssuePriority.Unknown]: '🟣',
      [IssuePriority.NoPriority]: '⚪'
    }
    return map[p] ?? '⚪'
  }

  const priorityLabel = (p: IssuePriority | string): string => {
    const map: Record<string, string> = {
      [IssuePriority.Urgent]: 'Urgent',
      [IssuePriority.VeryHigh]: 'Very High',
      [IssuePriority.High]: 'High',
      [IssuePriority.Medium]: 'Medium',
      [IssuePriority.Low]: 'Low',
      [IssuePriority.Unknown]: 'Unknown',
      [IssuePriority.NoPriority]: 'No Priority'
    }
    return map[p] ?? String(p)
  }

  const priorityColor = (p: IssuePriority | string): string => {
    const map: Record<string, string> = {
      [IssuePriority.Urgent]: 'text-red-400',
      [IssuePriority.VeryHigh]: 'text-orange-400',
      [IssuePriority.High]: 'text-yellow-400',
      [IssuePriority.Medium]: 'text-green-400',
      [IssuePriority.Low]: 'text-blue-400',
      [IssuePriority.Unknown]: 'text-purple-400',
      [IssuePriority.NoPriority]: 'text-gray-500'
    }
    return map[p] ?? 'text-gray-500'
  }

  const priorities = [
    { value: IssuePriority.Urgent, label: `${priorityIcon(IssuePriority.Urgent)} Urgent` },
    { value: IssuePriority.VeryHigh, label: `${priorityIcon(IssuePriority.VeryHigh)} Very High` },
    { value: IssuePriority.High, label: `${priorityIcon(IssuePriority.High)} High` },
    { value: IssuePriority.Medium, label: `${priorityIcon(IssuePriority.Medium)} Medium` },
    { value: IssuePriority.Low, label: `${priorityIcon(IssuePriority.Low)} Low` },
    { value: IssuePriority.Unknown, label: `${priorityIcon(IssuePriority.Unknown)} Unknown` },
    { value: IssuePriority.NoPriority, label: `${priorityIcon(IssuePriority.NoPriority)} No Priority` }
  ]

  return { priorityIcon, priorityLabel, priorityColor, priorities }
}
