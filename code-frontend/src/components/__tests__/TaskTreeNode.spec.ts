import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import TaskTreeNode from '../TaskTreeNode.vue'
import type { BoardTaskCardResponse } from '../../api/ronflowApi'

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

describe('TaskTreeNode', () => {
  it('shows direct child status counts for parent tasks', () => {
    const wrapper = mount(TaskTreeNode, {
      props: {
        task: createTask({
          children: [
            createTask({ id: 'child-1', title: 'Child 1', isCompleted: true }),
            createTask({ id: 'child-2', title: 'Child 2', isInFlow: true }),
            createTask({ id: 'child-3', title: 'Child 3' }),
          ],
        }),
      },
    })

    expect(wrapper.get('[data-testid="task-tree-child-status-todo"]').text()).toContain('○ 1')
    expect(wrapper.get('[data-testid="task-tree-child-status-doing"]').text()).toContain('◐ 1')
    expect(wrapper.get('[data-testid="task-tree-child-status-completed"]').text()).toContain('✓ 1')
  })

  it('collapses completed parent tasks by default', () => {
    const wrapper = mount(TaskTreeNode, {
      props: {
        task: createTask({
          isCompleted: true,
          completedAt: '2026-05-13T09:30:00.000Z',
          children: [
            createTask({ id: 'child-1', title: 'Child 1', isCompleted: true }),
          ],
        }),
      },
    })

    expect(wrapper.get('.task-tree-toggle').attributes('aria-expanded')).toBe('false')
    expect(wrapper.text()).not.toContain('Child 1')
  })

  it('keeps completed in-flow tasks in the completed visual state', () => {
    const wrapper = mount(TaskTreeNode, {
      props: {
        task: createTask({
          isCompleted: true,
          isInFlow: true,
          completedAt: '2026-05-13T09:30:00.000Z',
        }),
      },
    })

    expect(wrapper.get('.task-tree-item').classes()).toContain('task-tree-item-completed')
    expect(wrapper.get('.task-tree-item').classes()).toContain('task-tree-item-in-flow')
    expect(wrapper.get('.task-tree-completion-indicator').text()).toBe('✓')
  })

  it('selects the task without opening its detail', async () => {
    const wrapper = mount(TaskTreeNode, {
      props: {
        task: createTask(),
        selectedTaskId: 'task-1',
      },
    })

    expect(wrapper.get('.task-tree-item').classes()).toContain('task-tree-item-selected')

    await wrapper.get('.task-tree-item').trigger('click')

    expect(wrapper.emitted('select-task')).toEqual([['task-1']])
    expect(wrapper.emitted('open-task-detail')).toBeUndefined()
  })

  it('opens task detail only from the dedicated expand button', async () => {
    const wrapper = mount(TaskTreeNode, {
      props: {
        task: createTask(),
      },
    })

    await wrapper.get('[aria-label="展開任務詳細資訊"]').trigger('click')

    expect(wrapper.emitted('open-task-detail')).toEqual([['task-1', 'Task 1']])
  })

  it('shows an inside drop guide on the target task card', () => {
    const wrapper = mount(TaskTreeNode, {
      props: {
        task: createTask(),
        activeDropTarget: {
          taskId: 'task-1',
          placement: 'inside',
        },
      },
    })

    expect(wrapper.get('.task-tree-item').classes()).toContain('task-tree-item-drop-inside')
    expect(wrapper.classes()).not.toContain('task-tree-node-drop-before')
    expect(wrapper.classes()).not.toContain('task-tree-node-drop-after')
  })

  it('shows split-complete styling and badge when the parent task is manually marked complete for decomposition', () => {
    const wrapper = mount(TaskTreeNode, {
      props: {
        task: createTask({
          isSplitComplete: true,
          children: [
            createTask({ id: 'child-1', title: 'Child 1' }),
          ],
        }),
      },
    })

    expect(wrapper.get('.task-tree-item').classes()).toContain('task-tree-item-split-complete')
    expect(wrapper.get('[data-testid="task-split-complete-badge"]').text()).toContain('拆解完成')
  })

  it('emits a before-drop payload when the cursor is near the top edge', async () => {
    const wrapper = mount(TaskTreeNode, {
      props: {
        task: createTask(),
        draggingTaskId: 'dragged-task',
      },
    })

    const itemButton = wrapper.get('.task-tree-item')
    itemButton.element.getBoundingClientRect = () => ({
      top: 100,
      bottom: 160,
      left: 0,
      right: 320,
      width: 320,
      height: 60,
      x: 0,
      y: 100,
      toJSON: () => ({}),
    })

    await itemButton.trigger('dragover', {
      clientY: 108,
      dataTransfer: {
        dropEffect: 'none',
      },
    })

    expect(wrapper.emitted('task-drag-over')).toEqual([[
      {
        taskId: 'task-1',
        parentTaskId: null,
        placement: 'before',
      },
    ]])
  })
})
