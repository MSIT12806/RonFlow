import { expect, type APIRequestContext } from '@playwright/test'
import { createRonFlowAuthUser, type RonFlowAuthUser } from './ronflowAuthTestHelpers'

type ProcessEnvironment = typeof globalThis & {
  process?: {
    env?: Record<string, string | undefined>
  }
}

type AuthenticationResponse = {
  accessToken: string
}

type ProjectResponse = {
  id: string
  name: string
  updatedAt: string
}

type TaskResponse = {
  id: string
}

type ProjectListItem = {
  id: string
  name: string
  updatedAt: string
  role: string
}

type ProjectInvitation = {
  id: string
  invitee: string
  status: string
}

type InvitationInboxItem = {
  id: string
  projectId: string
  projectName: string
  inviterName: string
}

type ProjectMember = {
  userName: string
  role: string
}

export type RonFlowApiSession = {
  user: RonFlowAuthUser
  accessToken: string
}

export type TestHttpFault = {
  method: string
  pathPattern: string
  statusCode?: number | null
  delayMs?: number
  message?: string
}

const ronAuthApiBaseUrl = (globalThis as ProcessEnvironment).process?.env?.RONFLOW_E2E_RONAUTH_API_BASE_URL
  ?? 'http://127.0.0.1:8513/api/auth'

const backendApiBaseUrl = (globalThis as ProcessEnvironment).process?.env?.RONFLOW_E2E_BACKEND_API_BASE_URL
  ?? 'http://127.0.0.1:8507/api'

function authHeaders(accessToken: string) {
  return {
    Authorization: `Bearer ${accessToken}`,
  }
}

async function expectJsonOk(response: Awaited<ReturnType<APIRequestContext['get']>>) {
  expect(response.ok()).toBeTruthy()
}

export async function registerRonFlowApiUser(
  request: APIRequestContext,
  user = createRonFlowAuthUser(),
): Promise<RonFlowApiSession> {
  const response = await request.post(`${ronAuthApiBaseUrl}/register`, {
    data: {
      userName: user.userName,
      email: user.email,
      password: user.password,
    },
  })

  await expectJsonOk(response)

  const payload = await response.json() as AuthenticationResponse
  const session = {
    user,
    accessToken: payload.accessToken,
  }

  await syncRonFlowKnownUser(request, session)
  return session
}

export async function loginRonFlowApiUser(request: APIRequestContext, user: RonFlowAuthUser): Promise<RonFlowApiSession> {
  const response = await request.post(`${ronAuthApiBaseUrl}/login`, {
    data: {
      userName: user.userName,
      password: user.password,
    },
  })

  await expectJsonOk(response)

  const payload = await response.json() as AuthenticationResponse
  const session = {
    user,
    accessToken: payload.accessToken,
  }

  await syncRonFlowKnownUser(request, session)
  return session
}

export async function syncRonFlowKnownUser(request: APIRequestContext, session: RonFlowApiSession) {
  const response = await request.get(`${backendApiBaseUrl}/projects`, {
    headers: authHeaders(session.accessToken),
  })

  await expectJsonOk(response)
}

export async function createProjectThroughApi(
  request: APIRequestContext,
  session: RonFlowApiSession,
  projectName: string,
) {
  const response = await request.post(`${backendApiBaseUrl}/projects`, {
    headers: authHeaders(session.accessToken),
    data: { name: projectName },
  })

  await expectJsonOk(response)
  return response.json() as Promise<ProjectResponse>
}

export async function createTaskThroughApi(
  request: APIRequestContext,
  session: RonFlowApiSession,
  projectId: string,
  taskTitle: string,
) {
  const response = await request.post(`${backendApiBaseUrl}/projects/${projectId}/tasks`, {
    headers: authHeaders(session.accessToken),
    data: { title: taskTitle },
  })

  await expectJsonOk(response)
  return response.json() as Promise<TaskResponse>
}

export async function moveTaskStateThroughApi(
  request: APIRequestContext,
  session: RonFlowApiSession,
  projectId: string,
  taskId: string,
  stateKey: string,
) {
  const response = await request.patch(`${backendApiBaseUrl}/projects/${projectId}/tasks/${taskId}/state`, {
    headers: authHeaders(session.accessToken),
    data: { stateKey },
  })

  await expectJsonOk(response)
}

export async function inviteProjectMemberThroughApi(
  request: APIRequestContext,
  session: RonFlowApiSession,
  projectId: string,
  invitee: string,
) {
  const response = await request.post(`${backendApiBaseUrl}/projects/${projectId}/invitations`, {
    headers: authHeaders(session.accessToken),
    data: { invitee },
  })

  await expectJsonOk(response)
  return response.json() as Promise<ProjectInvitation>
}

export async function getProjectInvitationsThroughApi(
  request: APIRequestContext,
  session: RonFlowApiSession,
  projectId: string,
) {
  const response = await request.get(`${backendApiBaseUrl}/projects/${projectId}/invitations`, {
    headers: authHeaders(session.accessToken),
  })

  await expectJsonOk(response)
  const payload = await response.json() as { items: ProjectInvitation[] }
  return payload.items
}

export async function getProjectMembersThroughApi(
  request: APIRequestContext,
  session: RonFlowApiSession,
  projectId: string,
) {
  const response = await request.get(`${backendApiBaseUrl}/projects/${projectId}/members`, {
    headers: authHeaders(session.accessToken),
  })

  await expectJsonOk(response)
  const payload = await response.json() as { items: ProjectMember[] }
  return payload.items
}

export async function getInvitationInboxThroughApi(request: APIRequestContext, session: RonFlowApiSession) {
  const response = await request.get(`${backendApiBaseUrl}/invitations`, {
    headers: authHeaders(session.accessToken),
  })

  await expectJsonOk(response)
  const payload = await response.json() as { items: InvitationInboxItem[] }
  return payload.items
}

export async function acceptInvitationThroughApi(
  request: APIRequestContext,
  session: RonFlowApiSession,
  invitationId: string,
) {
  const response = await request.post(`${backendApiBaseUrl}/invitations/${invitationId}/accept`, {
    headers: authHeaders(session.accessToken),
  })

  await expectJsonOk(response)
}

export async function rejectInvitationThroughApi(
  request: APIRequestContext,
  session: RonFlowApiSession,
  invitationId: string,
) {
  const response = await request.post(`${backendApiBaseUrl}/invitations/${invitationId}/reject`, {
    headers: authHeaders(session.accessToken),
  })

  await expectJsonOk(response)
}

export async function getProjectsThroughApi(request: APIRequestContext, session: RonFlowApiSession) {
  const response = await request.get(`${backendApiBaseUrl}/projects`, {
    headers: authHeaders(session.accessToken),
  })

  await expectJsonOk(response)
  const payload = await response.json() as { items: ProjectListItem[] }
  return payload.items
}

export async function configureTestFaultsThroughApi(
  request: APIRequestContext,
  session: RonFlowApiSession,
  faults: TestHttpFault[],
) {
  const response = await request.put(`${backendApiBaseUrl}/testing/faults`, {
    headers: authHeaders(session.accessToken),
    data: { items: faults },
  })

  await expectJsonOk(response)
}

export async function clearTestFaultsThroughApi(request: APIRequestContext, session: RonFlowApiSession) {
  const response = await request.delete(`${backendApiBaseUrl}/testing/faults`, {
    headers: authHeaders(session.accessToken),
  })

  expect(response.status()).toBe(204)
}