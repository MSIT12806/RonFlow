import { expect, test, type APIResponse, type TestInfo } from '@playwright/test'
import {
  createProjectThroughApi,
  createTaskThroughApi,
  registerRonFlowApiUser,
} from './support/ronflowApiTestHelpers'
import { createScenarioData } from './support/ronflowTestHelpers'

type ProcessEnvironment = typeof globalThis & {
  process?: {
    env?: Record<string, string | undefined>
  }
}

const backendApiBaseUrl = (globalThis as ProcessEnvironment).process?.env?.RONFLOW_E2E_BACKEND_API_BASE_URL
  ?? 'http://127.0.0.1:8507/api'

const aiRoutes = {
  bootstrap: `${backendApiBaseUrl}/ai/bootstrap`,
  glossary: `${backendApiBaseUrl}/ai/glossary`,
  capabilities: `${backendApiBaseUrl}/ai/capabilities`,
  sessionSummary: `${backendApiBaseUrl}/ai/session-summary`,
  projectListSummary: `${backendApiBaseUrl}/ai/projects/summary`,
  workflowGuidance: `${backendApiBaseUrl}/ai/workflow-guidance`,
  activateScope: `${backendApiBaseUrl}/ai/active-scope`,
  auditEntry(auditEntryId: string) {
    return `${backendApiBaseUrl}/ai/audit-entries/${auditEntryId}`
  },
  projectBoardSummary(projectId: string) {
    return `${backendApiBaseUrl}/ai/projects/${projectId}/board-summary`
  },
  currentWorkSummary(projectId: string) {
    return `${backendApiBaseUrl}/ai/projects/${projectId}/current-work-summary`
  },
  taskDetailSummary(projectId: string, taskId: string) {
    return `${backendApiBaseUrl}/ai/projects/${projectId}/tasks/${taskId}/detail-summary`
  },
  apply: `${backendApiBaseUrl}/ai/apply`,
} as const

type RonFlowAiSession = Awaited<ReturnType<typeof registerRonFlowApiUser>> & {
  ronFlowSessionId: string
}

function authHeaders(accessToken: string, ronFlowSessionId?: string) {
  return {
    Authorization: `Bearer ${accessToken}`,
    ...(ronFlowSessionId ? { 'X-RonFlow-Session-Id': ronFlowSessionId } : {}),
  }
}

async function registerRonFlowAiSession(
  request: Parameters<typeof test>[0]['request'],
): Promise<RonFlowAiSession> {
  const session = await registerRonFlowApiUser(request)
  const ronFlowSessionId = crypto.randomUUID()
  const response = await request.post(`${backendApiBaseUrl}/session/activate`, {
    headers: authHeaders(session.accessToken, ronFlowSessionId),
  })

  expect(response.status(), await response.text()).toBe(204)

  return {
    ...session,
    ronFlowSessionId,
  }
}

async function activateAiScope(
  request: Parameters<typeof test>[0]['request'],
  accessToken: string,
  ronFlowSessionId: string,
  projectId: string,
) {
  const response = await request.post(aiRoutes.activateScope, {
    headers: {
      ...authHeaders(accessToken, ronFlowSessionId),
      'Content-Type': 'application/json',
    },
    data: {
      projectId,
    },
  })

  expect(response.status(), await response.text()).toBe(204)
}

async function expectSuccessfulTextContract(response: APIResponse, expectedLines: string[]) {
  const responseText = await response.text()

  expect(response.ok(), responseText).toBeTruthy()

  for (const expectedLine of expectedLines) {
    expect(responseText).toContain(expectedLine)
  }

  return responseText
}

async function expectErrorTextContract(response: APIResponse, expectedLines: string[]) {
  const responseText = await response.text()

  expect(response.ok(), responseText).toBeFalsy()

  for (const expectedLine of expectedLines) {
    expect(responseText).toContain(expectedLine)
  }

  return responseText
}

async function seedProjectWithTask(request: Parameters<typeof test>[0]['request'], testInfo: TestInfo) {
  const session = await registerRonFlowAiSession(request)
  const { projectName, taskTitle } = createScenarioData(testInfo)
  const project = await createProjectThroughApi(request, session, projectName)
  const task = await createTaskThroughApi(request, session, project.id, taskTitle)

  return {
    session,
    project,
    task,
    taskTitle,
  }
}

test.describe('RonFlow AI 驗收規格 - AI Interaction Surface', () => {
  test('bootstrap 端點回傳固定的啟動文字契約', async ({ request }) => {
    const session = await registerRonFlowAiSession(request)
    const response = await request.get(aiRoutes.bootstrap, {
      headers: authHeaders(session.accessToken, session.ronFlowSessionId),
    })

    await expectSuccessfulTextContract(response, [
      'RonFlow Bootstrap v1',
      'RonFlow 是一個專案管理工具。',
      '你現在應先做以下事情：',
      '1. 讀取 capabilities manifest',
      '4. 讀取 project list summary',
    ])
  })

  test('glossary 端點回傳固定的名詞契約', async ({ request }) => {
    const session = await registerRonFlowAiSession(request)
    const response = await request.get(aiRoutes.glossary, {
      headers: authHeaders(session.accessToken, session.ronFlowSessionId),
    })

    await expectSuccessfulTextContract(response, [
      'RonFlow AI Glossary v1',
      'term: bootstrap',
      'term: active scope',
      'term: workflow guidance',
    ])
  })

  test('manifest 端點回傳目前支援的 read / write capability 清單', async ({ request }) => {
    const session = await registerRonFlowAiSession(request)
    const response = await request.get(aiRoutes.capabilities, {
      headers: authHeaders(session.accessToken, session.ronFlowSessionId),
    })

    await expectSuccessfulTextContract(response, [
      'RonFlow Capabilities Manifest v1',
      '- capability: read_current_work_summary',
      '- capability: create_task',
      'active_scope_required: yes',
      'required_inputs: projectId, title',
      '- capability: update_task_detail',
    ])
  })

  test('session summary 端點在尚未選定 scope 時回傳 active_scope: none', async ({ request }) => {
    const session = await registerRonFlowAiSession(request)
    const response = await request.get(aiRoutes.sessionSummary, {
      headers: authHeaders(session.accessToken, session.ronFlowSessionId),
    })

    await expectSuccessfulTextContract(response, [
      'RonFlow Session Summary v1',
      'session_status: active',
      'actor_type: ai',
      'active_scope: none',
      'available_scopes:',
    ])
  })

  test('project list summary 端點回傳 project 清單與 next actions', async ({ request }, testInfo) => {
    const session = await registerRonFlowAiSession(request)
    const { projectName } = createScenarioData(testInfo)
    const project = await createProjectThroughApi(request, session, projectName)

    const response = await request.get(aiRoutes.projectListSummary, {
      headers: authHeaders(session.accessToken, session.ronFlowSessionId),
    })

    await expectSuccessfulTextContract(response, [
      'RonFlow Project List Summary v1',
      'projects_count:',
      `project_id: ${project.id}`,
      `project_name: ${projectName}`,
      'next_actions:',
      '- read_project_board_summary',
    ])
  })

  test('board summary 與 task detail summary 端點回傳固定欄位與 next actions', async ({ request }, testInfo) => {
    const { session, project, task, taskTitle } = await seedProjectWithTask(request, testInfo)

    const boardResponse = await request.get(aiRoutes.projectBoardSummary(project.id), {
      headers: authHeaders(session.accessToken, session.ronFlowSessionId),
    })

    await expectSuccessfulTextContract(boardResponse, [
      'RonFlow Project Board Summary v1',
      `project_id: ${project.id}`,
      'workflow_columns:',
      '- key: Todo',
      '- key: Active',
      '- key: Review',
      '- key: Done',
      'visible_tasks:',
      `task_id: ${task.id}`,
      `title: ${taskTitle}`,
      'workflow_state_key: Todo',
      'next_actions:',
      '- create_task',
    ])

    const currentWorkResponse = await request.get(aiRoutes.currentWorkSummary(project.id), {
      headers: authHeaders(session.accessToken, session.ronFlowSessionId),
    })

    await expectSuccessfulTextContract(currentWorkResponse, [
      'RonFlow Current Work Summary v1',
      `project_id: ${project.id}`,
      'open_task_count: 1',
      'open_tasks:',
      `task_id: ${task.id}`,
      `title: ${taskTitle}`,
      'workflow_state_key: Todo',
    ])

    const detailResponse = await request.get(aiRoutes.taskDetailSummary(project.id, task.id), {
      headers: authHeaders(session.accessToken, session.ronFlowSessionId),
    })

    await expectSuccessfulTextContract(detailResponse, [
      'RonFlow Task Detail Summary v1',
      `task_id: ${task.id}`,
      'workflow_state_key:',
      'recent_activities:',
      'next_actions:',
      '- update_task_detail',
    ])
  })

  test('workflow guidance 端點回傳固定的建議順序與 ask_human_when 區塊', async ({ request }) => {
    const session = await registerRonFlowAiSession(request)
    const response = await request.get(aiRoutes.workflowGuidance, {
      headers: authHeaders(session.accessToken, session.ronFlowSessionId),
    })

    await expectSuccessfulTextContract(response, [
      'RonFlow Workflow Guidance v1',
      'recommended_order:',
      '1. read summary',
      '4. prepare write request',
      '6. inspect result',
      'task_start_rules:',
      '- use move_task_state with targetStateKey: Active',
      'ask_human_when:',
    ])
  })

  test('apply 端點在 Task 更新成功時回傳固定 apply result 契約', async ({ request }, testInfo) => {
    const { session, project, task } = await seedProjectWithTask(request, testInfo)
    await activateAiScope(request, session.accessToken, session.ronFlowSessionId, project.id)

    const response = await request.post(aiRoutes.apply, {
      headers: {
        ...authHeaders(session.accessToken, session.ronFlowSessionId),
        'Content-Type': 'application/json',
      },
      data: {
        operation: 'update_task_detail',
        targetType: 'task',
        targetId: task.id,
        requiredFields: {
          taskId: task.id,
        },
        optionalFields: {
          title: `${testInfo.title} Updated`,
          dueDate: '2026-06-01',
        },
        note: 'acceptance-first update request',
      },
    })

    await expectSuccessfulTextContract(response, [
      'RonFlow Apply Result v1',
      'status: success',
      'operation: update_task_detail',
      `target_id: ${task.id}`,
      'changed_fields:',
      'audit_entry_id:',
    ])
  })

  test('apply 端點在缺少必要欄位時回傳固定 error 契約', async ({ request }, testInfo) => {
    const { session, project } = await seedProjectWithTask(request, testInfo)
    await activateAiScope(request, session.accessToken, session.ronFlowSessionId, project.id)

    const response = await request.post(aiRoutes.apply, {
      headers: {
        ...authHeaders(session.accessToken, session.ronFlowSessionId),
        'Content-Type': 'application/json',
      },
      data: {
        operation: 'create_task',
        targetType: 'task',
        targetId: 'new',
        requiredFields: {
          projectId: project.id,
        },
        optionalFields: {},
        note: 'missing title on purpose',
      },
    })

    await expectErrorTextContract(response, [
      'RonFlow Error v1',
      'error_code: ValidationFailed',
      'message: Required field `title` is missing.',
      'recovery_hint: Provide `title` and submit the write request again.',
    ])
  })

  test('apply 端點可以建立 Project 並回傳 create_project 結果契約', async ({ request }, testInfo) => {
    const session = await registerRonFlowAiSession(request)
    const response = await request.post(aiRoutes.apply, {
      headers: {
        ...authHeaders(session.accessToken, session.ronFlowSessionId),
        'Content-Type': 'application/json',
      },
      data: {
        operation: 'create_project',
        targetType: 'project',
        targetId: 'new',
        requiredFields: {
          name: `${testInfo.title} Project`,
        },
        optionalFields: {},
        note: 'acceptance-first create project',
      },
    })

    await expectSuccessfulTextContract(response, [
      'RonFlow Apply Result v1',
      'status: success',
      'operation: create_project',
      'target_type: project',
      'audit_entry_id:',
    ])
  })

  test('apply 端點可以在有效 scope 下建立 Task 並回傳 create_task 結果契約', async ({ request }, testInfo) => {
    const session = await registerRonFlowAiSession(request)
    const { projectName } = createScenarioData(testInfo)
    const project = await createProjectThroughApi(request, session, projectName)
    await activateAiScope(request, session.accessToken, session.ronFlowSessionId, project.id)

    const response = await request.post(aiRoutes.apply, {
      headers: {
        ...authHeaders(session.accessToken, session.ronFlowSessionId),
        'Content-Type': 'application/json',
      },
      data: {
        operation: 'create_task',
        targetType: 'task',
        targetId: 'new',
        requiredFields: {
          projectId: project.id,
          title: `${testInfo.title} Task`,
        },
        optionalFields: {},
        note: 'acceptance-first create task',
      },
    })

    await expectSuccessfulTextContract(response, [
      'RonFlow Apply Result v1',
      'status: success',
      'operation: create_task',
      'target_type: task',
      'audit_entry_id:',
    ])
  })

  test('apply 端點可以承接 move_task_state 契約', async ({ request }, testInfo) => {
    const { session, project, task } = await seedProjectWithTask(request, testInfo)
    await activateAiScope(request, session.accessToken, session.ronFlowSessionId, project.id)

    const moveResponse = await request.post(aiRoutes.apply, {
      headers: {
        ...authHeaders(session.accessToken, session.ronFlowSessionId),
        'Content-Type': 'application/json',
      },
      data: {
        operation: 'move_task_state',
        targetType: 'task',
        targetId: task.id,
        requiredFields: {
          taskId: task.id,
          targetStateKey: 'Active',
        },
        optionalFields: {},
        note: 'move task to Active',
      },
    })

    await expectSuccessfulTextContract(moveResponse, [
      'RonFlow Apply Result v1',
      'operation: move_task_state',
      `target_id: ${task.id}`,
      'changed_fields:',
    ])
  })

  test('apply 端點可以承接 reorder_task 契約', async ({ request }, testInfo) => {
    const session = await registerRonFlowAiSession(request)
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const project = await createProjectThroughApi(request, session, projectName)
    await createTaskThroughApi(request, session, project.id, `${taskTitle} A`)
    const secondTask = await createTaskThroughApi(request, session, project.id, `${taskTitle} B`)
    await activateAiScope(request, session.accessToken, session.ronFlowSessionId, project.id)

    const reorderResponse = await request.post(aiRoutes.apply, {
      headers: {
        ...authHeaders(session.accessToken, session.ronFlowSessionId),
        'Content-Type': 'application/json',
      },
      data: {
        operation: 'reorder_task',
        targetType: 'task',
        targetId: secondTask.id,
        requiredFields: {
          taskId: secondTask.id,
          targetStateKey: 'Todo',
          targetIndex: 0,
        },
        optionalFields: {},
        note: 'reorder task within Todo',
      },
    })

    await expectSuccessfulTextContract(reorderResponse, [
      'RonFlow Apply Result v1',
      'operation: reorder_task',
      `target_id: ${secondTask.id}`,
      'changed_fields:',
      'audit_entry_id:',
    ])

    const boardResponse = await request.get(aiRoutes.projectBoardSummary(project.id), {
      headers: authHeaders(session.accessToken, session.ronFlowSessionId),
    })

    const boardText = await expectSuccessfulTextContract(boardResponse, [
      'RonFlow Project Board Summary v1',
      `project_id: ${project.id}`,
    ])

    expect(boardText.indexOf(`Task visible on board: ${taskTitle} B`)).toBeLessThan(boardText.indexOf(`Task visible on board: ${taskTitle} A`))
  })

  test('apply 端點可以承接 archive、restore、trash、restore_trashed 契約', async ({ request }, testInfo) => {
    const { session, project, task } = await seedProjectWithTask(request, testInfo)
    await activateAiScope(request, session.accessToken, session.ronFlowSessionId, project.id)

    for (const operation of ['archive_task', 'restore_archived_task', 'trash_task', 'restore_trashed_task'] as const) {
      const response = await request.post(aiRoutes.apply, {
        headers: {
          ...authHeaders(session.accessToken, session.ronFlowSessionId),
          'Content-Type': 'application/json',
        },
        data: {
          operation,
          targetType: 'task',
          targetId: task.id,
          requiredFields: {
            taskId: task.id,
          },
          optionalFields: {},
          note: `acceptance-first ${operation}`,
        },
      })

      await expectSuccessfulTextContract(response, [
        'RonFlow Apply Result v1',
        `operation: ${operation}`,
        `target_id: ${task.id}`,
        'audit_entry_id:',
      ])
    }
  })

  test('board summary 端點在無權存取他人 Project 時回傳 Forbidden error 契約', async ({ request }, testInfo) => {
    const owner = await registerRonFlowAiSession(request)
    const otherUser = await registerRonFlowAiSession(request)
    const { projectName } = createScenarioData(testInfo)
    const project = await createProjectThroughApi(request, owner, projectName)

    const response = await request.get(aiRoutes.projectBoardSummary(project.id), {
      headers: authHeaders(otherUser.accessToken, otherUser.ronFlowSessionId),
    })

    await expectErrorTextContract(response, [
      'RonFlow Error v1',
      'error_code: Forbidden',
      'recovery_hint:',
    ])
  })

  test('apply 端點在 scope 不正確或未啟用時回傳 ScopeRequired error 契約', async ({ request }, testInfo) => {
    const { session, task } = await seedProjectWithTask(request, testInfo)
    const response = await request.post(aiRoutes.apply, {
      headers: {
        ...authHeaders(session.accessToken, session.ronFlowSessionId),
        'Content-Type': 'application/json',
      },
      data: {
        operation: 'update_task_detail',
        targetType: 'task',
        targetId: task.id,
        requiredFields: {
          taskId: task.id,
        },
        optionalFields: {
          title: `${testInfo.title} Without Scope`,
        },
        note: 'send without active scope on purpose',
      },
    })

    await expectErrorTextContract(response, [
      'RonFlow Error v1',
      'error_code: ScopeRequired',
      'recovery_hint:',
    ])
  })

  test('audit entry 端點可以查回 apply 成功後的固定 audit 契約', async ({ request }, testInfo) => {
    const { session, project, task } = await seedProjectWithTask(request, testInfo)
    await activateAiScope(request, session.accessToken, session.ronFlowSessionId, project.id)

    const applyResponse = await request.post(aiRoutes.apply, {
      headers: {
        ...authHeaders(session.accessToken, session.ronFlowSessionId),
        'Content-Type': 'application/json',
      },
      data: {
        operation: 'update_task_detail',
        targetType: 'task',
        targetId: task.id,
        requiredFields: {
          taskId: task.id,
        },
        optionalFields: {
          title: `${testInfo.title} Audit`,
        },
        note: 'produce audit entry',
      },
    })

    const applyText = await expectSuccessfulTextContract(applyResponse, [
      'RonFlow Apply Result v1',
      'audit_entry_id:',
    ])

    const auditEntryIdLine = applyText
      .split(/\r?\n/)
      .find((line) => line.startsWith('audit_entry_id:'))

    expect(auditEntryIdLine).toBeTruthy()

    const auditEntryId = auditEntryIdLine!.slice('audit_entry_id:'.length).trim()
    const auditResponse = await request.get(aiRoutes.auditEntry(auditEntryId), {
      headers: authHeaders(session.accessToken, session.ronFlowSessionId),
    })

    await expectSuccessfulTextContract(auditResponse, [
      'RonFlow Audit Entry v1',
      `audit_entry_id: ${auditEntryId}`,
      'actor_type: ai',
      `target_id: ${task.id}`,
      'requested_change: update_task_detail',
      'actual_diff:',
    ])
  })
})
