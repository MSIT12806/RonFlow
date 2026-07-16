# Git hooks

The repository uses `core.hooksPath` to enable the hooks in this directory.

The `post-commit`, `post-merge`, and `post-rewrite` hooks use the current Git
`HEAD` revision to start the existing `RonFlowLocalhostDeploy` Windows Task
Scheduler job only when the version changed. This covers commits, normal
`git pull`, and `git pull --rebase` without deploying the same revision twice.

The scheduled task runs the full deployment with elevated IIS permissions, so
the Git hook itself does not request UAC elevation.

Deployment status and logs are written under:

```text
%LOCALAPPDATA%\RonFlow\localhost-deploy
```

If the scheduled task has not been installed on a machine, run this once from
PowerShell 7 and approve the UAC prompt:

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\deployment\Install-LocalhostDeployScheduledTask.ps1
```

The hook is intentionally non-blocking for the Git operation: a
deployment-start failure is reported as a warning, while the commit or pull
remains successful.


