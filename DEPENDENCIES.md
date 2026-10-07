# DigifyCX Intranet — Dependencies and Configuration

Companion to [HANDOVER.md](HANDOVER.md). Based on repository documentation and configuration/source inspected on 7 October 2026; the live deployment must be reconciled before sign-off. Package manifests and lockfiles remain authoritative for exact transitive versions; this is the operational dependency list, not a frozen software bill of materials.

| Dependency | Required setup / configuration | Source or handover action |
| --- | --- | --- |
| Runtime/packages | ASP.NET Core 8 hosting bundle, IIS, SQL Server; EF Core, Identity/Negotiate, Quartz, ClosedXML, HtmlSanitizer | DigifyCXIntranet.csproj; package.json/package-lock.json for frontend tooling |
| Database/auth | ConnectionStrings:DefaultConnection; IIS/Windows identity; activation credentials; DataProtection key ring | Server environment and service identity; preserve protected key-ring access during transfer. |
| Zendesk | ZendeskSync:Subdomain/BaseUrl/Email/ApiToken; ZendeskWebhook:Secret; allowed Guide sections | Rotate token and webhook secret; update sender and receiver together. |
| Employee registry | UserRegistrySync:SheetApiUrl and Google Sheet/Apps Script access | Integration verifies employees. Confirm removal behavior before a sync; it is not an attendance-editing interface. |
| Email | Smtp host/port/TLS/username/password/from address and routing inboxes, when enabled | Rotate SMTP credentials; verify company sender and intended recipient configuration. |
| Operations | Uploads, SQL backups, App_Data outbox, Quartz schedules, logs and DataProtection keys | appsettings.Production.example.json; deploy/README.md; deploy/LIVE-RUNBOOK.md; docs/system-context.md |

## API and OAuth completion requirements

For **every enabled API/OAuth integration**, record its accountable owner, provider/project, credential name, scopes, secret-store location, endpoint/redirect URI, expiry/renewal behavior and dependent consumers in the private operations register. Rotate/reissue all applicable keys, client secrets, tokens, grants and deployment credentials; configure each consumer; test the new identity; then revoke the superseded credentials. See the ordered procedure in [HANDOVER.md](HANDOVER.md).

Never put secret values in this file. If the live environment has additional integrations, add their non-secret dependency details before handover sign-off. Items absent from inspected source are unverified, not automatically unnecessary. This documentation update does not perform credential rotation or modify runtime settings.
