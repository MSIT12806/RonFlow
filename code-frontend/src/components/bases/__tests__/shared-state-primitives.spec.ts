import { mount } from '@vue/test-utils'
import ApiCommandResourceView from '../ApiCommandResourceView.vue'
import AsyncStateBoundary from '../AsyncStateBoundary.vue'
import BaseErrorState from '../BaseErrorState.vue'
import BaseLoadingState from '../BaseLoadingState.vue'

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

  it('renders the default slot when there is no loading or error', () => {
    const wrapper = mount(AsyncStateBoundary, {
      props: {
        isLoading: false,
        errorMessage: '',
        loadingMessage: '載入中測試訊息',
      },
      slots: {
        default: '<div data-testid="boundary-content">內容已載入</div>',
      },
    })

    expect(wrapper.get('[data-testid="boundary-content"]').text()).toBe('內容已載入')
  })

  it('renders the shared loading and error primitives through AsyncStateBoundary', () => {
    const loadingWrapper = mount(AsyncStateBoundary, {
      props: {
        isLoading: true,
        errorMessage: '',
        loadingMessage: '載入中測試訊息',
      },
    })

    expect(loadingWrapper.get('[data-testid="base-loading-state"]').text()).toContain('載入中測試訊息')

    const errorWrapper = mount(AsyncStateBoundary, {
      props: {
        isLoading: false,
        errorMessage: '錯誤測試訊息',
        loadingMessage: '載入中測試訊息',
        errorScope: 'page',
      },
    })

    expect(errorWrapper.get('[data-testid="base-error-state"]').text()).toContain('錯誤測試訊息')
  })

  it('keeps command form content visible while showing submitting feedback', () => {
    const wrapper = mount(ApiCommandResourceView, {
      props: {
        isSubmitting: true,
        errorMessage: '',
        submittingMessage: '處理中測試訊息',
      },
      slots: {
        default: '<button type="button" data-testid="command-content">送出內容</button>',
      },
    })

    expect(wrapper.get('[data-testid="command-content"]').text()).toBe('送出內容')
    expect(wrapper.get('[data-testid="api-command-submitting"]').attributes('role')).toBe('status')
    expect(wrapper.text()).toContain('處理中測試訊息')
  })

  it('renders a command-level error without replacing the form content', () => {
    const wrapper = mount(ApiCommandResourceView, {
      props: {
        isSubmitting: false,
        errorMessage: '命令錯誤測試訊息',
      },
      slots: {
        default: '<input data-testid="command-field" />',
      },
    })

    expect(wrapper.find('[data-testid="command-field"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="api-command-error"]').attributes('role')).toBe('alert')
    expect(wrapper.text()).toContain('命令錯誤測試訊息')
  })
})