import { apiPath, request } from './request'
import type { DatabaseSyncOperationListResponse } from './types'

export async function getDatabaseSyncOperations(limit = 20) {
  return request<DatabaseSyncOperationListResponse>(apiPath(`/notifications/database-sync?limit=${limit}`))
}
