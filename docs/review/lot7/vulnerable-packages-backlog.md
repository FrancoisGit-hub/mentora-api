# Vulnerable packages backlog (Lot 7 / E1.1)

Source: `dotnet list package --vulnerable --include-transitive`, captured in
`docs/review/lot7/packages-before.txt` before the JWT upgrade.

`System.IdentityModel.Tokens.Jwt` (GHSA-59j7-ghrg-fj52) is excluded from this list —
it is the subject of the current sub-lot (E1.1) and is handled in section 4, not here.

Everything below is **out of scope for E1.1**. Listed for backlog tracking only, no
fixes or upgrades applied in this session.

| Package | Resolved version | Severity | Advisory | Reference type |
|---|---|---|---|---|
| `Microsoft.Extensions.Caching.Memory` | 8.0.0 | High | https://github.com/advisories/GHSA-qj66-m88j-hmgj | Transitive (Mentora.API, Mentora.Infrastructure) |
| `Microsoft.IdentityModel.JsonWebTokens` | 7.0.3 | Moderate | https://github.com/advisories/GHSA-59j7-ghrg-fj52 | Transitive (Mentora.API, Mentora.Infrastructure) |
| `Npgsql` | 8.0.0 | High | https://github.com/advisories/GHSA-x9vc-6hfv-hg8c | Transitive (Mentora.API, Mentora.Infrastructure — pulled in via `Npgsql.EntityFrameworkCore.PostgreSQL`) |
| `System.Text.Json` | 8.0.0 | High | https://github.com/advisories/GHSA-hh2w-p6rv-4g7w | Transitive (Mentora.API) |
| `System.Text.Json` | 8.0.0 | High | https://github.com/advisories/GHSA-8g4q-xg66-9fp4 | Transitive (Mentora.API) |

Note: `Microsoft.IdentityModel.JsonWebTokens` 7.0.3 shares the same advisory
(GHSA-59j7-ghrg-fj52) as `System.IdentityModel.Tokens.Jwt` 7.0.3 and is expected to
be resolved as a side effect of aligning the `Microsoft.IdentityModel.*` graph in
section 4.2 — listed here for completeness since it is a distinct package ID, not
because it needs separate work.
