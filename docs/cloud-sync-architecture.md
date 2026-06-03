# Cloud Sync Architecture Notes

This document records the cloud sync design used for the Microsoft Store branch. The GitHub community edition does not ship these UI entries, service classes, settings sections, or sync crypto helpers.

## Product Boundary

- GitHub community edition: local settings, local SQLite history, local templates, local favorites, local OCR, and user-configured model providers.
- Microsoft Store branch: may add user-initiated sync features such as OneDrive folder sync or WebDAV sync.
- No edition should silently upload prompts, OCR text, images, API keys, diagnostics, or screenshots.
- Sync must be opt-in. The user chooses or confirms the target location and starts sync manually.

## OneDrive Folder Sync

The intended Store branch approach is local-folder sync, not Microsoft Graph upload:

- Keep the main SQLite database in the normal local app data directory.
- Let the already-installed OneDrive desktop client handle cross-device transfer.
- Write only encrypted JSON snapshots into the chosen OneDrive folder after the user clicks sync.
- Do not ask for Microsoft Entra client ids, MSAL login, or app-hosted server credentials.
- Wake the OneDrive client after a manual export when available, but do not treat OneDrive as an app backend.

Typical Store-branch service responsibilities:

- `OneDriveLocalFolderService`: detect candidate local OneDrive folders, create chosen sync directories only during manual sync, retry transient file locks, back up replaced files, and detect conflict copies.
- `OneDriveHistorySyncService`: export/import encrypted history snapshots, update the sync manifest, verify hashes, and avoid overwriting newer local records with older snapshots.
- `OneDriveClientLauncherService`: find and start `OneDrive.exe /background` only after a user-triggered sync.
- `OneDriveVaultCacheService`: optionally store remembered local vault keys in Windows Credential Manager, scoped to the selected sync folder.
- `OneDriveSyncModels`: define manifest paths, encrypted document records, tombstone paths, and schema versioning.

## WebDAV Sync

The intended Store branch WebDAV path follows the same encrypted snapshot model:

- Support providers such as Nutstore / Jianguoyun, Nextcloud, and other WebDAV-compatible storage.
- Require a user-provided server URL, username, and app password.
- Store the WebDAV app password in Windows Credential Manager.
- Keep the end-to-end encryption passphrase separate from the WebDAV app password.
- Run connection tests and sync only from explicit user actions.
- Do not run hidden startup probes against a WebDAV server.

Typical Store-branch service responsibilities:

- `WebDavRemoteStoreService`: perform `MKCOL`, `PROPFIND`, `GET`, and `PUT`, back up replaced remote files, detect conflict files, and avoid provider SDK lock-in.
- `WebDavHistorySyncService`: push and pull encrypted history snapshots, verify hashes, scope remembered vault keys to the configured remote, and merge by record update time.

## Conflict And Recovery Rules

- Back up the previous version before replacing any sync snapshot or manifest.
- Stop sync and ask the user to resolve manually when conflict-copy files are detected.
- Treat cloud snapshots as imports, not the primary database.
- Prefer newer record update timestamps when merging history.
- Keep API keys, model-send audit logs, OCR diagnostics, raw screenshots, temporary image files, and plaintext history out of the sync directory.

## Repository Rules

- GitHub community releases must not include OneDrive/WebDAV settings UI or service classes.
- Release checks should fail if community builds contain `oneDriveSync`, `webDavSync`, `OneDrive*SyncService`, `WebDav*SyncService`, or `PromptInputMethod.Core.Sync`.
- Store-branch sync work should be documented here before the service list grows again.
