import type { BoardTaskCardResponse } from '../api/ronflowApi'

export type CompletedTasksVisibilityValue =
  | 'current-month'
  | 'week'
  | 'month'
  | 'two-months'
  | 'year'
  | 'all'

export type CompletedTasksVisibilityOption = {
  value: CompletedTasksVisibilityValue
  label: string
  shortLabel: string
}

export const COMPLETED_TASKS_VISIBILITY_STORAGE_KEY = 'ronflow.completedTasksVisibility'

export const completedTasksVisibilityOptions: CompletedTasksVisibilityOption[] = [
  { value: 'current-month', label: '本月', shortLabel: '本月' },
  { value: 'week', label: '一週內', shortLabel: '一週' },
  { value: 'month', label: '一個月內', shortLabel: '一月' },
  { value: 'two-months', label: '兩個月內', shortLabel: '兩月' },
  { value: 'year', label: '一年內', shortLabel: '一年' },
  { value: 'all', label: '永久', shortLabel: '永久' },
]

export function isCompletedTasksVisibilityValue(value: string): value is CompletedTasksVisibilityValue {
  return completedTasksVisibilityOptions.some((option) => option.value === value)
}

export function getCompletedTasksVisibilityIndex(value: CompletedTasksVisibilityValue) {
  return Math.max(0, completedTasksVisibilityOptions.findIndex((option) => option.value === value))
}

export function getCompletedTasksVisibilityLabel(value: CompletedTasksVisibilityValue) {
  return completedTasksVisibilityOptions.find((option) => option.value === value)?.label ?? '本月'
}

export function getCompletedTasksVisibilityValue(index: number): CompletedTasksVisibilityValue {
  const safeIndex = Math.min(completedTasksVisibilityOptions.length - 1, Math.max(0, index))
  return completedTasksVisibilityOptions[safeIndex].value
}

export function getCompletedTasksCutoffDate(
  value: CompletedTasksVisibilityValue,
  now = new Date(),
): Date | null {
  switch (value) {
    case 'current-month':
      return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1))
    case 'week':
      return addUtcDays(now, -7)
    case 'month':
      return addUtcMonths(now, -1)
    case 'two-months':
      return addUtcMonths(now, -2)
    case 'year':
      return addUtcYears(now, -1)
    case 'all':
      return null
  }
}

export function isCompletedTaskVisible(
  task: BoardTaskCardResponse,
  value: CompletedTasksVisibilityValue,
  now = new Date(),
) {
  const cutoff = getCompletedTasksCutoffDate(value, now)
  if (!cutoff || !task.completedAt) {
    return true
  }

  return new Date(task.completedAt).getTime() >= cutoff.getTime()
}

function addUtcDays(value: Date, days: number) {
  const nextValue = new Date(value)
  nextValue.setUTCDate(nextValue.getUTCDate() + days)
  return nextValue
}

function addUtcMonths(value: Date, months: number) {
  const nextValue = new Date(value)
  nextValue.setUTCMonth(nextValue.getUTCMonth() + months)
  return nextValue
}

function addUtcYears(value: Date, years: number) {
  const nextValue = new Date(value)
  nextValue.setUTCFullYear(nextValue.getUTCFullYear() + years)
  return nextValue
}
