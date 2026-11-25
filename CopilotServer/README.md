# Copilot Server API

ASP.NET Core web server that hosts a headless GitHub Copilot runtime via Microsoft.Copilot.SDK and exposes session CRUD over HTTP.

## Run

```bash
cd /workspace/EHR/CopilotServer
dotnet run
```

The API listens on http://localhost:5000 by default.

Swagger UI: http://localhost:5000/swagger

## Endpoints

* `GET /api/sessions` – list session IDs
* `POST /api/sessions` – create a session
  ```json
  { "sessionId": "user-123", "model": "gpt-5" }
  ```
* `GET /api/sessions/{id}` – get session metadata
* `DELETE /api/sessions/{id}` – delete session

## Notes

* The service starts the Copilot runtime in-process on startup via `CopilotClient.StartAsync()`.
* Session objects are tracked in memory for demo purposes. Production should persist session state and use proper auth.
* Requires a GitHub token or BYOK configuration for the Copilot runtime.
