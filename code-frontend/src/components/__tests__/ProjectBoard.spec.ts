import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ProjectBoard from '../ProjectBoard.vue'
import type { BoardColumnResponse, BoardTaskCardResponse } from '../../api/ronflowApi'

function createTask(overrides: Partial<BoardTaskCardResponse> = {}): BoardTaskCardResponse {
  return {
    id: 'task-1',
    title: 'Task 1',
    isCompleted: false,
    isInFlow: false,
    isSplitComplete: false,
    createdAt: '2026-05-12T08:00:00.000Z',
    completedAt: null,
    parentPath: '',
    children: [],
    ...overrides,
  }
}

function createColumn(overrides: Partial<BoardColumnResponse> = {}): BoardColumnResponse {
  return {
    stateKey: 'todo',
    label: 'Todo',
    isInitialState: true,
    isCompletedState: false,
    emptyStateMessage: '目前沒有任務',
    tasks: [],
    ...overrides,
  }
}

function mountProjectBoard(options: {
  taskTree?: BoardTaskCardResponse[]
  columns?: BoardColumnResponse[]
} = {}) {
  return mount(ProjectBoard, {
    props: {
      activeProjectName: 'RonFlow',
      taskTree: options.taskTree ?? [],
      columns: options.columns ?? [],
      isLoadingBoard: false,
      commandErrorMessage: '',
    },
  })
}

describe('ProjectBoard', () => {
  it('allows project members to open completion condition templates without exposing member management', async () => {
    const wrapper = mountProjectBoard()
    const templateButton = wrapper.findAll('button').find((button) => button.text() === '完成條件模板')

    expect(templateButton).toBeDefined()
    expect(wrapper.findAll('button').some((button) => button.text() === '專案成員')).toBe(false)

    await templateButton!.trigger('click')

    expect(wrapper.emitted('open-project-subtask-templates')).toHaveLength(1)
  })

  it('hides completed task tree branches by default and shows them collapsed when requested', async () => {
    const wrapper = mountProjectBoard({
      taskTree: [
        createTask({
          id: 'completed-root',
          title: 'Completed root',
          isCompleted: true,
          completedAt: '2026-05-13T09:30:00.000Z',
          children: [
            createTask({
              id: 'completed-child',
              title: 'Completed child',
              isCompleted: true,
              completedAt: '2026-05-13T09:30:00.000Z',
            }),
          ],
        }),
        createTask({ id: 'open-root', title: 'Open root' }),
      ],
    })

    expect(wrapper.text()).not.toContain('Completed root')
    expect(wrapper.text()).toContain('Open root')

    await wrapper.get('[data-testid="show-completed-task-tree"]').setValue(true)

    expect(wrapper.text()).toContain('Completed root')
    expect(wrapper.text()).not.toContain('Completed child')
    expect(wrapper.get('[data-testid="task-tree-item-completed-root"]').classes()).toContain('task-tree-item-completed')
  })

  it('moves only the selected task to trash when Delete is pressed outside an editable field', async () => {
    const wrapper = mountProjectBoard({
      taskTree: [createTask()],
    })

    await wrapper.get('[data-testid="task-tree-item-task-1"]').trigger('click')
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Delete' }))

    expect(wrapper.emitted('move-task-to-trash')).toEqual([['task-1']])
  })

  it('duplicates the selected task tree when Ctrl+C and Ctrl+V are pressed', async () => {
    const wrapper = mountProjectBoard({
      taskTree: [createTask()],
    })

    await wrapper.get('[data-testid="task-tree-item-task-1"]').trigger('click')
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'c', ctrlKey: true }))
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'v', ctrlKey: true }))

    expect(wrapper.emitted('duplicate-task-subtree')).toEqual([['task-1']])
  })

  it('keeps manual order by default and can order the task tree and workflow columns by created time', async () => {
    const wrapper = mountProjectBoard({
      taskTree: [
        createTask({ id: 'new-tree-task', title: 'New tree task', createdAt: '2026-05-12T10:00:00.000Z' }),
        createTask({ id: 'old-tree-task', title: 'Old tree task', createdAt: '2026-05-12T09:00:00.000Z' }),
      ],
      columns: [
        createColumn({
          tasks: [
            createTask({ id: 'new-flow-task', title: 'New flow task', isInFlow: true, createdAt: '2026-05-12T10:00:00.000Z' }),
            createTask({ id: 'old-flow-task', title: 'Old flow task', isInFlow: true, createdAt: '2026-05-12T09:00:00.000Z' }),
          ],
        }),
      ],
    })

    expect(wrapper.findAll('[data-testid^="task-tree-item-"]').map((item) => item.attributes('data-testid'))).toEqual([
      'task-tree-item-new-tree-task',
      'task-tree-item-old-tree-task',
    ])
    expect(wrapper.findAll('[data-testid^="workflow-task-"]').map((item) => item.attributes('data-testid'))).toEqual([
      'workflow-task-new-flow-task',
      'workflow-task-old-flow-task',
    ])

    await wrapper.get('[data-testid="task-created-at-sort"]').setValue('created-asc')

    expect(wrapper.findAll('[data-testid^="task-tree-item-"]').map((item) => item.attributes('data-testid'))).toEqual([
      'task-tree-item-old-tree-task',
      'task-tree-item-new-tree-task',
    ])
    expect(wrapper.findAll('[data-testid^="workflow-task-"]').map((item) => item.attributes('data-testid'))).toEqual([
      'workflow-task-old-flow-task',
      'workflow-task-new-flow-task',
    ])

    await wrapper.get('[data-testid="task-created-at-sort"]').setValue('created-desc')

    expect(wrapper.findAll('[data-testid^="task-tree-item-"]').map((item) => item.attributes('data-testid'))).toEqual([
      'task-tree-item-new-tree-task',
      'task-tree-item-old-tree-task',
    ])
    expect(wrapper.findAll('[data-testid^="workflow-task-"]').map((item) => item.attributes('data-testid'))).toEqual([
      'workflow-task-new-flow-task',
      'workflow-task-old-flow-task',
    ])
  })
})
