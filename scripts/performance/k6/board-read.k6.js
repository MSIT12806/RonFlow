import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';

const ronAuthApiBaseUrl = __ENV.RONFLOW_LOAD_TEST_RONAUTH_API_BASE_URL || 'http://127.0.0.1:5146/api/auth';
const ronFlowApiBaseUrl = __ENV.RONFLOW_LOAD_TEST_RONFLOW_API_BASE_URL || 'http://127.0.0.1:5088/api';
const userName = __ENV.RONFLOW_LOAD_TEST_USER_NAME || 'perf-owner';
const password = __ENV.RONFLOW_LOAD_TEST_PASSWORD || 'Admin123!';
const pacingSeconds = Number(__ENV.RONFLOW_LOAD_TEST_PACING_SECONDS || '1');

const boardDuration = new Trend('ronflow_board_duration');
const boardResponseStartDuration = new Trend('ronflow_board_response_start_duration');
const boardControllerDuration = new Trend('ronflow_board_controller_duration');
const boardApplicationDuration = new Trend('ronflow_board_application_duration');
const boardStoreDuration = new Trend('ronflow_board_store_duration');
const boardCurrentUserSyncDuration = new Trend('ronflow_board_current_user_sync_duration');
const boardActiveSessionDuration = new Trend('ronflow_board_active_session_duration');

export const options = {
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1000'],
    ronflow_board_duration: ['p(95)<250'],
  },
};

function createSessionId() {
  const randomPart = Math.floor(Math.random() * 1_000_000_000).toString(16);
  return `k6-session-${Date.now()}-${randomPart}`;
}

function createJsonHeaders(accessToken, sessionId) {
  return {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${accessToken}`,
    'X-RonFlow-Session-Id': sessionId,
  };
}

function parseServerTimingHeader(serverTimingHeader) {
  if (!serverTimingHeader || typeof serverTimingHeader !== 'string') {
    return {};
  }

  return serverTimingHeader
    .split(',')
    .map((metric) => metric.trim())
    .reduce((timings, metric) => {
      const [namePart, ...attributes] = metric.split(';').map((part) => part.trim());
      const durationAttribute = attributes.find((attribute) => attribute.startsWith('dur='));
      if (!namePart || !durationAttribute) {
        return timings;
      }

      const durationMs = Number(durationAttribute.slice(4));
      if (Number.isNaN(durationMs)) {
        return timings;
      }

      timings[namePart] = durationMs;
      return timings;
    }, {});
}

export function setup() {
  const loginResponse = http.post(
    `${ronAuthApiBaseUrl}/login`,
    JSON.stringify({ userName, password }),
    { headers: { 'Content-Type': 'application/json' } },
  );

  check(loginResponse, {
    'login succeeded': (response) => response.status === 200,
    'login returned token': (response) => !!response.json('accessToken'),
  });

  const accessToken = loginResponse.json('accessToken');
  const sessionId = createSessionId();
  const headers = createJsonHeaders(accessToken, sessionId);

  const activateResponse = http.post(`${ronFlowApiBaseUrl}/session/activate`, null, { headers });
  check(activateResponse, {
    'session activated': (response) => response.status === 204,
  });

  const projectsResponse = http.get(`${ronFlowApiBaseUrl}/projects`, { headers });
  check(projectsResponse, {
    'projects query succeeded': (response) => response.status === 200,
  });

  const projectItems = projectsResponse.json('items') || [];
  if (projectItems.length === 0) {
    throw new Error('No projects available for board read load test. Seed performance data first.');
  }

  return {
    accessToken,
    sessionId,
    projectIds: projectItems.map((item) => item.id),
  };
}

export default function (data) {
  const projectId = data.projectIds[Math.floor(Math.random() * data.projectIds.length)];
  const headers = createJsonHeaders(data.accessToken, data.sessionId);
  const response = http.get(`${ronFlowApiBaseUrl}/projects/${projectId}/board`, { headers });

  boardDuration.add(response.timings.duration);

  const serverTimingHeader = response.headers['Server-Timing'];
  const serverTimings = parseServerTimingHeader(serverTimingHeader);

  if (typeof serverTimings['response-start'] === 'number') {
    boardResponseStartDuration.add(serverTimings['response-start']);
  }

  if (typeof serverTimings['middleware-current-user-sync'] === 'number') {
    boardCurrentUserSyncDuration.add(serverTimings['middleware-current-user-sync']);
  }

  if (typeof serverTimings['middleware-active-session'] === 'number') {
    boardActiveSessionDuration.add(serverTimings['middleware-active-session']);
  }

  if (typeof serverTimings.controller === 'number') {
    boardControllerDuration.add(serverTimings.controller);
  }

  if (typeof serverTimings.application === 'number') {
    boardApplicationDuration.add(serverTimings.application);
  }

  if (typeof serverTimings.store === 'number') {
    boardStoreDuration.add(serverTimings.store);
  }

  check(response, {
    'board response succeeded': (result) => result.status === 200,
    'board response contains columns': (result) => {
      const columns = result.json('columns');
      return Array.isArray(columns) && columns.length > 0;
    },
  });

  if (pacingSeconds > 0) {
    sleep(pacingSeconds);
  }
}