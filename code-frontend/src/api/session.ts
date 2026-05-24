import { apiPath, request } from './request'

export async function activateRonFlowSession() {
  await request<void>(apiPath('/session/activate'), {
    method: 'POST',
  })
}

export async function releaseRonFlowProjectScope() {
  await request<void>(apiPath('/session/project-scope/release'), {
    method: 'POST',
  })
}