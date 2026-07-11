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
    parentPath: '',
    children: [],
    ...overrides,
  }
}

describe('TaskTreeNode', () => {
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
