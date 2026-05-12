import { mount } from '@vue/test-utils'
import BaseModalShell from '../BaseModalShell.vue'

describe('BaseModalShell', () => {
  it('does not render when closed', () => {
    const wrapper = mount(BaseModalShell, {
      props: {
        isOpen: false,
        title: '建立專案',
        titleId: 'create-project-title',
      },
    })

    expect(wrapper.find('[data-testid="base-modal-shell"]').exists()).toBe(false)
  })

  it('renders dialog semantics, title, and slot content when open', () => {
    const wrapper = mount(BaseModalShell, {
      props: {
        isOpen: true,
        title: '建立專案',
        titleId: 'create-project-title',
        eyebrow: 'New project',
      },
      slots: {
        default: '<form data-testid="modal-content">內容</form>',
      },
    })

    const dialog = wrapper.get('[role="dialog"]')

    expect(dialog.attributes('aria-modal')).toBe('true')
    expect(dialog.attributes('aria-labelledby')).toBe('create-project-title')
    expect(wrapper.get('#create-project-title').text()).toBe('建立專案')
    expect(wrapper.text()).toContain('New project')
    expect(wrapper.get('[data-testid="modal-content"]').text()).toBe('內容')
  })

  it('emits close when the close button is clicked', async () => {
    const wrapper = mount(BaseModalShell, {
      props: {
        isOpen: true,
        title: '建立任務',
        titleId: 'create-task-title',
      },
    })

    await wrapper.get('button[aria-label="關閉視窗"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('keeps the close button disabled and applies the wide layout variant when requested', () => {
    const wrapper = mount(BaseModalShell, {
      props: {
        isOpen: true,
        title: '任務詳細資訊',
        titleId: 'task-detail-title',
        size: 'wide',
        closeDisabled: true,
      },
    })

    expect(wrapper.get('[data-testid="base-modal-shell-card"]').classes()).toContain('base-modal-shell__card--wide')
    expect(wrapper.get('button[aria-label="關閉視窗"]').attributes('disabled')).toBeDefined()
  })

  it('owns the shared shell structure through stable backdrop and card classes', () => {
    const wrapper = mount(BaseModalShell, {
      props: {
        isOpen: true,
        title: '任務詳細資訊',
        titleId: 'task-detail-title',
      },
    })

    expect(wrapper.get('[data-testid="base-modal-shell"]').classes()).toContain('base-modal-shell__backdrop')
    expect(wrapper.get('[data-testid="base-modal-shell-card"]').classes()).toContain('base-modal-shell__card')
  })
})