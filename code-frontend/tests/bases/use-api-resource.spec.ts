import { nextTick } from 'vue'
import { useApiResource } from '../../src/composables/useApiResource'

describe('useApiResource', () => {
  it('tracks loading and stores returned data', async () => {
    const resource = useApiResource(async (projectId: string) => {
      await Promise.resolve()
      return { projectId }
    })

    const execution = resource.execute('project-1')

    expect(resource.isLoading.value).toBe(true)

    await execution

    expect(resource.isLoading.value).toBe(false)
    expect(resource.data.value).toEqual({ projectId: 'project-1' })
    expect(resource.errorMessage.value).toBe('')
  })

  it('maps errors to a friendly message and can clear data on execute', async () => {
    const resource = useApiResource(
      async () => {
        throw new Error('network failed')
      },
      {
        clearDataOnExecute: true,
        createInitialData: () => 'seed',
        mapErrorMessage: () => '友善錯誤訊息',
      },
    )

    resource.setData('existing data')

    const execution = resource.execute().catch(() => null)
    await nextTick()

    expect(resource.data.value).toBe('seed')

    await execution

    expect(resource.errorMessage.value).toBe('友善錯誤訊息')
    expect(resource.isLoading.value).toBe(false)
  })
})