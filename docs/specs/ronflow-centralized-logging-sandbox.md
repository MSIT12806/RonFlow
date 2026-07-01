# RonFlow Centralized Logging Sandbox Spec

## Purpose

RonFlow needs a reproducible local centralized logging sandbox so `RonFlow.Diagnostics.Api` can dogfood centralized log providers without making centralized logging a production prerequisite.

The sandbox should prove that RonFlow-related logs can be collected, indexed, queried through a GUI, queried through an API, and read through Diagnostics.Api configuration.

## Selected Stack

The initial stack is:

- OpenSearch
- OpenSearch Dashboards
- Fluent Bit

Rationale:

- OpenSearch exposes an Elasticsearch-compatible `_search` API, which matches the Diagnostics.Api centralized provider contract.
- OpenSearch Dashboards gives a local GUI query surface.
- Fluent Bit is lightweight and can tail IIS-hosted logs and sample logs through configuration.
- The stack is deployable with Docker Compose and does not require changes to RonFlow.Api or RonAuth.Api.

Rejected for the first sandbox:

- Full ELK: heavier for single-machine dogfooding.
- Loki/Grafana: good option, but its query API is different from the current Diagnostics.Api Elasticsearch/OpenSearch provider.
- Production OpenTelemetry pipeline: useful later, but too broad for the first local sandbox.

## Deployment

The deployable sandbox lives in:

```text
scripts/centralized-logging
```

Start command:

```powershell
docker compose -f .\scripts\centralized-logging\docker-compose.yml up -d
```

Stop command:

```powershell
docker compose -f .\scripts\centralized-logging\docker-compose.yml down
```

Destroy indexed sandbox data:

```powershell
docker compose -f .\scripts\centralized-logging\docker-compose.yml down -v
```

## Runtime Endpoints

- OpenSearch API: `http://localhost:9200`
- OpenSearch Dashboards: `http://localhost:5601`

Security plugins are disabled in the sandbox. Do not expose these ports outside localhost.

## Log Sources

Fluent Bit collects:

- `C:\inetpub\ronflow-api\logs\*.log` as `service.name=ronflow-api`
- `C:\inetpub\ronauth-api\logs\*.log` as `service.name=ronauth-api`
- `C:\inetpub\ronflow-api\App_Data\database-git-sync.log` as `service.name=database-git-sync`
- `scripts\centralized-logging\sample-logs\*.log` as `service.name=ronflow-sample`

The sample source is intentional. It lets the sandbox validate ingestion and queries even when IIS logs are not present on a fresh machine.

## Field Contract

Events should be queryable with:

- `@timestamp`
- `message`
- `service.name`
- `service.environment`

The Fluent Bit OpenSearch output uses `Replace_Dots On`, so OpenSearch documents may contain `service_name` and `service_environment`. Diagnostics.Api centralized queries must support both the dotted field names and the underscore-normalized sandbox field names.

The index pattern is:

```text
ronflow-logs-*
```

## Redaction

Fluent Bit applies ingress redaction with `scripts/centralized-logging/fluent-bit/redact.lua`.

It redacts:

- GitHub PATs
- Bearer tokens
- Basic auth headers
- credentialed URLs
- `Password=` and `Pwd=` fragments

Diagnostics.Api still applies egress redaction when returning query results.

## API Query

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri 'http://localhost:9200/ronflow-logs-*/_search' `
  -ContentType 'application/json' `
  -Body '{"size":5,"query":{"bool":{"filter":[{"term":{"service.name":"ronflow-sample"}},{"term":{"service.environment":"localhost"}}]}}}'
```

## GUI Query

1. Open `http://localhost:5601`.
2. Create an index pattern for `ronflow-logs-*`.
3. Query `service.name: ronflow-sample` or one of the IIS-backed service names.

## Diagnostics.Api Integration

Use:

```text
scripts/centralized-logging/diagnostics.appsettings.example.json
```

The sample source points Diagnostics.Api at:

```text
http://localhost:9200/ronflow-logs-*/_search
```

Expected Diagnostics.Api verification route after applying the example settings:

```text
GET /api/logs/ronflow-centralized-sample?tail=5
```

## Limitations

- Requires Docker Desktop or another Docker-compatible runtime.
- Windows Docker file sharing must allow mounts from `C:\inetpub` and the repository directory.
- This is not highly available, multi-tenant, or capacity-planned.
- It should not be exposed beyond localhost without adding authentication, TLS, and resource controls.
