import { computed, ref } from 'vue'

type ErrorMessageMapper = (error: unknown) => string

type UseApiResourceOptions<TData> = {
  clearDataOnExecute?: boolean
  createInitialData?: () => TData | null
  mapErrorMessage?: ErrorMessageMapper
}

export function useApiResource<TData, TArgs extends unknown[]>(
  executor: (...args: TArgs) => Promise<TData>,
  options: UseApiResourceOptions<TData> = {},
) {
  const data = ref<TData | null>(options.createInitialData?.() ?? null)
  const isLoading = ref(false)
  const error = ref<unknown | null>(null)

  const errorMessage = computed(() => {
    if (!error.value) {
      return ''
    }

    if (options.mapErrorMessage) {
      return options.mapErrorMessage(error.value)
    }

    return error.value instanceof Error ? error.value.message : '發生未預期錯誤。'
  })

  async function execute(...args: TArgs) {
    error.value = null

    if (options.clearDataOnExecute) {
      data.value = options.createInitialData?.() ?? null
    }

    isLoading.value = true

    try {
      const nextData = await executor(...args)
      data.value = nextData
      return nextData
    } catch (nextError) {
      error.value = nextError
      throw nextError
    } finally {
      isLoading.value = false
    }
  }

  function reset() {
    data.value = options.createInitialData?.() ?? null
    error.value = null
    isLoading.value = false
  }

  function setData(nextData: TData | null) {
    data.value = nextData
  }

  return {
    data,
    isLoading,
    error,
    errorMessage,
    execute,
    reset,
    setData,
  }
}