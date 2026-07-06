import { mount } from '@vue/test-utils'
import TaskDetailModal from '../TaskDetailModal.vue'
import type { TaskDetailResponse } from '../../api/ronflowApi'

function createTask(overrides: Partial<TaskDetailResponse> = {}): TaskDetailResponse {
  return {
    id: 'task-1',
    projectId: 'project-1',
    parentTaskId: null,
    title: '補上 Drawer 編輯測試',
    description: '讓使用者可以直接在 Task Detail Drawer 編輯標題、描述與到期日。',
    currentState: {
      key: 'active',
      label: '進行中',
      isInitialState: false,
      isCompletedState: false,
    },
    isInFlow: true,
    dueDate: '2026-05-20',
    lifecycleState: 'activeRecord',
    createdAt: '2026-05-12T08:00:00.000Z',
    completedAt: null,
    estimatedEffort: null,
    childTasks: [],
    parentPath: '',
    subtasks: [
      {
        id: 'subtask-1',
        title: '需求已釐清',
        isChecked: false,
        order: 0,
      },
    ],
    codeTraceability: {
      api: [],
      frontendPages: [],
      frontendComponents: [],
    },
    activityTimeline: [],
    canEnterEdit: true,
    ...overrides,
  }
}

const baseModalShellStub = {
  props: ['presentation', 'allowUnderlayInteraction'],
  template: '<div data-testid="base-modal-shell-stub" :data-presentation="presentation" :data-allow-underlay-interaction="allowUnderlayInteraction"><slot /></div>',
}

function mountTaskDetail(task: TaskDetailResponse, propOverrides: Record<string, unknown> = {}) {
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
      ...propOverrides,
    },
    global: {
      stubs: {
        BaseModalShell: baseModalShellStub,
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
  it('uses drawer presentation by default and keeps modal view available', () => {
    const drawerWrapper = mountTaskDetail(createTask())
    const modalWrapper = mountTaskDetail(createTask(), { viewMode: 'modal' })

    expect(drawerWrapper.get('[data-testid="base-modal-shell-stub"]').attributes('data-presentation')).toBe('drawer')
    expect(drawerWrapper.get('[data-testid="base-modal-shell-stub"]').attributes('data-allow-underlay-interaction')).toBeDefined()
    expect(modalWrapper.get('[data-testid="base-modal-shell-stub"]').attributes('data-presentation')).toBe('modal')
  })

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

  it('shows created time as header metadata instead of a standalone field', () => {
    const wrapper = mountTaskDetail(createTask())

    expect(wrapper.find('.detail-preview-meta').text()).toBe('建立 2026-05-12T08:00:00.000Z')
    expect(wrapper.text()).not.toContain('建立時間')
  })

  it('shows task tree position as plain text when the task has a parent path', () => {
    const wrapper = mountTaskDetail(createTask({
      parentPath: '設計 Hatchery > Flow 關聯提示',
    }))

    expect(wrapper.text()).toContain('任務樹位置')
    expect(wrapper.text()).toContain('設計 Hatchery > Flow 關聯提示')
  })

  it('shows structured code traceability entries in view mode', () => {
    const wrapper = mountTaskDetail(createTask({
      codeTraceability: {
        api: [
          { changeType: 'added', target: 'GET /api/build-info' },
        ],
        frontendPages: [
          { changeType: 'modified', target: 'ProjectBoardPage' },
        ],
        frontendComponents: [
          { changeType: 'removed', target: 'LegacyTaskDrawer' },
        ],
      },
    }))

    expect(wrapper.text()).toContain('程式修改追蹤')
    expect(wrapper.text()).toContain('新增')
    expect(wrapper.text()).toContain('GET /api/build-info')
    expect(wrapper.text()).toContain('修改')
    expect(wrapper.text()).toContain('ProjectBoardPage')
    expect(wrapper.text()).toContain('移除')
    expect(wrapper.text()).toContain('LegacyTaskDrawer')
  })

  it('places code traceability after reminders', () => {
    const wrapper = mountTaskDetail(createTask())
    const sections = wrapper.findAll('.detail-card-full')
    const reminderIndex = sections.findIndex((section) => section.attributes('data-testid') === 'task-reminders-section')
    const traceabilityIndex = sections.findIndex((section) => section.attributes('data-testid') === 'task-code-traceability-section')

    expect(reminderIndex).toBeGreaterThanOrEqual(0)
    expect(traceabilityIndex).toBeGreaterThan(reminderIndex)
  })

  it('opens active tasks in view mode until the user explicitly enters edit mode', () => {
    const wrapper = mountTaskDetail(createTask())

    expect(wrapper.text()).toContain('編輯')
    expect(wrapper.text()).not.toContain('儲存變更')
  })

  it('groups completion criteria, estimated effort, and send-to-flow in the Ready list block', async () => {
    const wrapper = mountTaskDetail(createTask({
      isInFlow: false,
      estimatedEffort: {
        value: 2,
        unit: 'hours',
      },
    }))

    const readyList = wrapper.get('[data-testid="task-ready-list-section"]')

    expect(readyList.text()).toContain('Ready list')
    expect(readyList.text()).toContain('完成條件')
    expect(readyList.text()).toContain('預估耗時')

    const sendButton = readyList.findAll('button').find((button) => button.text() === '送進 Flow')
    expect(sendButton).toBeDefined()
    expect(sendButton!.attributes('disabled')).toBeUndefined()

    await sendButton!.trigger('click')

    expect(wrapper.emitted('send-to-flow')).toEqual([['task-1']])
  })

  it('emits direct checklist replacement when a subtask is checked in view mode', async () => {
    const wrapper = mountTaskDetail(createTask())

    const checkbox = wrapper.find('input[type="checkbox"]')
    await checkbox.setValue(true)

    expect(wrapper.emitted('replace-subtasks')).toEqual([[
      {
        taskId: 'task-1',
        subtasks: [
          {
            id: 'subtask-1',
            title: '需求已釐清',
            isChecked: true,
            order: 0,
          },
        ],
      },
    ]])
  })

  it('creates child tasks and opens existing child task detail', async () => {
    const wrapper = mountTaskDetail(createTask({
      childTasks: [
        { id: 'child-task-1', title: '撰寫 SRS', isCompleted: false, isInFlow: false, parentPath: '補上 Drawer 編輯測試', children: [] },
      ],
    }))

    ;(wrapper.vm as unknown as { draftChildTaskTitle: string }).draftChildTaskTitle = '撰寫驗收測試'
    await wrapper.findAll('button').find((button) => button.text() === '建立 child task')!.trigger('click')
    await wrapper.findAll('button').find((button) => button.text().includes('撰寫 SRS'))!.trigger('click')

    expect(wrapper.emitted('create-child-task')).toEqual([[{
      parentTaskId: 'task-1',
      title: '撰寫驗收測試',
    }]])
    expect(wrapper.emitted('open-child-task')).toEqual([['child-task-1', '撰寫 SRS']])
    expect(wrapper.find('[data-testid="task-ready-list-section"]').exists()).toBe(false)
  })

  it('disables checklist checkboxes when the task is locked by another user', () => {
    const wrapper = mount(TaskDetailModal, {
      props: {
        isOpen: true,
        isLoading: false,
        isSaving: false,
        isEditing: false,
        canEnterEdit: false,
        errorMessage: '',
        saveErrorMessage: '',
        titleValidationError: '',
        reminderDatetimeValidationError: '',
        reminderDeliveryStatusMessage: '',
        canEnableReminderDelivery: true,
        isEnablingReminderDelivery: false,
        mode: 'active',
        displayTitle: '補上 Drawer 編輯測試',
        task: createTask({ canEnterEdit: false }),
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

    expect(wrapper.find('input[type="checkbox"]').attributes('disabled')).toBeDefined()
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
    expect(wrapper.findAll('button').some((button) => button.text() === '編輯')).toBe(false)
  })

  it('emits structured code traceability in save payload', async () => {
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
        task: createTask({
          codeTraceability: {
            api: [
              { changeType: 'added', target: 'GET /api/build-info' },
            ],
            frontendPages: [],
            frontendComponents: [],
          },
        }),
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

    await wrapper.get('button.primary-button').trigger('click')

    expect(wrapper.emitted('save')).toEqual([[{
      taskId: 'task-1',
      title: '補上 Drawer 編輯測試',
      description: '讓使用者可以直接在 Task Detail Drawer 編輯標題、描述與到期日。',
      dueDate: '2026-05-20',
      estimatedEffort: null,
      codeTraceability: {
        api: [
          { changeType: 'added', target: 'GET /api/build-info' },
        ],
        frontendPages: [],
        frontendComponents: [],
      },
      subtasks: [
        {
          id: 'subtask-1',
          title: '需求已釐清',
          isChecked: false,
          order: 0,
        },
      ],
    }]])
  })

  it('omits blank new subtasks from save payload', async () => {
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

    const addSubtaskButton = wrapper.findAll('button').find((button) => button.text() === '新增完成條件')
    expect(addSubtaskButton).toBeDefined()

    await addSubtaskButton!.trigger('click')
    await wrapper.get('button.primary-button').trigger('click')

    const savePayload = wrapper.emitted('save')?.[0]?.[0] as { subtasks: Array<{ id: string | null; title: string; order: number }> }

    expect(savePayload.subtasks).toEqual([
      {
        id: 'subtask-1',
        title: '需求已釐清',
        isChecked: false,
        order: 0,
      },
    ])
  })

  it('stacks the task description label above the input area', () => {
    const wrapper = mountTaskDetail(createTask())
    const descriptionField = wrapper.find('label[for="task-detail-description-input"]').element.parentElement

    expect(descriptionField).not.toBeNull()
    expect(descriptionField?.classList.contains('detail-field-inline')).toBe(false)
    expect(descriptionField?.classList.contains('detail-field')).toBe(true)
  })
})
