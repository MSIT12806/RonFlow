# RonFlow Centralized Logging Sandbox

This sandbox provides a local centralized logging target for `RonFlow.Diagnostics.Api`.

## Stack Choice

The first sandbox uses OpenSearch, OpenSearch Dashboards, and Fluent Bit.

- OpenSearch exposes an Elasticsearch-compatible `_search` API that matches the Diagnostics.Api centralized provider.
- OpenSearch Dashboards gives a GUI for quick dogfooding queries.
- Fluent Bit is small, configuration-driven, and can tail IIS log files plus sample files without changing RonFlow.Api or RonAuth.Api.

This is a localhost sandbox, not a production logging cluster.

## Ports

- OpenSearch API: `http://localhost:9200`
- OpenSearch Dashboards: `http://localhost:5601`

Security plugins are disabled because this stack is intended for local dogfooding only.

## Start

From the repository root:

```powershell
docker compose -f .\scripts\centralized-logging\docker-compose.yml up -d
```

Wait until OpenSearch is healthy:

```powershell
Invoke-WebRequest http://localhost:9200/_cluster/health -UseBasicParsing
```

## Logs Collected

Fluent Bit tails these sources:

- `C:\inetpub\ronflow-api\logs\*.log` as `service.name=ronflow-api`
- `C:\inetpub\ronauth-api\logs\*.log` as `service.name=ronauth-api`
- `C:\inetpub\ronflow-api\App_Data\database-git-sync.log` as `service.name=database-git-sync`
- `scripts\centralized-logging\sample-logs\*.log` as `service.name=ronflow-sample`

If an IIS path does not exist yet, the sample log source still lets the sandbox prove ingestion, GUI search, API search, and Diagnostics.Api wiring.

## Field Contract

Each indexed event should include:

- `@timestamp`
- `message`
- `service.name`
- `service.environment`

The Fluent Bit OpenSearch output is configured with `Replace_Dots On`, so indexed documents may store the service fields as `service_name` and `service_environment`. `RonFlow.Diagnostics.Api` supports both the dotted OpenTelemetry-style names and the underscore-normalized names.

Indexes are written with the `ronflow-logs-*` pattern.

## Redaction

Fluent Bit applies `fluent-bit/redact.lua` before events leave the shipper. It redacts:

- GitHub PATs
- Bearer tokens
- Basic auth headers
- credentialed URLs
- `Password=` and `Pwd=` connection string fragments

Diagnostics.Api also applies output redaction after querying the backend. The sandbox intentionally keeps both ingress and egress redaction layers.

## API Query

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri 'http://localhost:9200/ronflow-logs-*/_search' `
  -ContentType 'application/json' `
  -Body '{"size":5,"query":{"bool":{"filter":[{"term":{"service.name":"ronflow-sample"}},{"term":{"service.environment":"localhost"}}]}}}'
```

## Diagnostics.Api Wiring

Use `diagnostics.appsettings.example.json` as the source shape for `Diagnostics:LogSources`.

The sample source is:

```json
"ronflow-centralized-sample": {
  "Provider": "OpenSearch",
  "DisplayName": "RonFlow centralized sample logs",
  "Centralized": {
    "Endpoint": "http://localhost:9200",
    "IndexPattern": "ronflow-logs-*",
    "QueryPattern": "message:*",
    "ServiceName": "ronflow-sample",
    "Environment": "localhost",
    "TimeRangeMinutes": 120,
    "MaxTailLines": 100
  }
}
```

After deploying the appsettings change, verify through Diagnostics.Api:

```powershell
Invoke-RestMethod http://localhost/ronflow-diagnostics-api/api/logs/ronflow-centralized-sample?tail=5
```

## GUI Query

Open `http://localhost:5601`, create an index pattern for `ronflow-logs-*`, then filter by:

```text
service.name: ronflow-sample
```

## Stop

```powershell
docker compose -f .\scripts\centralized-logging\docker-compose.yml down
```

To remove indexed sandbox data:

```powershell
docker compose -f .\scripts\centralized-logging\docker-compose.yml down -v
```

## Windows Notes

Docker Desktop must be able to mount `C:\inetpub`. If that mount is not available, keep the stack running with the sample log source and later add a host-side shipper such as Windows Fluent Bit, Filebeat, or OpenTelemetry Collector.
