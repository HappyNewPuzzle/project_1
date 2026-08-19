# SocketStudy Operations Runbook

## Deploy

1. Run CI restore, release build, and protocol tests.
2. In Production set TLS PFX/password, database provider, admin IDs, and log level.
3. Deploy the new image, wait for readiness, then drain the old instance.

## Backup

- Take a SQLite online backup before schema or application deployment.
- Verify every backup with `PRAGMA integrity_check`.
- Keep backups outside the container volume and test restore regularly.

## Incident

1. Check lifecycle, readiness, JSON logs, active connections, reject count, and p99 latency.
2. Set the instance to draining and remove it from gateway routing.
3. Preserve logs and database files before restart.
4. Restore only a backup that passes integrity verification.
5. Record timeline, impact, root cause, and follow-up tests.

## Certificate Rotation

Distribute the next CER pin, replace the server PFX, verify the new thumbprint, then remove the old pin after the overlap period.
