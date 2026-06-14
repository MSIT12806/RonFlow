# RonFlow Diagnostics API Spec

## 1. Purpose

RonFlow needs a small diagnostics application that can inspect deployment, log, and local runtime health signals without coupling those diagnostics to the core RonFlow product API.

The first target environment is localhost IIS deployment. The diagnostics application should help developers and AI agents answer operational questions such as:

- Which RonFlow components are currently deployed?
- Are configured log files present and readable?
- What are the latest relevant log lines?
- Is a configured Git-backed artifact repository clean and what is its latest commit?
- Are RonAuth and RonFlow API health endpoints reachable?

This specification defines a standalone application, not a section of the core flow spec.

## 2. Application Boundary

The application name should be:

```text
RonFlow.Diagnostics.Api
```

It must be deployable independently from:

- `RonFlow.Api`
- `RonAuth.Api`
- RonFlow frontend

It must not own business workflows such as project, task, board, invitation, or user mutation. It is an operational diagnostics surface only.

## 3. Design Principles

1. Diagnostics sources must be configuration-driven.
2. The API must not depend on a specific database Git sync implementation.
3. Database Git sync should be supported by adding configured log and repository sources.
4. The API must avoid exposing secrets such as PATs, bearer tokens, connection strings, or credentials.
5. Log reading must be bounded by tail limits to avoid returning very large files.
6. Missing files, missing repositories, and inaccessible paths should return structured diagnostics results instead of crashing the application.
7. The app should be useful even when `RonFlow.Api` or `RonAuth.Api` is unavailable.

## 4. Non-Goals

The first version should not:

- Provide a frontend UI.
- Modify log files.
- Modify Git repositories.
- Restart IIS, app pools, or Windows services.
- Expose arbitrary filesystem browsing.
- Require direct references to RonFlow domain, application, or infrastructure projects.
- Replace centralized observability tools such as OpenTelemetry, Seq, ELK, or Application Insights.

## 5. Configuration

Diagnostics sources should be configured through `appsettings.json` and environment-specific overrides.

Example:

```json
{
  "Diagnostics": {
    "LogSources": {
      "ronflow-api-stdout": {
        "Provider": "File",
        "PathPattern": "C:\\inetpub\\ronflow-api\\logs\\stdout*.log",
        "DisplayName": "RonFlow API stdout"
      },
      "ronauth-api-stdout": {
        "Provider": "File",
        "PathPattern": "C:\\inetpub\\ronauth-api\\logs\\stdout*.log",
        "DisplayName": "RonAuth API stdout"
      },
      "database-git-sync": {
        "Provider": "File",
        "PathPattern": "C:\\inetpub\\ronflow-api\\App_Data\\database-git-sync.log",
        "DisplayName": "RonFlow database Git sync"
      },
      "ronflow-api-centralized": {
        "Provider": "Elasticsearch",
        "DisplayName": "RonFlow API centralized logs",
        "Centralized": {
          "Endpoint": "https://logs.example.internal",
          "IndexPattern": "ronflow-*",
          "QueryPattern": "level:ERROR OR level:WARN",
          "ServiceName": "ronflow-api",
          "Environment": "localhost",
          "TimeRangeMinutes": 60,
          "MaxTailLines": 500
        }
      }
    },
    "GitRepositories": {
      "ronflow-db": {
        "Path": "C:\\inetpub\\ronflow-api\\App_Data\\ronflow-db-repository",
        "DisplayName": "RonFlow database repository"
      }
    },
    "BuildInfoSources": {
      "ronflow-api": {
        "Path": "C:\\inetpub\\ronflow-api\\build-info.json",
        "DisplayName": "RonFlow API"
      },
      "ronauth-api": {
        "Path": "C:\\inetpub\\ronauth-api\\build-info.json",
        "DisplayName": "RonAuth API"
      }
    },
    "HealthChecks": {
      "ronflow-ai-bootstrap": {
        "Url": "http://localhost/ronflow-api/api/ai/bootstrap",
        "ExpectedStatusCodes": [401, 200]
      },
      "ronauth-bootstrap": {
        "Url": "http://localhost/ronauth-api/api/auth/bootstrap",
        "ExpectedStatusCodes": [401, 200]
      }
    }
  }
}
```

`database-git-sync` and `ronflow-db` are examples of configured sources. They must not be hard-coded into the diagnostics application.

### 5.1 Log Provider Model

`Diagnostics:LogSources:*:Provider` selects the log backend. The default is `File`, preserving the original localhost IIS behavior.

Supported initial providers:

- `File`: reads the newest local file matched by `PathPattern`.
- `Elasticsearch` / `OpenSearch`: sends a bounded `_search` request to a centralized backend described by `Centralized`.

Centralized provider settings:

- `Endpoint`: base URL of the centralized log platform.
- `IndexPattern`: index or data stream pattern used in the `_search` path.
- `QueryPattern`: optional backend query string.
- `ServiceName`: optional `service.name` filter.
- `Environment`: optional `service.environment` filter.
- `TimeRangeMinutes`: relative time window, bounded to at least one minute.
- `MaxTailLines`: optional provider-specific cap, still subject to bounded query behavior.

Provider implementations must keep the `/api/logs` and `/api/logs/{sourceKey}` response shape stable. Centralized providers return `fileName` and `lastWriteTimeUtc` as `null`, because the backing data is no longer a local file. Provider errors should be returned as structured log tail results with redaction applied, not unhandled exceptions.

## 6. API Surface

### 6.1 Health

```text
GET /api/health
```

Returns whether the diagnostics API itself is running.

Minimum response:

```json
{
  "status": "healthy",
  "application": "RonFlow.Diagnostics.Api",
  "checkedAtUtc": "2026-06-14T00:00:00Z"
}
```

### 6.2 Source Inventory

```text
GET /api/sources
```

Returns configured diagnostics sources grouped by type.

The response must not expose secrets or unbounded filesystem paths beyond configured source paths.

### 6.3 Log Source List

```text
GET /api/logs
```

Returns configured log source keys and the current readability state for each source.

Each item should include:

- `key`
- `displayName`
- `exists`
- `readable`
- `matchedFileCount`
- `latestFileName`
- `latestLastWriteTimeUtc`
- `error`

### 6.4 Log Tail

```text
GET /api/logs/{sourceKey}?tail=200
```

Returns the latest lines from the configured log source.

Rules:

1. `tail` must default to 200.
2. `tail` must have a maximum, initially 1000.
3. If `PathPattern` matches multiple files, the API should read the newest file by `LastWriteTimeUtc` unless a future version adds explicit file selection.
4. Returned lines must be redacted.
5. Unknown `sourceKey` returns `404 Not Found`.
6. Missing configured files returns `200 OK` with `exists: false`, not an unhandled exception.

Minimum response:

```json
{
  "key": "database-git-sync",
  "displayName": "RonFlow database Git sync",
  "exists": true,
  "readable": true,
  "fileName": "database-git-sync.log",
  "lastWriteTimeUtc": "2026-06-14T00:00:00Z",
  "tail": 200,
  "lines": [
    "[2026-06-14T00:00:00.0000000+00:00] Completed: pull database snapshot before opening runtime database"
  ],
  "error": null
}
```

### 6.5 Git Repository Status

```text
GET /api/git-repositories/{repoKey}/status
```

Returns current status for a configured local Git repository.

The endpoint should inspect:

- Repository path exists.
- `.git` exists.
- Current branch.
- Latest commit short SHA and subject.
- Whether working tree is clean.
- Porcelain status lines, with a conservative maximum count.
- Remote names, without exposing remote URLs by default.

Minimum response:

```json
{
  "key": "ronflow-db",
  "displayName": "RonFlow database repository",
  "exists": true,
  "isRepository": true,
  "branch": "main",
  "latestCommit": {
    "sha": "cbbbee4",
    "subject": "Sync RonFlow database: workflow throughput outbox processed"
  },
  "workingTreeClean": true,
  "statusLines": [],
  "checkedAtUtc": "2026-06-14T00:00:00Z",
  "error": null
}
```

The endpoint must run Git with:

- Non-interactive prompts disabled.
- A short timeout.
- Redaction applied to stdout, stderr, and exception messages.

### 6.6 Build Info List

```text
GET /api/build-info
```

Returns all configured build-info sources.

### 6.7 Build Info Detail

```text
GET /api/build-info/{sourceKey}
```

Reads a configured `build-info.json` file and returns its parsed content plus file metadata.

Missing files should return a structured response with `exists: false`.

### 6.8 External Health Checks

```text
GET /api/health-checks
GET /api/health-checks/{sourceKey}
```

Runs configured HTTP checks and reports:

- URL host and path.
- Status code.
- Whether the status code is expected.
- Elapsed milliseconds.
- Error, if the request failed.

The response should not include request headers or credentials.

## 7. Redaction

All text returned from logs, Git output, exception messages, and diagnostic summaries must pass through a redaction step.

Initial redaction patterns must cover:

- GitHub PATs such as `github_pat_*`
- URLs containing credentials, for example `https://token@github.com/org/repo.git`
- Bearer tokens
- Basic authorization headers
- Common connection string password fields such as `Password=...`

Redaction should replace secrets with stable placeholders such as:

```text
github_pat_***
https://***@github.com
Bearer ***
Password=***
```

## 8. Security

The first implementation may be localhost-oriented, but the API must still be designed as a privileged diagnostics surface.

Minimum security requirements:

1. Do not expose arbitrary path parameters.
2. Only configured source keys may be queried.
3. Do not expose full remote URLs by default.
4. Do not expose process environment variables.
5. Do not expose request headers from downstream health checks.
6. Keep tail limits bounded.
7. Prefer authentication before exposing this outside localhost.

If deployed under IIS localhost, the deployment script must grant the diagnostics app pool only the read permissions needed for configured logs and build-info files. Write permissions should not be granted unless a future feature explicitly requires them.

## 9. Implementation Shape

The first implementation should use a small ASP.NET Core API project:

```text
code-backend/RonFlow.Diagnostics.Api
```

Suggested internal services:

- `DiagnosticsOptions`
- `ILogSourceReader`
- `ILogRedactor`
- `IGitRepositoryInspector`
- `IBuildInfoReader`
- `IConfiguredHealthCheckRunner`

The implementation should not reference RonFlow domain or application layers.

## 10. Testing

Unit or integration tests should cover:

1. Unknown source key returns `404`.
2. Missing configured log file returns structured `exists: false`.
3. Tail limit is enforced.
4. Redaction removes GitHub PATs and credentialed GitHub URLs.
5. Git repository status returns clean/dirty state for a temporary repository.
6. Git command timeout or failure returns a structured error.
7. Build-info reader handles missing and valid JSON files.
8. Health check runner treats configured expected status codes as success.
9. Centralized log provider maps service/environment/time range/tail settings into a bounded backend query.
10. Centralized log provider failures return structured redacted errors.

## 11. Acceptance Criteria

1. `RonFlow.Diagnostics.Api` exists as a standalone backend project.
2. `GET /api/health` returns `200 OK`.
3. Configured log sources can be listed.
4. A configured log source can be tailed with bounded line count.
5. Log and Git outputs redact secrets before returning responses.
6. A configured Git repository status can be queried without modifying the repository.
7. Configured build-info files can be queried.
8. Configured HTTP health checks can be queried.
9. The app can be deployed to localhost IIS independently from RonFlow API and RonAuth API.
10. Database Git sync information can be supported only by configuration, without hard-coded dependency on the database Git sync implementation.
11. File and centralized log sources can be configured without changing the API route surface.
