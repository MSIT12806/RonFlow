const storageKey = 'ronflow.session.id'
const invalidatedEventName = 'ronflow:session-invalidated'

function createSessionId() {
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`
}

export function getRonFlowSessionId() {
  const existingSessionId = window.sessionStorage.getItem(storageKey)
  if (existingSessionId) {
    return existingSessionId
  }

  const nextSessionId = createSessionId()
  window.sessionStorage.setItem(storageKey, nextSessionId)
  return nextSessionId
}

export function dispatchRonFlowSessionInvalidated() {
  window.dispatchEvent(new CustomEvent(invalidatedEventName))
}

export function onRonFlowSessionInvalidated(listener: () => void) {
  window.addEventListener(invalidatedEventName, listener)

  return () => {
    window.removeEventListener(invalidatedEventName, listener)
  }
}