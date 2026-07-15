# Git hooks

The repository uses `core.hooksPath` to enable the hooks in this directory.

The `post-commit` hook starts the existing `RonFlowLocalhostDeploy` Windows
Task Scheduler job. The scheduled task runs the full deployment with elevated
IIS permissions, so the commit hook itself does not request UAC elevation.

Deployment status and logs are written under:

```text
%LOCALAPPDATA%\RonFlow\localhost-deploy
```

If the scheduled task has not been installed on a machine, run this once from
PowerShell 7 and approve the UAC prompt:

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\deployment\Install-LocalhostDeployScheduledTask.ps1
```

The hook is intentionally non-blocking for the commit: a deployment-start
failure is reported as a warning, while the commit remains successful.
