import { mount } from '@vue/test-utils'
import TaskDetailModal from '../TaskDetailModal.vue'
import type { TaskDetailResponse } from '../../api/ronflowApi'

function createTask(overrides: Partial<TaskDetailResponse> = {}): TaskDetailResponse {
  return {
    id: 'task-1',
    projectId: 'project-1',
    title: '補上 Drawer 編輯測試',
    description: '讓使用者可以直接在 Task Detail Drawer 編輯標題、描述與到期日。',
    currentState: {
      key: 'active',
      label: '進行中',
      isInitialState: false,
      isCompletedState: false,
    },
    dueDate: '2026-05-20',
    createdAt: '2026-05-12T08:00:00.000Z',
    completedAt: null,
    activityTimeline: [],
    canEnterEdit: true,
    ...overrides,
  }
}

function mountTaskDetail(task: TaskDetailResponse) {
  return mount(TaskDetailModal, {
    props: {
      isOpen: true,
      isLoading: false,
      isSaving: false,
      isEditing: false,
      errorMessage: '',
      saveErrorMessage: '',
      titleValidationError: '',
      reminderDatetimeValidationError: '',
      reminderDeliveryStatusMessage: '提醒可能無法送達，請先啟用此裝置的提醒通知。',
      canEnableReminderDelivery: true,
      isEnablingReminderDelivery: false,
      mode: 'active',
      displayTitle: task.title,
      task,
      formatTimelineTime: (occurredAt: string) => occurredAt,
    },
    global: {
      stubs: {
        BaseModalShell: {
          template: '<div><slot /></div>',
        },
        AsyncStateBoundary: {
          template: '<div><slot /></div>',
        },
        ApiCommandResourceView: {
          template: '<div data-testid="command-resource-view"></div>',
        },
        DatePicker: {
          template: '<input />',
        },
        InputText: {
          template: '<input />',
        },
        Textarea: {
          template: '<textarea></textarea>',
        },
      },
    },
  })
}

describe('TaskDetailModal', () => {
  it('shows completed time when the current workflow state is completed', () => {
    const wrapper = mountTaskDetail(createTask({
      currentState: {
        key: 'shipping',
        label: '已交付',
        isInitialState: false,
        isCompletedState: true,
      },
      completedAt: '2026-05-13T09:30:00.000Z',
    }))

    expect(wrapper.text()).toContain('完成時間')
    expect(wrapper.text()).toContain('2026-05-13T09:30:00.000Z')
  })

  it('does not show completed time when the current workflow state is not completed', () => {
    const wrapper = mountTaskDetail(createTask({
      completedAt: '2026-05-13T09:30:00.000Z',
    }))

    expect(wrapper.text()).not.toContain('完成時間')
  })

  it('shows reminder delivery status and enable button from props', () => {
    const wrapper = mountTaskDetail(createTask())

    expect(wrapper.text()).toContain('提醒可能無法送達，請先啟用此裝置的提醒通知。')
    expect(wrapper.text()).toContain('啟用提醒通知')
  })

  it('opens active tasks in view mode until the user explicitly enters edit mode', () => {
    const wrapper = mountTaskDetail(createTask())

    expect(wrapper.text()).toContain('編輯')
    expect(wrapper.text()).not.toContain('儲存變更')
  })

  it('shows save action after entering edit mode', () => {
    const wrapper = mount(TaskDetailModal, {
      props: {
        isOpen: true,
        isLoading: false,
        isSaving: false,
        isEditing: true,
        errorMessage: '',
        saveErrorMessage: '',
        titleValidationError: '',
        reminderDatetimeValidationError: '',
        reminderDeliveryStatusMessage: '',
        canEnableReminderDelivery: true,
        isEnablingReminderDelivery: false,
        mode: 'active',
        displayTitle: '補上 Drawer 編輯測試',
        task: createTask(),
        formatTimelineTime: (occurredAt: string) => occurredAt,
      },
      global: {
        stubs: {
          BaseModalShell: {
            template: '<div><slot /></div>',
          },
          AsyncStateBoundary: {
            template: '<div><slot /></div>',
          },
          ApiCommandResourceView: {
            template: '<div data-testid="command-resource-view"></div>',
          },
          DatePicker: {
            template: '<input />',
          },
          InputText: {
            template: '<input />',
          },
          Textarea: {
            template: '<textarea></textarea>',
          },
        },
      },
    })

    expect(wrapper.text()).toContain('儲存變更')
    expect(wrapper.text()).not.toContain('編輯')
  })
})