import { mount } from '@vue/test-utils'
import BaseErrorState from '../../src/components/bases/BaseErrorState.vue'
import BaseLoadingState from '../../src/components/bases/BaseLoadingState.vue'

describe('shared state primitives', () => {
  it('renders loading state with a status role and message', () => {
    const wrapper = mount(BaseLoadingState, {
      props: {
        message: '載入中測試訊息',
      },
    })

    expect(wrapper.get('[data-testid="base-loading-state"]').attributes('role')).toBe('status')
    expect(wrapper.text()).toContain('載入中測試訊息')
  })

  it('renders error state with an alert role and page scope class', () => {
    const wrapper = mount(BaseErrorState, {
      props: {
        message: '錯誤測試訊息',
        scope: 'page',
      },
    })

    const errorState = wrapper.get('[data-testid="base-error-state"]')

    expect(errorState.attributes('role')).toBe('alert')
    expect(errorState.classes()).toContain('base-state-block-page')
    expect(wrapper.text()).toContain('錯誤測試訊息')
  })
})