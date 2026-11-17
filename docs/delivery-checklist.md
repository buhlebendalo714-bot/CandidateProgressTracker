# Candidate Tracker/LMS delivery checklist

Updated 17 November 2026 (SAST). This is the single current delivery checklist;
older proposals/handoffs are historical. Later explicit conversation decisions
supersede draft requirements, including team access, approval/reopening and archives.

## Interval boundary

Current interval complete: isolated live-Chrome bulk onboarding acceptance passed
with unchanged HTTP/form/authentication and 45-second loader-readiness assertions.
MVP implementation/acceptance checklist updated below. Stop for user review; no new
feature or production work. Earlier resource stalls are not proven permanently fixed.
Android remains explicitly deferred.
Course/file acceptance is complete (Theory 1 / 1 approved, displayed `100,0%`).
Account-lifecycle acceptance is complete; do not repeat it. The attached November
handover is context, not approval to execute its embedded UI-design prompt.
The user accepts the shared
shell and Candidate/Mentor/Administrator dashboards as the initial UI framework;
evidence-editor browser review passed. Further visual refinement is deferred.
Prior full integration and affected retries remain recorded below. No new full run
or other feature work in this interval. Stop at this acceptance checkpoint.
No new business features, schema changes or production work.
Future intervals cover one major feature or a small related group, aiming for a
20?30-minute checkpoint without skipping necessary checks. Full integration is
recorded below with affected retries; the initial full run was not green. Preserve
unfinished work.

## Bulk onboarding acceptance completed ? 17 November 2026 (latest checkpoint)

- **Passed 1/1**, 0 failed/skipped, 1m52s:
  `TestResults/bulk-onboarding-assets.trx`. Exactly one affected browser run in this
  continuation; no preview/import/full-suite rerun. Prior failed attempts retained
  in their TRX files and historical checkpoint notes below.
- Actual installed Chrome, HTTPS Kestrel, Razor/Identity login and antiforgery forms,
  fixture-owned unique temporary database and CSV files. Passed cohort Members ?
  bulk onboard ? header-only template; actual duplicate file upload denies confirm;
  valid Create / Enrol existing / Skip preview; confirm ? Created / Enrolled /
  Skipped persisted results; reload; private no-store attachment CSV; existing
  reasoned activation management screen without issuing credentials; batch audit filter.
- Final database assertions passed: expected three row outcomes, exactly one new
  membership for the new account, passwordless/disabled pending account, unchanged
  credential count. Existing focused read-only/duplicates/conflicts/concurrency/
  rollback/revocation/archive/fresh-scope recovery evidence remains prior passing
  coverage, not a claim that every case was rerun in Chrome.
- Test-only middleware now records static request start/end, status and elapsed
  milliseconds without query/header/body data. Two exact previously pending assets
  were probed anonymously before Chrome: Bootstrap CSS HTTP 200 (3.22s roundtrip),
  jQuery HTTP 200 (1.39s). Instrumented browser resources then completed HTTP 200.
  Existing 45-second DOMContentLoaded/new-loader requirement and authorization were
  unchanged; no scripts blocked, responses fabricated or credentials injected.
- Supported diagnosis remains an intermittent resource-completion stall in earlier
  isolated browser runs. This successful run included serial asset probes and the
  owned Development site stopped for the build/test; their individual causal effect
  is not established. No confirmed application defect or permanent resource fix.
- Evidence: `TestResults/onboarding-browser/acceptance.json`, sanitized
  `browser-output.txt`, and the TRX above. No credentials, account DOM/HAR or
  screenshots retained. Temporary CSV files/browser context/test database owned by
  this test; Development accounts/data/storage not changed.
- Test-project build **0 warnings/errors**; Node syntax and diff check passed.
  No business/UI/schema changes, migration, timeout/security changes, SQL restart,
  unrelated process termination, commits, pushes or deployment.
- Bulk onboarding Intervals 1?4 implementation and this bounded automated acceptance
  are complete. Manual owner review remains distinct. Native spreadsheet opening,
  hard process termination during commit/power loss and physical Android are not
  certified; broader operational prerequisites remain separately deferred.
- Previous restart was not executed because automatic approval review hit a usage
  limit; this was a review failure, not a determination the action was unsafe.
  On continuation, startup succeeded with bootstrap passwords disabled (session
  58721); HTTPS login returned **200** at `https://localhost:7231`. Site left
  running. Documentation/diff check passed; no tests repeated in this finalization.

## Onboarding browser diagnostic continuation ? 17 November 2026

- Added path-only pending-resource and lifecycle diagnostics to the existing browser
  helper. Queries, headers, body/DOM and credentials are not logged. Existing HTTP,
  real-form, loader and 45-second DOMContentLoaded requirements remain unchanged.
- Exactly one affected browser run: `TestResults/bulk-onboarding-browser-diagnostic.trx`,
  **0 passed / 1 failed / 1 total**, 1m8s. Login loaded and submitted successfully.
  Administrator HTML returned HTTP 200 and its loader initialized, but seven local
  resources remained pending: Bootstrap/site/scoped CSS, logo, jQuery, Bootstrap
  script and site script. No matching DOMContentLoaded before the deadline.
  Failed before cohort navigation/template/upload/confirmation. No acceptance claimed.
- Supported diagnosis: local page resources stalled in this isolated live-browser
  run, preventing observed DOM readiness. No external scripts are referenced by the
  Members validation partial. Root cause of the resource stall remains unresolved;
  no captured application exception or evidence proving a SQL/business defect.
  Resource pressure is prior context, not newly proven as the cause here.
- Sanitized stage/resource evidence: `TestResults/onboarding-browser/browser-output.txt`.
  No browser rerun, assertion relaxation, timeout increase, C# change/build or full
  suite. Previous seven passing focused cases remain prior evidence.
- Existing Development server (session 42892) was left running; fresh login HTTP
  **200** (0.58s). Two of the pending fingerprinted paths independently returned
  **200** there: Bootstrap CSS (0.20s), jQuery (0.03s). This confirms those Development
  assets respond now; it does not prove all isolated-host resources or browser readiness.
- Node syntax and `git diff --check` passed. No Development account/data/schema change,
  migration, SQL restart, unrelated process termination, commit, push or deployment.
- Next bounded work: isolate static-asset completion in the disposable HTTPS test
  host using per-resource response/timing and safe browser/runtime diagnostics.
  Fix only a demonstrated harness/application defect, retain readiness assertions,
  then run only affected onboarding browser acceptance. No new feature interval.

## Bulk cohort onboarding ? Interval 4 checkpoint (17 November 2026)

- Added opt-in `OnboardingBrowserAcceptanceTests.cs` and
  `Browser/onboarding-journey.cjs`, reusing existing installed-Chrome isolated
  contexts, actual HTTPS host, secret-safe logging and loader-correlated readiness.
  Shared browser launcher only gains an onboarding dispatch/output directory;
  existing course/archive assertions and 45-second navigation deadline unchanged.
- Intended journey: cohort Members navigation, template, actual duplicate/valid CSV
  file selection, Create / Enrol existing / Skip preview, confirm/persisted results,
  private CSV, existing activation screen without issuance, batch audit search.
  These browser journey assertions have **not passed**.
- Extended existing failed-row regression: after membership-write failure, earlier
  committed identity/membership/rowversion retained and unfinished identities absent;
  removed Administrator role denies read/resume; archiving cohort pauses work;
  disposable eligibility restored and a fresh service scope resumes unfinished rows.
  No hard process termination, commit-ambiguity or power-loss certification claimed.
- Build **0 warnings/errors**. Both browser scripts passed Node syntax. One focused
  run: `TestResults/bulk-onboarding-acceptance.trx`, **7 passed / 1 failed / 8 total**,
  reported duration 1m31s. All three preview and four import cases passed, covering
  duplicate/conflict/read-only rules, concurrent confirmation, credentials/history,
  HTTP export/access/audit and the expanded partial recovery checks.
- The failed live Chrome case (Chrome 157.0.8103.79) logged successful Login and
  Administrator redirect, HTTP 200. Next cohort Members GET returned HTTP 200,
  but the new document's DOMContentLoaded was not observed within 45 seconds.
  Browser stopped before template/upload; no import or activation in that journey.
  No server exception captured. Evidence supports a navigation-readiness timeout,
  not a confirmed application/SQL defect. No repeated attempt or timeout increase.
  Sanitized evidence: `TestResults/onboarding-browser/browser-output.txt`;
  no credentials, DOM, HAR or account screenshots retained.
- Application/business/UI/schema unchanged. No Development accounts/data altered,
  migration, full suite, SQL restart/security change, unrelated process termination,
  commits, pushes or deployment. Only exact owned site stopped for the build.
- Remaining acceptance: complete the live onboarding journey after safe readiness
  diagnosis. Native spreadsheet opening, hard process-crash recovery and Android
  are not claimed. Next bounded interval: diagnose this Members navigation timeout
  with safe event/readiness evidence, then affected browser acceptance only; do not
  repeat passed preview/import checks without a relevant change.
- Final local startup succeeded with bootstrap passwords disabled (session 42892);
  HTTPS login **200** at `https://localhost:7231`. Site left running.
  `git diff --check` passed. Stopped at this incomplete acceptance checkpoint.

## Bulk cohort onboarding ? Interval 3 (17 November 2026)

- Results now link successful/matched rows to existing account management; new
  accounts have an explicit activation handoff label. Existing reason/version/
  antiforgery lifecycle controls remain authoritative. No automatic credential
  issuance, email, password reset or role/profile changes.
- Added owner/current-Administrator results download through the same authorized
  service read as the saved batch. UTF-8 BOM CSV includes row, username, status and
  details only; partial results remain explicit. Quoting plus literal-apostrophe
  neutralization protects formula-like cells, including leading whitespace.
  Response is an attachment with no-store; raw uploads and credentials excluded.
- Batch audit link uses the existing record filter. Existing transactional audit
  capture records batch/row/account/membership changes; no new parallel audit store.
- Build passed with **0 warnings/errors**. Focused affected run, once:
  `TestResults/bulk-onboarding-results.trx`, **12 passed / 0 failed / 0 skipped**,
  41 seconds. Eight spreadsheet quoting/formula cases and four import cases cover
  protected HTTP confirm/export, UTF-8 BOM/headers, handoff link, owner/non-owner/
  Candidate access, unchanged credential/audit counts on export, batch/account/
  membership audit events and batch-filter search; atomic rollback/resume, concurrent
  confirmation and stale/revoked import safeguards also passed.
- No full suite, new migration, Development test accounts, deployment or UI redesign.
  Existing Development data and approved migration preserved. No physical browser,
  native spreadsheet-opening or hands-on activation acceptance claimed this interval.
- Remaining: Interval 4 end-to-end/browser acceptance and broader interrupted/revoked
  processing checks; physical Android remains explicitly deferred. Stop for review.
- Site restarted with bootstrap passwords disabled (session 32499); final HTTPS
  login returned **200** at `https://localhost:7231`. Site left running.
  `git diff --check` passed. No commits or pushes.

## Bulk cohort onboarding ? Interval 2 (17 November 2026)

- Implemented protected, actor/cohort-bound 30-minute confirmation tickets; preview
  still creates no accounts, memberships or batches. Confirmation revalidates the
  full preview fingerprint; changed eligibility requires a fresh preview.
- Batch records store validated fields/plan/hash, not raw uploaded CSV or credentials.
  Unique request ID plus payload/plan checks prevent changed-request reuse. Current
  Administrator, cohort and row eligibility are rechecked under existing cohort /
  lifecycle lock ordering. Only the initiating current Administrator can read/resume.
- One transaction per row includes new passwordless pending Candidate, role/state,
  intended membership, row completion and existing automatic audit capture. No
  credential issuance, password reset, silent role grant to an existing account,
  profile change or email. Existing accounts only receive the eligible membership.
- Explicit partial progress; failed-row rollback, earlier completed rows retained,
  retries skip completed rows. Owner-only saved-batch links permit recovery after
  an interrupted request / expired preview ticket. Basic progress is necessary for
  this interval; credential-free results export and activation links remain Interval 3.
- New migration `20261116205953_AddBulkOnboardingBatches`: two new tables only,
  restrictive foreign keys, status/action checks and rowversion. Populated downgrade
  blocked. No existing-table rewrites or backfill. Development application completed
  at the migration checkpoint below.
- Verification: `TestResults/bulk-onboarding-safe-import.trx`: **7 passed / 1 failed
  / 8 total**; the failure was a SQL execution timeout in the populated-upgrade
  metadata query before migration application. Reduced repeated key-metadata
  lookups to one query, retaining all original table/column/row/rowversion comparisons
  and default timeouts. Only the affected upgrade rerun:
  `TestResults/bulk-onboarding-upgrade-final.trx`, **1/1 passed** (1m50s).
  All eight distinct focused cases now have passing coverage; not a second clean
  combined run. No full-suite run.
- Passing import checks: concurrent double confirmation/replay; changed payload
  rejection; existing account profile/password/security stamps/roles and credential
  count preservation; passwordless/pending new account and selected SAST date;
  failed membership write after Identity writes rolls back the new identity;
  earlier completed rows/IDs/rowversions retained; unfinished-only resume; stale
  preview, archived cohort and revoked/non-Administrator denial; real HTTP protected
  ticket/antiforgery confirm, saved batches and corrupt ticket rejection.
- Upgrade passed all original table/column/data/rowversion comparisons, empty new
  tables, and blocked populated downgrade. Failure injection is test-only command
  interception, not hard process-crash certification. Physical browser/manual
  acceptance and broader interruption matrix remain Interval 4.
- Final builds: **0 warnings/errors**. The initial build encountered locks from the
  earlier owned web process; that exact process was stopped and subsequent builds
  were serial. EF pending-model check: **none**. Final diff check recorded below.
- [Reviewed up SQL](migrations/AddBulkOnboardingBatches.sql): transactionally creates
  only `OnboardingBatches`, `OnboardingBatchRows`, indexes/checks/restrictive FKs and
  the migration-history entry. No old data rewrites.
- **Development migration checkpoint (17 November):** user continuation authorised
  the reviewed migration after a fresh recoverable backup. Fresh target confirmed
  `Lutho\SQLEXPRESS` / `CandidateProgressTracker_Dev`, at the expected lifecycle
  predecessor. Stopped only the exact owned web process for backup/comparison.
- COPY_ONLY/CHECKSUM backup, RESTORE VERIFYONLY, an actual uniquely named restore,
  and DBCC CHECKDB all succeeded, serially. Backup retained:
  `C:\Program Files\Microsoft SQL Server\MSSQL17.SQLEXPRESS\MSSQL\Backup\CandidateProgressTracker_Dev_pre_bulk_20261117_416284b521d241d8beb2a779cd1e3a6b.bak`.
  Verified recovery database retained:
  `CandidateProgressTracker_Tests_416284b521d241d8beb2a779cd1e3a6b`.
  No uncertain or other databases deleted.
- Applied `20261116205953_AddBulkOnboardingBatches` successfully with the explicit
  Testing-environment command below. Bidirectional EXCEPT comparisons plus row
  counts against the restore passed for **all 31 original tables**, selecting every
  original column including rowversions. New onboarding tables are empty; migration
  history includes the new migration. Evidence:
  `TestResults/migration-bulk/recovery.json`, `recovery.sql`,
  `compare-template.sql` and `preservation.txt`.
- No new test-suite run, account import, credential issuance, SQL restart, security
  or timeout change. Previous eight-case isolated evidence remains as recorded above.
  No Interval 3 exports/activation handoff, commits, pushes or deployment.
- Final startup succeeded with bootstrap passwords disabled (session 27526);
  HTTPS login returned **200** at `https://localhost:7231`. Site left running.
  `git diff --check` passed. Stop for review; no further interval started.

Exact update command, **successfully executed against Development** after approval
and backup/restore verification:

```powershell
dotnet ef database update 20261116205953_AddBulkOnboardingBatches --project src/CandidateProgressTracker.Infrastructure --startup-project src/CandidateProgressTracker.Web --no-build --connection 'Server=.\SQLEXPRESS;Database=CandidateProgressTracker_Dev;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True' -- --environment Testing --ConnectionStrings:DefaultConnection 'Server=.\SQLEXPRESS;Database=CandidateProgressTracker_Dev;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True'
```


## Bulk cohort onboarding ? Interval 1 (historical preview checkpoint)

- [x] Add Cohort ? Members ? Bulk onboard candidates, a static UTF-8 CSV template
  and one membership start date per batch (SAST). Existing account fields only:
  `UserName`, `Email`; email required for new accounts under existing lifecycle rules.
  No name fields exist. No passwords/roles in CSV, no profile updates.
- [x] Implement a service-authorized read-only preview: normalized username matches,
  duplicate names/emails, Create / Enrol existing / Skip / Conflict summaries,
  matched account identity, row/field corrections, strict UTF-8/CSV and size/row limits.
  Archived cohorts and noncurrent Administrators receive no matching data.
- [x] Confirmation intentionally unavailable. Preview never saves changes, issues
  credentials, alters memberships/roles or sends emails. Static template contains
  headers only; no user values/formulas are exported. Preview is Razor-encoded and
  no-store. Raw uploaded CSV is not persisted by this feature.
- [x] Focused verification: **3/3 passed** in `TestResults/bulk-onboarding-preview-focused.trx`;
  classification, CSV/Identity validation and real HTTP template/antiforgery upload.
  Compared accounts/password/security stamps, roles, memberships/rowversions,
  state/credential counts and audit/change counts: unchanged after preview.
- [x] Clarified scheduled-end handling: a currently enrolled Candidate is skipped
  if the membership remains active through the batch date; ended/future or incompatible
  membership remains Conflict. Affected classification/no-write rerun **1/1 passed**
  in `TestResults/bulk-onboarding-membership-final.trx` (3 distinct cases total).
- [x] Final serial test-project/Web dependency build: **0 warnings/errors**; diff check
  passed. Earlier overlapping build hit a compiler-output lock; remaining build
  completed. An initial filtered command discovered no tests because its build
  predated the test file; rebuilt serially before the passing runs. No assertions
  removed or timeouts increased. No full-suite run or Development migration.
- [x] Local startup succeeded with bootstrap passwords disabled (session 53384);
  login HTTP **200** at `https://localhost:7231`. Site left running for preview review.
- [ ] Manual preview acceptance: Administrator ? Cohorts ? Members ? Bulk onboard
  candidates ? template ? choose one SAST date ? upload ? inspect four action groups.
  Confirm absence of any import action. No visual/browser acceptance claimed here.
- [ ] Interval 2 Development acceptance: implementation/isolated checks passed above;
  reviewed migration applied after approval, verified recovery and exact preservation;
  hands-on import acceptance remains outstanding.
- [ ] Interval 3: per-row results, account/activation handoff, credential-free
  formula-safe results CSV and transactional batch/account/membership audit events.
- [ ] Interval 4: focused end-to-end acceptance including retries and interruptions.

These are newly authorized extension deliverables, not retrospective missing legacy
MVP requirements. XLSX, bulk emails, multi-cohort imports and profile updates remain
outside version one. Do not implement the next interval before preview approval.

## Current checkpoint checklist ? 17 November 2026

Completed and verified:

- [x] Original functional MVP implementation (MVP-01?10) and retained verification
  mapped in the original-scope table below; no second clean full-suite run claimed.
- [x] User-approved bulk onboarding extension: template/read-only preview, protected
  confirmation, atomic/resumable rows, results CSV and account/activation handoff;
  reviewed additive Development migration applied with verified recoverable backup
  and preservation of all 31 original tables. Focused verification recorded above.
- [x] Bulk onboarding live-browser acceptance passed in the latest isolated run;
  actual journey and data assertions above. Earlier failures remain recorded.
  No permanent resolution of intermittent local resource pressure is claimed.

- [x] Remove decorative Overview backgrounds across Candidate, Mentor and Administrator:
  no gradient, geometric accents, watermark or dashboard frost; shared light-grey
  workspace and opaque white panels restored. CAPACITI logo/sidebar/layouts retained.
- [x] Web build: 0 warnings/errors; JavaScript syntax and diff checks passed.
- [x] Current-CSS Edge snapshot replay across three roles at 1366px/360px:
  top/middle/bottom captures, light-theme assertions and no horizontal page overflow
  (18 viewport captures). Earlier authenticated fixture markup, not fresh live login.
- [x] Local startup/SQL recheck passed; latest recorded login HTTP 200. Availability
  is the previous checkpoint observation, not a new check in this documentation update.
- [x] Retain passing functional, file-transfer, lifecycle and governance evidence;
  no full-suite or functional rerun for this styling/documentation update.

Implemented; awaiting manual acceptance:

- [ ] Review the current light Overview dashboards in all three roles after Ctrl+F5;
  inspect desktop/mobile navigation, text, dropdowns, long-page bottom and table scrolling.
- [ ] Complete hands-on Windows file-picker selection/cancel, actual submission,
  browser download and opening the saved disposable certificate. Existing automated
  multipart/Defender/exact-byte Edge download evidence is retained, not OS-picker proof.
- [ ] Record actual outcomes of the remaining current-UI review using the
  [hands-on checklist](native-file-android-ui-acceptance.md). Do not mark manual
  acceptance passed from snapshots or implementation alone.

Deferred / remaining prerequisites:

- [ ] Physical Android Chrome acceptance: explicitly deferred by user; no device setup.
- [ ] Broader accessibility/performance and production operational prerequisites remain
  unverified/deferred as recorded below; targets and policies need agreement first.
- [ ] SQL resource-pressure risk remains: successful startup is not a permanent fix.

No missing agreed functional MVP requirement has been confirmed by the existing
checklist. Bounded onboarding automated acceptance passed as recorded above; manual UI/native
checks and broader operational prerequisites remain pending. Do not start features or production work at this checkpoint.

## Overview light theme ? 16 November 2026

- User requested undoing the Overview dashboard background theme. Removed shared
  dashboard gradient/geometric decorations, fixed logo watermark, frost overrides
  and dark-background-specific text/button/focus overrides from `site.css`.
  Shared light-grey workspace, opaque white panels and normal semantic brand
  controls now apply. CAPACITI sidebar/logo/navigation/layout and behavior retained.
  This explicit direction supersedes the earlier decorative/glass/watermark requests.
- Web build passed: 0 warnings/errors. Node syntax and diff check passed. Focused
  Edge replay uses existing authenticated fixture markup with current shared CSS:
  three roles, 1366px/360px, scrolled top/middle/bottom; theme/no-overflow checks and
  PNGs in `TestResults/dashboard-light/{candidate,mentor,admin}`. This is snapshot
  visual verification, not fresh authenticated workflow or physical Android acceptance.
- Site login HTTP 200 at https://localhost:7231; hard-refresh for stylesheet changes.
  No functional suites, schema, database or business behavior changes. Android deferred.

## SQL/startup recovery checkpoint ? 16 November 2026 (latest availability)

- SQL Express Running/Automatic. Three independent default-timeout `sqlcmd`
  Development queries passed. No SQL restart, data reset, database cleanup,
  configuration/security changes or increased timeouts.
- Available RAM approximately 197 MiB; SQL `process_physical_memory_low=1`,
  `process_virtual_memory_low=0`. Resource pressure remains a supported risk;
  successful rechecks do not establish a permanent fix or exclusive root cause.
- One application startup attempt succeeded with bootstrap account passwords
  disabled. Existing role queries completed in 675/12/1 ms; HTTPS login **200**.
  Site left running at `https://localhost:7231` (owned session 12428).
- No source changes or tests/build needed for this read-only responsiveness/startup
  check. No functional/browser suites rerun. Prior acceptance evidence retained.
- Next: [hands-on desktop native picker/download and current UI review](native-file-android-ui-acceptance.md),
  using disposable records. Android explicitly deferred; no device setup. User must
  record actual manual outcomes before acceptance can be marked passed. Stop here.

## Mentor scrolling checkpoint ? 16 November 2026

- Added opt-in standalone `Browser/mentor-scroll.cjs`: GET-only loopback replay
  of the existing `TestResults/dashboard-branding/mentor/authenticated-snapshot.json`
  with the current shared site stylesheet substituted by asset path. No fixture
  writes or functional workflow/full-suite rerun. Earlier authenticated markup is
  fictional test data, not a fresh authenticated application response.
- Six checks/captures: Edge 157.0.4341.46, widths 1366/360, top/middle/bottom.
  All reported visible content/no page horizontal overflow. Inspected mobile
  middle/bottom and desktop bottom: review/changes panels, cohort table and footer
  render without the prior full-page screenshot's blank bands. This supports a
  capture/compositing explanation; it does not prove fresh live/physical-device
  acceptance. No production styling fix justified or applied.
- Evidence: `TestResults/mentor-scroll/checks.json` (browser, source, current CSS
  SHA-256 and scroll positions) and six PNGs. The driver uses an owned random port
  and profile. Initial capture completed but browser cleanup stalled; only the
  identified owned Node process 7824 and its child browser tree were terminated.
  Cleanup adjusted to close loopback connections and the exact spawned browser
  tree; affected lightweight replay completed with exit code 0. Node syntax and
  diff checks passed (existing CSS line-ending warning only). No unrelated processes touched.
- Local availability: initial HTTPS check returned curl 000; no listener on 7231.
  One startup attempt with bootstrap passwords disabled failed at
  `Program.cs:165` / `RoleManager.RoleExistsAsync`: SqlClient -2 post-login timeout,
  Win32 258 connecting to Development. **Site not available.** No SQL restart,
  settings/security/timeout changes, migration or database cleanup. This recurrence
  is not proof of a new application defect or a sole resource-pressure root cause.
- User chose **Keep Android pending** on 16 November. Physical Android acceptance
  is deferred by explicit request. No USB debugging, installation, LAN binding or certificate changes.
  Physical Android, OS picker and final human UI acceptance remain pending; use
  [hands-on checklist](native-file-android-ui-acceptance.md).
- Next bounded step: restore/confirm local SQL connectivity before live acceptance;
  arrange approved device access separately. Preserve prior passing file-transfer,
  lifecycle and governance evidence; do not rerun them without a relevant need.

## Native transfer / Android / current UI acceptance ? 13 November 2026

- Focused Edge live case **1 passed / 0 failed / 0 skipped**, 4m14s:
  `TestResults/native-edge-ui-final.trx`. Actual browser identity verified:
  **Edg/157.0.4341.46**, using its own dynamically allocated debugging endpoint/profile.
- Actual PDF file-input selection and browser multipart POST, links-only course
  rejection, Processing, real Defender commitment/receipt, private exact-byte
  download and unrelated Candidate denial, scoped Mentor review/download/approval,
  Candidate Approved feedback and Theory 1/1/100 passed. C# final database assertions
  passed. Test-only control drives the real processor; worker scheduling not rerun.
- New native download check clicks the actual attachment link and verifies the
  browser's completed disk file against original bytes; 1 completed / 0 partial files.
  Artifact `TestResults/course-browser/native-download.json`. This is headless Edge
  download-manager coverage, not Windows file-picker or physical-device acceptance.
- First affected attempt failed at the new native download wait (3m10s): download
  behavior targeted the default context while the Candidate used an isolated context.
  Corrected the harness to target that Candidate context; affected rerun above passed.
  No application defect demonstrated; no timeout, HTTP/workflow assertion or file
  security behavior relaxed. Browser shutdown now waits for its close event rather
  than a command reply that may never arrive. Existing archive branch preserved,
  not rerun or newly certified.
- Six actual authenticated pages captured in Edge at 1366px and 360px: all three
  dashboards, certificate form/history and Mentor review. Numeric checks confirm
  loaded logo assets and no page horizontal overflow in all 12 combinations.
  `TestResults/course-browser/native-ui.json` and corresponding PNG files.
  Inspected Administrator desktop/mobile, Candidate mobile/form and Mentor review
  desktop screenshots. Mentor mobile full-page capture has blank regions beyond
  the viewport; screenshot compositing versus actual scrolling remains unresolved.
  Do not label complete current-UI/long-page visual acceptance passed.
- Android phone with Chrome confirmed available by user; version not yet recorded.
  No adb executable available here. Device connection remains unconfigured: PC
  localhost cannot be opened as the same endpoint on a phone. No LAN binding,
  firewall/certificate changes, installation or USB-debugging changes performed.
- [Consolidated hands-on checklist](native-file-android-ui-acceptance.md): native OS
  picker, manual saved-file opening, physical Android chooser/download/keyboard/
  rotation, remaining individual/team native checks and final all-role UI approval.
  Use disposable accounts; do not report credentials. Agree safe device access
  before executing phone steps. No human acceptance fabricated.
- Changed only browser test driver/documentation. Node syntax and final diff check
  passed (existing CSS line-ending warning only). No C# changes/build/full suite,
  migrations, Development writes, database cleanup, process/service restart or UI
  redesign. Final Development login **HTTP 200**, existing site left available at
  `https://localhost:7231`. Prior builds and acceptance remain historical evidence.

## Current release acceptance status (supersedes older interval notes below)

Handoff: [local setup/runbook and verification limits](local-release-handoff.md).
For new tests: [completed deliverables and acceptance criteria](completed-deliverables-test-checklist.md)
(D01–D16). This document remains the sole current delivery-status checklist; the
companion is a test-design reference, not a second status tracker.

Approved UI baseline: [Structured Workspace specification](ui-design-framework.md)
and [preview index](ui-previews/index.html). Standalone previews remain design
references; production implementation and verification are recorded below. Existing
functional acceptance evidence is retained, not substituted by screenshots.

| Classification | Agreed scope | Remaining evidence / action |
| --- | --- | --- |
| New extension; implemented, isolated checks passed and Development migration applied | Bulk cohort onboarding Interval 2 | Preview/confirmation, atomic rows and resumable owner batches tested. Approved additive migration applied after verified backup/restore and exact preservation of all 31 original tables. Results CSV/activation links are implemented and focused checks passed (Interval 3); isolated live onboarding browser acceptance passed (Interval 4), with manual review and stated operational limits retained. |
| Implemented; verified across full + affected runs | FR-01,05-10,12-17,21/22/24: authorised setup, individual/team submission, frozen credit, exact revisions, review/reopening | All initial full-run failures subsequently covered by passing affected retries; details below. Existing user-confirmed individual/team/review/Bob-access acceptance retained. No repeat of passed browser checks requested. |
| Implemented; operational and isolated live-browser acceptance passed | FR-11/14/18; NFR-03/04: courses/private files | Course setup, certificate Processing/history/downloads, isolation, mentor feedback and Theory progress passed in Chrome. Actual EvidenceWorker polling and host restart with a simulated expired lease now passed separately with real Defender, exact-byte downloads and original-receipt retries. Prior PDF/PNG/JPEG, scanner failure/recovery and stale-finalization evidence retained. Production provisioning and hard-process-crash acceptance remain unverified. |
| Implemented; isolated live-browser acceptance passed | FR-02-04; NFR-02: account lifecycle | Actual Chrome login/forms, separate session cookies, activation/recovery expiry and single use, revocation, reasoned reactivation, unchanged ended membership, and last-admin protection passed. Evidence in lifecycle interval below; no Development accounts changed. |
| Implemented; isolated browser acceptance passed | FR-20/25/26; FR-27-30: review queue/progress, admin archives/audit | Course queue/review/feedback/Theory totals passed previously. Project archive/outcome/history/download, current candidate/mentor denial, reasoned scope reopening with approval retained, and actor/action/record/date audit search now passed. Existing data, audit fields and rowversions remained unchanged. This is local functional acceptance, not production readiness. |
| Implemented; automated UI verification passed; visual acceptance pending | Structured Workspace across shared shell, all role dashboards, learning, submissions/history/progress, staff management/governance and account screens | Current Overview baseline is light-grey/opaque white; earlier decorative/glass/watermark styling is superseded. Automated current-CSS snapshot checks passed; user review of current pages remains pending. |
| Missing agreed functionality | None confirmed by the existing delivery checklist | This is not a new scope audit or a claim that passing tests prove scope completeness. Resolve any concrete acceptance defect within its bounded interval. |
| Deferred policy / features | Cancellation; FUT-01/02/03; DOCX; roster correction/supersession | Cancellation remains unavailable pending policy; the others are explicitly outside approved first-release implementation. |
| Deferred polish / production prerequisites | Further optional visual polish; broader accessibility/performance; production hosting, storage/scanner provisioning, keys/secrets, monitoring/support, retention and coordinated restore acceptance | Local functional acceptance is distinct from production readiness. Do not start these in this interval. |

The previously listed essential course, lifecycle and archive/audit journeys now
have recorded isolated browser evidence. No additional missing agreed functionality
is confirmed by this checklist; this is not a new scope audit. Background evidence
worker scheduling now has separate operational acceptance. Remaining limits: original
full suite was not wholly green in one run (affected retries passed), and deferred
nonfunctional/production prerequisites remain unverified as recorded below.

Further visual inspection is optional polish, not an invented functional blocker.

## Original-scope MVP deliverables: achieved and remaining ? 17 November 2026

This is a compact continuation checklist, not another scope audit. Sources:
[original proposal, ?6.1?6.6 and ?9](requirements/Project_Proposal_Candidate_Progress_Tracker.pdf),
[Scope v0.2, ?5?6](requirements/Candidate_Progress_Tracker_01_Scope_and_MVP%20%281%29.md)
and [SRS v0.1, ?8?11](requirements/Candidate_Progress_Tracker_02_SRS.md), with later
explicit user decisions taking precedence. The original PDF was located in Downloads
and copied unchanged into requirements; its SHA-256 matches the source. D01?D16
refer to the completed-deliverables test checklist. Prior results remain historical
unless explicitly rerun. Original proposal ambitions are not all first-release scope.

| Achieved implementation | Scope ID / original proposal reference | Existing delivery/test group |
| --- | --- | --- |
| [x] Login/logout, roles, administrator-managed activation/recovery/deactivation and session revocation | MVP-01; ?6.1 Candidate/Admin | D01?02 |
| [x] Cohorts, dated candidate/mentor membership and preserved association history | MVP-02; ?6.1 Mentor/Admin | D03 |
| [x] Dated cycles, individual/team projects, milestones, assignments and team membership | MVP-03; ?6.1/6.3 | D03/05/07 |
| [x] Course catalogue, cycle offerings and individual theory assignments | MVP-04; ?6.2 | D04 |
| [x] Individual/team evidence, real certificate files, private downloads, actual submitter, frozen credit, receipts and attempt history | MVP-05; ?6.2/6.3 | D06?09/12/13 |
| [x] Scoped mentor decisions, written feedback, resubmission and administrator approval reopening | MVP-06; ?6.5/7.2/7.4 | D10?11 |
| [x] Candidate theory/project progress, actionable deadlines and pending-review separation | MVP-07; ?6.4 | D14 |
| [x] Mentor scoped progress, oldest eligible review navigation and queue | MVP-08; ?6.1/6.4 | D14 |
| [x] Administrator archived outcomes/history/evidence; protected candidate/mentor boundaries | MVP-09; ?6.6 | D13/15 |
| [x] Transactional audit/search, validation, isolation and additive migration/data preservation | MVP-10 functional foundations; ?6.1 audit / ?9 access | D01/09/12/15/16; operational NFR work below remains partial |
| [x] Approved role shell/page patterns and CAPACITI branding; current light-grey Overview workspace and opaque white panels | Later approved UI framework and explicit theme rollback | D16; current light-theme evidence retained; hands-on acceptance pending |
| [x] Bulk cohort onboarding implementation: CSV preview, safe resumable import, per-row results, credential-free export and existing activation handoff | Later explicitly approved extension; not an added original MVP requirement ID | Intervals 1?4 delivered; focused checks and latest isolated live browser acceptance passed; manual review and operational limitations retained |

Remaining acceptance / release work:

- [x] **NFR-06 / AC-14 desktop subset:** 360px/1366px keyboard/error verification
  completed for three core forms in installed Chrome/Edge; evidence below.
- [ ] **NFR-06 hands-on acceptance:** native file selection and core submit/review
  interaction still require user acceptance. Broader accessibility is not certified by screenshots or passing tests.
- [ ] **NFR-07 / AC-14 remaining:** selected Chrome/Edge desktop rendering and
  keyboard checks passed; full native transfer journeys and a representative real
  Android Chrome device remain unverified. A desktop
  mobile viewport is not Android device acceptance.
- [ ] **NFR-05:** agree the representative operating workload, then performance/load
  acceptance. The draft 500-candidate/50-concurrent-user envelope and p95 target are
  proposals, not promises or measurements.
- [ ] **NFR-08 / AC-13:** agree recovery/backup retention targets and demonstrate a
  coordinated database-plus-evidence restore. Earlier database migration restore
  checks and simulated worker lease recovery do not complete this requirement.
- [ ] **NFR-09/10:** production owner/service hours, availability measurement, health
  monitoring, secret-safe error routing, log retention and incident/support procedure.
  Provisioning and operational acceptance remain deferred.
- [ ] **NFR-11/12:** production hosting/storage/scanner/key/secret/CI readiness and
  approved privacy, retention and deletion procedures before live-data onboarding.
  Local architecture, migrations, setup documentation and builds are already delivered;
  production readiness is separate and remains incomplete/unverified.

Explicitly deferred / superseded, **not missing delivered MVP features**:
FUT-01 scorecards, FUT-02 reminders and FUT-03 advanced reporting remain outside
MVP (original ?6.1/6.6/7.3; Scope v0.2 ?6). Presentation scheduling/date records and
rich workload visualisation beyond counts are **proposed later work**, not implemented;
priority/policy needs agreement before starting them (Scope ?6 / SRS ?11). Basic
workload lists and protected histories are implemented.
Also deferred: DOCX; roster
reset/correction/supersession; cancellation pending policy. Older mentor reopening,
former-member history access and draft Identity key choices are superseded by the
agreed administrator-only approval reopening, current-team candidate access and
existing string Identity keys. No newly confirmed missing functional MVP feature is
identified by the existing checklist; this is not a claim that all original proposal
ambitions are implemented. Production work needs a separately agreed interval.

### Core-form usability checkpoint ? 13 November 2026

- New opt-in `UsabilityBrowserAcceptanceTests.cs` reuses WebAccessFixture,
  ReviewTestContext, TeamTestContext, existing form/antiforgery helpers and browser
  snapshot bridge. Authorised HTTP POSTs produce real label-length and blank-feedback
  validation responses; browser replay uses those responses/assets, not static mockups.
- Final focused result: **1 passed / 0 failed / 0 skipped**, **3m32s**,
  `TestResults/usability-browser-close-final.trx`. Three journeys (individual/team
  submission, Mentor review) ? Chrome/Edge ? **1366px/360px = 12 viewport combinations**.
  Actual final identities: **Chrome 157.0.8103.52**, **Edge 157.0.4341.46**.
  Labels/unique IDs, readable error text, retained values, 3px keyboard focus,
  reaching each form action without a trap, no page overflow and mobile drawer/
  Escape behavior passed. Selected error/focus screenshots visually inspected.
- C# final assertions confirmed no extra individual submission, no review decision,
  no team submission and no frozen team roster after rejected requests. No real
  certificate/file-transfer/browser POST acceptance is claimed by the GET-only bridge.
- Initial three focused attempts each failed (**0/1** each): browser teardown stalls
  after completed assertions, plus investigation found the old fixed debugging port
  could connect Edge checks to an existing Chrome instance. Those early results do
  **not** prove Edge compatibility. Final driver reads each new profile's own dynamic
  debugging endpoint and checks browser identity; awaits the browser close event
  instead of a reply/OS command that could stall. Only its owned browser is closed;
  uncertain older processes/databases were left untouched. Timeout diagnostics are
  retained. Assertions and the existing 90-second deadline were not weakened/increased.
  No application defect reproduced or production code changed.
- Evidence: `TestResults/usability/{chrome,edge}-{individual,team,review}/checks.json`,
  desktop/mobile/error/focus/menu PNGs, `summary.json`; earlier failed TRXs retained.
  Changed areas: new test/driver; optional browser-path and timeout diagnostics in
  CandidateDashboardTests helper; original proposal PDF reference; this checklist,
  completed-deliverables test reference and local handoff. No full suite repeated.
- Solution build **0 warnings/errors**; Node syntax and `git diff --check` passed.
  Previous local server was no longer listening; restarted with bootstrap passwords
  disabled. **https://localhost:7231 login HTTP 200**, session 6913. No Development
  data edits, migrations, software installation, security changes, commits/push/deploy.
- Remaining / next bounded acceptance: use current owner and scoped non-self Mentor
  on disposable work; at 360px and 1366px in Chrome/Edge, keyboard through evidence
  fields, inspect/correct a partial-link error and native file selection; inspect
  ChangesRequested feedback error/correction. On a real Android Chrome device record
  device/OS/browser versions and complete submission ? receipt/download ? feedback.
  These hands-on/native-transfer/device checks are not claimed as performed.
  Performance targets/load envelope and production recovery/operations/retention still
  need agreement; do not start them, deferred features or another scope audit here.

### Dashboard glass and fixed watermark ? 12 November 2026

- Shared CSS only in production: softened navy/indigo gradient and edge accents;
  panels use 90% white, 14px backdrop blur, translucent borders, restrained shadows
  and 12px corners. Text/container opacity stays 1. Forms/controls use at least
  98% white; tables and cells reveal the parent surface with retained dividers.
  Top bar has matching frost; sidebar logo area remains opaque white.
- Opaque white/blur-none defaults outside `@supports` provide fallback. Chrome
  CSSOM removal of the support rule verified that fallback; an actual legacy
  browser was not tested. No animation or business/markup/schema changes.
- Official logo watermark uses the existing unchanged asset in an empty CSS
  pseudo-element: fixed viewport-centre, 80% workspace width with natural aspect
  ratio, 8% opacity, negative stacking within an isolated workspace and pointer
  events disabled. Desktop offset excludes sidebar; mobile offset is zero.
  It is deliberately very subtle beneath the 90% white/blurred panels.
- Reused previously authorised isolated dashboard HTML/assets; **3/3 role Chrome
  visual checks passed** at **1440px / 390px** after final styling. Verified logo
  decoding/aspect, fixed watermark position across scrolling, no horizontal overflow,
  panel opacity/radius/blur, nearly opaque forms/controls, keyboard dropdown use,
  account dropdown, mobile table scrolling, drawer/Escape and runtime errors.
  Desktop/mobile/menu/bottom/fallback screenshots inspected. No SQL or functional
  suites repeated for CSS-only changes; previous authorization evidence retained.
- Conservative contrast calculations: exterior text >= **6.47:1**, panel navy text
  >= **11.69:1**, muted text **5.05:1** over the darkest 90%-white composite. Evidence:
  `TestResults/dashboard-branding/{candidate,mentor,admin}/checks.json` and PNGs,
  `glass-watermark-results.json`, `glass-muted-contrast.json`. Each role includes
  `account-dropdown.png` and `mobile-fallback.png` alongside normal screenshots.
- Administrator's first final visual run completed all assertions but stalled waiting
  for Chrome's shutdown reply. Removed that unnecessary reply wait; existing owned
  Chrome process cleanup remains. Only Administrator rerun, **passed**. Assertions
  and deadlines retained; Candidate/Mentor passing checks were not repeated.
- Final Web build **0 warnings/errors**; Node syntax and `git diff --check` passed.
  Running at **https://localhost:7231**, login **200** verified; session 41860,
  bootstrap passwords disabled. No migrations, Development data edits, unrelated
  cleanup, commits, pushes or deployment. Stop for visual review.

### CAPACITI dashboard branding ? 12 November 2026

- Copied supplied `CAPACITI-Logo.webp` unchanged into `wwwroot/images/`; SHA-256
  matches the Downloads original. White sidebar brand area, natural aspect ratio,
  accessible name CAPACITI and existing role dashboard link. No duplicate wordmark
  text beside the image. Shared footer, browser-title suffix and public home branding
  use CAPACITI; internal names unchanged.
- Dashboard-only full-height navy/indigo gradient with static coral/pale edge
  diagonals. CSS layers introduce no interaction overlay, photographs or animation.
  White panels/forms/tables and approved layouts retained. Restored the mobile
  drawer close symbol after screenshot inspection found it visually missing.
- Reused existing Candidate/Staff dashboard tests and authenticated HTTP snapshot
  bridge; added WebP capture and opt-in `CPT_BRANDING_VISUAL=1` driver. Original
  workflow/authorization assertions remain. Initial run **1 passed / 2 failed**:
  conservative contrast check found pale exterior text at **4.48:1** over the
  brightest layered accent. Changed exterior text to white, retaining the check;
  only the two failures rerun: **2/2 passed**. All **three distinct dashboard cases
  passed** across these runs. Artifacts: `dashboard-branding.trx` and
  `dashboard-branding-contrast-fix.trx` under `TestResults/`.
- Actual Chrome at **1440px desktop / 390px mobile**: loaded undistorted logo on
  white, correct role links, title, opaque panels, full growing background, no page
  overflow/runtime errors, skip focus and mobile drawer/Escape behavior. Exterior
  text minimum **5.48:1** against tested gradient/accent combinations. Desktop and
  mobile screenshots for every role visually inspected, including menu/bottom views.
- After close-symbol CSS correction, refreshed all three visual captures from the
  existing isolated authenticated HTML/assets with current CSS: **3/3 passed**,
  without repeating SQL/service tests. These are real authorised Razor responses
  rendered through the existing GET-only bridge; no Development identities used.
  Final evidence: `TestResults/dashboard-branding/{candidate,mentor,admin}/`:
  `desktop.png`, `mobile.png`, `mobile-menu.png`, `desktop-bottom.png`,
  `mobile-bottom.png`, `checks.json`; `final-visual-results.json` records the refresh.
- Solution build and final Web build: **0 warnings/errors**. Node syntax and
  `git diff --check` passed. No full suite, migrations, Development data edits,
  unrelated cleanup, commits, pushes or deployment.
- Running at **https://localhost:7231**, HTTPS login **200** verified; session 40727,
  bootstrap passwords disabled. Stop for user visual review; no remaining confirmed
  blocker in this branding interval. Broader accessibility/production work remains
  deferred. Framework and completed-deliverables test reference updated.

### Structured Workspace implementation ? 12 November 2026

- Applied shared CAPACITI navy navigation, light content surfaces, accessible focus,
  responsive page families, consistent forms/filters, scrollable record tables and
  account screens. Existing dashboards inherit the shared design. Candidate
  assignment lists, courses, individual/team submission, Processing, history and
  progress use the same patterns. Administrator People/Learning/Governance pages
  and Mentor queue/course/project/progress screens inherit shared components.
- Submission workspaces separate original requirements/evidence entry from assignment
  context and roster. Mentor evidence leads into exact submitted requirements and
  decision, with previous decisions afterward on mobile. Shared `_SavedRequirements`
  and `_ReviewEvents` retain historical text, timing, receipts and audit events.
  Routes, fields, handlers, antiforgery, service authorization and schema unchanged.
- Focused presentation HTTP test: **1/1 passed**. Initial restricted visual launch
  failed during SQL fixture setup with an encryption-capability error; local-access
  retry passed **1/1**, covering **23 pages at 1440px and 390px**. Authenticated
  fixture HTTP responses/assets rendered through the existing GET snapshot bridge;
  account forms were blank anonymous forms. No Development identities used.
- Final affected regression: **31 passed / 0 failed / 0 skipped**,
  `TestResults/workspace-affected.trx`. Includes submission/team/review/evidence-entry,
  dashboards, lifecycle, archive/audit and presentation cases. Eight targeted visual
  rechecks cover final form/table/progress/review changes and populated course lists.
  No assertions weakened, existing tests deleted or full suite rerun.
- Visual evidence: `TestResults/workspace-visual/` screenshots and `checks.json`.
  Desktop/mobile submission and review screenshots, Candidate dashboard and Admin
  management/audit examples inspected. Automated checks cover page overflow, keyboard
  skip focus, drawer focus/Escape restoration and JavaScript errors; submission/review
  forms also require one primary action. This does not claim exhaustive visual review
  of every management subpage/state or full accessibility certification. Existing HTTP
  regressions cover real POST behavior; snapshot rendering itself performs no POSTs.
- Approved baseline recorded in `ui-design-framework.md`; user test-design reference
  extended in `completed-deliverables-test-checklist.md`. No migrations, Development
  data edits, unrelated cleanup, commits, pushes or deployment.
- Final solution build: **0 warnings / 0 errors**. Node syntax, duplicate form-class
  check and `git diff --check` passed. No model/schema edits or migrations.
  Development started with bootstrap passwords disabled; HTTPS login **200** verified
  through local-access curl. Site running at `https://localhost:7231` (session 54012).
  Restricted curl returned 000; that does not override the successful local check.
- User review: sign in with each current role, inspect its dashboard and navigation,
  then Candidate assignment/evidence/history, Mentor queue/review and Administrator
  account/course setup/archive/audit. Inspect mobile menu, long content and form errors.
  Await user review before another interval. Production prerequisites remain deferred.

### UI specification and previews — 12 November 2026 (latest checkpoint)

- Delivered `ui-design-framework.md`: official-reference strategy, actual role/route
  map, MVP page templates, shared tokens/components, desktop/mobile behavior,
  validation/Processing/approval/eligibility/stale/denial states, and three subsequent
  bounded implementation intervals requiring approval. D01–D16 remain the functional
  contract; no borrowed LMS features or deferred features added.
- Added standalone `docs/ui-previews/` Candidate assignment/submission and Mentor
  review HTML/CSS/JS plus index, four desktop/mobile PNGs and reproducible static
  export helper. Illustrative fixtures only; no sessions, API calls, actual uploads,
  evidence downloads or decisions. Preview-state toolbar is documentation tooling,
  not proposed business UI. Route links show existing targets rather than navigating live.
- Official TalentLMS, Canvas and Moodle guides inspected and linked in the framework.
  Retained observed CAPACITI navy #1D2951/coral #F25251. No integrations, external fonts
  or copied product assets. Requirement snapshots/timing, team eligibility/credit,
  administrator reopening and archive access policies are explicitly preserved.
- Static Chrome 157.0.8103.39 checks passed for both pages at 1440×1000 and 390×1000:
  no page overflow, labelled fields, one primary action, 3px focus, mobile menu
  focus/inert/Escape restoration, blocked/stale/denied state display and retained
  link values. Saved requirement text matches between both fixtures. Four PNGs
  visually inspected; mobile field alignment/order corrected before final export.
  Contrast pairs exceed 4.5:1; no runtime errors/external requests observed.
  Artifact: `docs/ui-previews/preview-checks.json`. This is not full accessibility
  certification or authenticated application acceptance.
- Only documentation/preview artifacts changed; no production Razor/CSS/JS,
  business logic, schema, databases, build, functional suites, installation,
  commits, pushes or deployment. Existing uncommitted application work preserved.
- **Stopped for design approval.** Review desktop/mobile hierarchy, section order,
  role grouping and state presentation. Do not implement Candidate, Mentor or Admin
  redesign until explicitly approved; the three future intervals are proposals.

### Local release handoff — 11 November 2026 (prior checkpoint)

- Added `local-release-handoff.md`: existing local prerequisites/configuration,
  safe run command, role navigation, already-applied migrations, data/storage safety,
  recorded acceptance results and operational/production limits. No credentials or
  new database/application changes. This is a documentation delivery, not deployment.
- Added `completed-deliverables-test-checklist.md`: D01–D16 trace completed functional
  deliverables to SRS IDs, explicit policy overrides, pages/services, existing
  coverage and testable expected behavior. Includes authorization, failure/no-write,
  idempotency, concurrency, file/retention boundaries and preservation checks.
- The unchecked test boxes are for the user's future execution. Proposed tests are
  not claimed newly run/passed. Superseded draft rules, deferred features and
  unverified nonfunctional targets are explicitly separated. No new scope audit.
- Documentation references and whitespace checked; no test suite, build, migration,
  database cleanup, software installation, UI change, commit, push or deployment.
- **Stopped for the user's test design/review.** Use Dxx-based case IDs and record
  preconditions, steps, expected/actual results and evidence. Select a separately
  bounded interval if new tests expose a defect; do not start deferred production
  or visual work automatically.

### Hosted evidence-worker acceptance — 11 November 2026 (prior checkpoint)

- **Passed: 1 / 1**, 0 failed/skipped, 30s:
  `TestResults/evidence-hosted-worker-acceptance.trx`. Only the new
  `Hosted_worker_polls_real_Defender_jobs_and_restart_recovers_expired_lease_without_duplicate_receipts`
  case in `CourseFileOperationalAcceptanceTests.cs` was run. No repeated passing
  suite or course/lifecycle/archive browser journey.
- Actual `EvidenceWorker`, fresh DI scopes/DbContexts and real Defender process
  durably queued PDF evidence with its unchanged 15-second polling schedule.
  The test first observes an idle poll, then queues the upload and awaits observed
  processing completion; no manual processor trigger, arbitrary sleep, fake scanner
  or application timeout change. Pending evidence remained unavailable.
- After stopping the first host, a second upload is queued and its disposable
  lease timestamp set in the past. A fresh host recovers and scans it automatically.
  This is **host restart with simulated expired-lease metadata**, not an actual
  killed-process/crash or system-clock test. Both files are Clean/scanned and their
  authorized downloads match exact original bytes. Identical accepted retry returns
  the first original receipt; exactly two uploads/evidence sets and attempt numbers
  1/2 remain. Assertions are scoped to this assignment, preserving shared fixtures.
- Only the operational test file and this checklist changed. Existing fixtures,
  private-storage guards, PDF sample and service/worker implementation reused.
  Actual durable work/scanning is observed through a delegating test wrapper, not
  replaced. New hosts use only the fixture's uniquely named temporary database and
  generated private temporary root; no Development jobs/accounts/data changes.
- Test-project build **0 warnings/errors**; `git diff --check` passed. No schema,
  migrations, full suite, SQL restart/configuration change, security change, unrelated
  cleanup, application feature, UI redesign, commit, push or deployment.
- Existing review site is available: final HTTPS login **200** at
  `https://localhost:7231`, session 96126 retained without restart.
- **Stopped for local acceptance/sign-off.** No further missing agreed functionality
  is confirmed in the existing checklist. Production provisioning, broader
  performance/accessibility/restore acceptance, hard-process-crash testing and
  deferred visual polish remain outside this completed interval. Intermittent host
  resource-pressure/SQL timeouts are not claimed fixed. A further interval needs
  explicit scope; do not invent features or rerun accepted journeys speculatively.
- Reproduce only when relevant:
  `$env:CPT_RUN_OPERATIONAL_ACCEPTANCE='1'; dotnet test tests/CandidateProgressTracker.IntegrationTests --no-build --filter FullyQualifiedName~Hosted_worker_polls_real_Defender_jobs_and_restart_recovers_expired_lease_without_duplicate_receipts --logger 'trx;LogFileName=evidence-hosted-worker-acceptance.trx' --results-directory TestResults`.

### Archive/audit acceptance completed — 11 November 2026 (prior checkpoint)

- **Passed: 1 / 1**, 0 failed/skipped, 1m5s:
  `TestResults/archive-browser-storage-final.trx`. Sanitized browser phases:
  `TestResults/archive-browser/acceptance.json` and `browser-output.txt`.
- Real Chrome/Razor/Identity/antiforgery journey: reasoned project archive retains
  approved outcomes; administrator history/receipt/approval and exact-byte protected
  file download remain available; current candidate/mentor history/review/download
  direct routes return 404 without leaking the receipt; scope reopening restores
  authorized reads/downloads but does not reopen approval; administrator finds the
  two new audit events with actor, action, record and date filters.
- Post-browser assertions passed: original assignment, revisions, submissions,
  reviews, link/file/upload metadata, original audit rows and rowversions unchanged;
  exactly two append-only scope events with their original approved receipt/outcome;
  no assignment-approval reopening event; same effective approval still blocks a
  fresh submission with no additional writes. Real Defender scanned setup evidence;
  only fixture-owned temporary SQL/private-storage records were used.
- Initial resume run `archive-browser-resume.trx` failed during test preparation:
  file processing committed but the injected review verifier had a different storage
  root. Corrected **test configuration only** to use the same private temporary
  root for setup and review verification. Retained the production evidence-availability
  guard and all preservation/access assertions; no application defect reproduced.
- After this relevant fixture correction, only the affected acceptance case was
  rerun and passed. Final test-project build **0 warnings/errors**; both browser
  scripts passed syntax; `git diff --check` passed. No full suite, migrations,
  Development account/data seeding, security/timeout changes or SQL restart.
- The prior local app was no longer listening. Started the existing app with
  bootstrap passwords disabled; verified startup and final login **HTTP 200** at
  `https://localhost:7231`, session 96126. No unrelated process was terminated or
  uncertain database cleaned. Resource-pressure recurrence remains a local risk.
- **Stopped for review.** Next bounded interval should be chosen explicitly:
  local acceptance/sign-off, or the recorded background-worker operational gap.
  Production provisioning and further visual polish remain deferred. No completed
  course/lifecycle/team acceptance or full suite rerun is needed without a change.

### Archive/audit acceptance prepared — 11 November 2026 (prior checkpoint)

- Final availability check: login endpoint did **not** respond (`curl` 000);
  do not claim the site is available. Independent `sqlcmd` also failed login with
  Shared Memory timeout 258. Available physical RAM was ~89 MiB of ~3.7 GiB.
  This supports recurring severe resource pressure; the sole root cause remains
  unproven. No service restart, timeout/security change or unrelated process kill.
- Added opt-in `ArchiveBrowserAcceptanceTests.cs` and
  `Browser/archive-audit-journey.cjs`; reused the accepted browser driver via a
  test-only journey dispatch in `Browser/course-files.cjs`. Course journey checks,
  lifecycle tests and application behavior are unchanged. Existing `ArchiveAuditTests`
  service/HTTP checks are reused as prior evidence, not rerun or duplicated.
- Intended browser coverage: current Administrator archives an approved project
  with a reason, sees saved outcomes/history and exact-byte private downloads;
  current Candidate/Mentor direct history/review/download routes are denied;
  reasoned scope reopening restores reads without reopening approval; actor/action/
  record/date audit filters find both events. New post-browser checks compare
  original assignment/revision/submission/review/link/file/upload/audit fields and
  rowversions unchanged, retain both outcome snapshots, and require approval still
  to block a fresh submission with no extra writes. **None ran successfully yet.**
- Test fixture prepares prior work through existing submission/review services and
  real Defender, in its own uniquely named database/private temporary storage.
  This avoids repeating the accepted course/lifecycle browser journeys. No test
  scanner or Development account/data seeding; no uncertain database cleanup.
- Build: initial new-test property typo `EvidenceId` corrected to
  `SubmissionEvidenceId`; final test-project build **0 warnings/errors**. Both Node
  scripts passed syntax checks. No C# application, domain, service or schema change.
- Actual run: `TestResults/archive-browser-acceptance.trx`, **0 passed / 1 failed**.
  Fixture failed before test-body execution, with SqlClient SSL pre-login handshake
  timeout / Win32 wait timeout. Stack includes `TestDatabase.CreateAsync` and
  `DisposeAsync` cleanup; original setup error may be masked by cleanup failure.
  No browser acceptance evidence or data-preservation assertions passed. Do not
  treat this as an application archive/audit defect or claim the new scenario passed.
- Launch approval review initially timed out **before any test started**; its one
  permitted launch retry ran the single failed case above. No repeated test attempt,
  raised timeout, security change, SQL restart, unrelated process termination,
  migration, full suite, new feature or UI work. Leave uncertain databases untouched.
- Exact continuation after connectivity/resource stability is restored: run only
  `$env:CPT_ARCHIVE_BROWSER='1'; dotnet test tests/CandidateProgressTracker.IntegrationTests --no-build --filter FullyQualifiedName~ArchiveBrowserAcceptanceTests --logger 'trx;LogFileName=archive-browser-acceptance.trx' --results-directory TestResults`.
  Diagnose its first failing stage without weakening preservation/access checks;
  do not repeat course/lifecycle/team acceptance or start production work.

### Course browser acceptance — 11 November 2026 (prior checkpoint)

- **Passed: 1 / 1**, 0 failed/skipped, 1m21s; exactly one affected run after the
  navigation-helper fix: `TestResults/course-browser-navigation-final.trx`.
  Evidence: `TestResults/course-browser/acceptance.json`, `progress.json` and
  sanitized `browser-output.txt`. No credentials, account DOM or HAR retained.
- Fixed only the course browser helper: readiness now requires a new main-document
  response and `DOMContentLoaded` correlated by loader, rather than a global page
  load event. Prior loader events cannot satisfy the next navigation; original
  45-second deadline, real forms/antiforgery, HTTP status and workflow checks remain.
  No confirmed application defect or behavior/formatting change.
- Real Chrome/Razor/Identity journey passed catalogue creation, cycle offering and
  individual course assignment; owner navigation and links-only rejection; actual
  PDF upload, Processing and real-Defender commitment/receipt; exact-byte attachment
  with no-store headers; unrelated Candidate history/download denial; scoped Mentor
  queue/download/approval; owner feedback and Approved history; selected-cycle Theory
  **1 approved / 1 required / 100%**, displayed `100,0`. This confirms the prior
  literal `100.0%` expectation was locale-sensitive. Numeric assertions stay exact
  and scoped to Theory, not any project percentage on the page.
- Durable processing uses an authenticated **test-only** trigger calling the actual
  processor/Defender after Processing is observed. Background worker scheduling was
  not rechecked. C# post-browser assertions passed: exactly one submission, review
  and committed upload. Isolated fixture database/private temporary storage only;
  no fabricated scanner, Development account/data changes or broad cleanup.
- Node syntax and `git diff --check` passed. No C# change/build, full suite,
  migrations, SQL configuration/security/timeout changes, process termination,
  service restart, UI redesign, commit, push or deployment. Existing build and
  separate real PDF/PNG/JPEG failure/stale-finalization results remain prior evidence.
- Site remains available at `https://localhost:7231`: final login **HTTP 200**,
  existing session 33790 left running. This is local acceptance, not production
  readiness or a guarantee that previous resource-pressure timeouts cannot recur.
- **Stopped.** Recommended next bounded interval: administrator archive/audit
  acceptance using disposable records and existing helpers. Do not rerun course,
  lifecycle, team or full-suite checks without a relevant change or failure.

### Progress assertion interval — 11 November 2026 (prior checkpoint)

- Changed only `Browser/course-files.cjs` and this checklist. Replaced the literal
  `100.0%` check with a check of the **Theory** summary specifically: approved=1,
  required=1, percentage=100, accepting a decimal comma or point. This retains
  exact numeric expectations and prevents a project total from satisfying the
  course check. Added explicit Approved review-heading assertion in candidate
  history. Application/service/formatting behavior unchanged.
- Added numeric-only `progress.json` diagnostics when that stage is reached;
  no account data, full DOM, credentials or evidence content is retained. The
  earlier locale hypothesis is **not yet verified from rendered progress**.
- Exactly one affected run: `TestResults/course-browser-progress-final.trx`,
  **0 passed / 1 failed**, 1m1s. Login GET and redirected `/Admin` returned HTTP 200;
  helper timed out waiting for `Page.loadEventFired` after login. This run did not
  reach catalogue/course setup or progress; no course/progress acceptance claimed.
  No server exception was captured. This alone does not establish a SQL or
  application defect. No second run or increased timeout.
- Node syntax and `git diff --check` passed. No C# change/build or full-suite run;
  prior build evidence remains historical. No migrations, Development changes,
  database cleanup, process termination, service restart or UI work.
- Local site remains available: final HTTPS login check returned **200** at
  `https://localhost:7231`; existing server session 33790 was not restarted.
- Exact continuation for a separately authorised interval: diagnose the browser
  helper's navigation completion using safe event/readiness diagnostics, retaining
  HTTP/authentication and numeric workflow checks. Then capture actual Theory
  progress and rerun only affected acceptance. Do not repeat SQL discovery unless
  a fresh connection failure supports it; do not begin archive/audit work.

### SQL connectivity interval — 11 November 2026

**Latest user-requested retry (supersedes availability/blocker notes below):**

- Two separate direct Development queries succeeded. Available RAM improved to
  ~439 MiB; SQL's low-physical-memory flag remains set. No restart, process
  termination, database-option/security change or increased timeout was used.
- Application startup succeeded with bootstrap passwords disabled; existing-role
  reads completed in 5–197 ms. HTTPS login returned **200** before the test and
  again afterward. Site left running at `https://localhost:7231` (session 33790).
  Resource pressure remains a plausible contributor to earlier intermittent
  timeouts, not a proven sole cause or a guarantee against recurrence.
- Exactly **one** existing course browser test run: **0 passed / 1 failed**, 1m28s.
  Artifact: `TestResults/course-browser-connectivity-recheck.trx`; sanitized stages
  in `TestResults/course-browser/browser-output.txt`. No repeated attempt.
- Passed browser stages: catalogue/offering/individual assignment; candidate course
  navigation and links-only rejection; actual PDF Processing then real-Defender
  commitment/receipt; history, attachment/cache headers, exact-byte download and
  other-candidate history/download denial. Scoped mentor queue/review/download and
  candidate feedback checks also completed before the final assertion failed.
- Exact failure: `Browser/course-files.cjs:55` expects literal
  `1 / 1 approved (100.0%)` on Candidate Progress (HTTP 200). Shared progress Razor
  uses culture-sensitive `ToString("0.0")`; local `en-ZA` formats 100 as `100,0`.
  This supports a locale-sensitive test expectation as the likely cause; the
  failed run did not retain rendered progress totals, so actual totals are not
  independently proven. Course acceptance remains incomplete. Do not claim a
  confirmed application progress defect or change formatting/assertions here.
- Only this checklist changed during the retry. Next bounded interval: inspect
  actual rendered theory totals and correct the demonstrated expectation or
  application defect, retaining meaningful numeric approval/progress assertions;
  rerun only the affected acceptance test when authorised. No archive/audit or UI
  expansion, full suite, migrations or database cleanup.

Earlier diagnostic evidence (retained):

- Service `MSSQL$SQLEXPRESS` is Running/Automatic. Three separate default-timeout
  `sqlcmd` connections succeeded; a fourth direct Development `SELECT 1` succeeded
  and confirmed ONLINE. This does **not** establish stable application connectivity.
- Host evidence: ~3.7 GiB total RAM, ~268–293 MiB available, CPU sampled at 100%,
  paging ~6,346 pages/sec. SQL reports `process_physical_memory_low=1` and only
  ~25 MiB resident. Resource pressure is a supported contributor, not a proven
  exclusive root cause. No unrelated process was stopped.
- SQL/Windows logs inspected read-only. Earlier 18456 state-38 errors name temporary
  test databases, not Development authentication failure. Development has
  `AUTO_CLOSE=ON`; logs show repeated database startups and recovery. This can add
  cold-start overhead; no database option was changed.
- Startup attempted once, bootstrap passwords disabled: SqlClient post-login
  timeout -2 / Win32 258 at `RoleManager.RoleExistsAsync`, `Program.cs:165`.
  Login endpoint returned no HTTP response (`curl` 000); site is **not available**.
- Course browser acceptance **not run in this interval** because startup failed.
  No service restart, security/timeout change, database cleanup, migrations, full
  suite, new features or UI work. Existing application changes preserved.
- Await user closing unused heavy applications; no permission to terminate their
  processes or restart SQL has been given. Then recheck resource pressure and
  direct Development connectivity; verify startup. Only after both succeed, run
  the existing course browser test **once** using the command below. If it fails,
  record its stage and stop repeated attempts. No broad discovery is needed.

### Course/file acceptance checkpoint — 11 November 2026

- **Blocked; not accepted.** Added opt-in `CourseBrowserAcceptanceTests.cs` and
  `Browser/course-files.cjs`, reusing the lifecycle test's secret-safe logger and
  existing fixture, seed, real Defender service and certificate sample. Intended
  journey: administrator catalogue/offering/assignment forms; owner links-only
  rejection and PDF upload; Processing then committed receipt; exact-byte private
  download and another candidate's denial; scoped mentor queue/approval; candidate
  feedback and theory progress. These journey assertions have **not passed**.
- Four focused attempts, each one failed case: two timed out during login POST
  (one captured HTTP 500), one failed fixture account seeding with SQL execution
  timeout, and the final diagnostic attempt timed out before the first login page
  loaded. Read-only `sqlcmd` independently failed SQL Express login with Shared
  Memory timeout 258. No application defect has been isolated; no app behavior,
  assertions, timeouts, schema or Development records changed to force success.
- Final run also reported SQL test-class cleanup failure. Its remaining database
  identity is not established here: leave uncertain databases untouched, never
  delete by prefix. Do not repeat the older abandoned-database cleanup exercise.
- Artifacts: `TestResults/course-browser-acceptance.trx`,
  `TestResults/course-browser-diagnostic.trx`, and sanitized
  `TestResults/course-browser/browser-output.txt`. No credentials/DOM/HAR/screenshots
  retained. Earlier passing real Defender PDF/PNG/JPEG, scanner failure/recovery and
  stale-finalization results remain prior evidence, not new browser acceptance.
- Test-project build passed with **zero warnings/errors**; Node syntax and
  `git diff --check` passed. No full-suite run or migration in this interval.
- Local startup was attempted with bootstrap account passwords disabled using
  the existing run command below. It failed at `RoleManager.RoleExistsAsync`
  (`Program.cs:165`), reading Development: SQL connection timeout -2 / Win32 258.
  **Site startup/browser availability is not verified.** No migration or database
  update was attempted. Do not claim the site is currently running.
- **Exact continuation:** restore/confirm local SQL Express responsiveness without
  resetting data or changing security settings; rerun only:
  `$env:CPT_COURSE_BROWSER='1'; dotnet test tests/CandidateProgressTracker.IntegrationTests --no-build --filter FullyQualifiedName~CourseBrowserAcceptanceTests --logger 'trx;LogFileName=course-browser-acceptance.trx' --results-directory TestResults`.
  Preserve this unfinished test. Do not start archive/audit or UI work.
- Consolidated manual fallback, using disposable accounts and an unarchived cycle:
  administrator **Set up learning → cohort → Learning cycles → Courses**: create
  catalogue entry, offer and assign to a current Candidate/cohort member. A current
  Mentor must belong to that cohort; use a separate out-of-scope Candidate too.
  Owner **Courses & certificates → Requirements and certificate submission**:
  reject links-only, then upload an actual PDF/PNG/JPEG; refresh Processing until
  committed receipt. **Submission history and feedback**: download attachment;
  out-of-scope account must be denied the copied history/download routes. Mentor
  **Review submissions → Review latest attempt**: download and approve with feedback.
  Owner sees feedback and approval under history and **Progress details → Theory**.
  Browser evidence remains outstanding until performed; do not repeat team or
  lifecycle acceptance.

### Functional-release verification (prior checkpoint)

- Full run: `functional-release-full.trx`, run **once**: **278 passed, 17 failed,
  3 skipped / 298 total** (36m42s). Thirteen failed cases were SQL setup/seed/read/
  cleanup infrastructure errors; three expected old pre-lifecycle HTTP denial
  behaviour, and one review retry returned no receipt under load. Do not label the
  initial full run green. The three skipped cases are explicitly opt-in real
  Defender operational checks; previous 3/3 evidence is retained, not rerun here.
- Pending-model check passed with a test-only design-time connection; no migration
  applied and no database created by the check. Initial invocation lacked the Testing
  connection setting and was corrected; this was a command setup issue.
- Solution build after all scoped test corrections: **zero warnings/errors**.
  Final `git diff --check` passed. Startup/login HTTP **200** verified; site running
  at `https://localhost:7231` (session 98436), bootstrap account creation disabled.
  Restart exactly with:
  `dotnet run --project src/CandidateProgressTracker.Web --no-build --launch-profile https -- --DevelopmentAdmin:Password= --DevelopmentCandidate:Password=`.
- Corrected four old HTTP expectations to require Login/session invalidation after
  role removal, as approved lifecycle policy specifies. Independent service denial,
  data isolation and no-write assertions are retained.
- SQL setup/cleanup timeouts occurred under the full run. Test collections will run
  serially on retry; coordinated concurrent requests inside tests remain unchanged.
  Test-database disposal is now idempotent and still propagates its first SQL error.
  Review receipt retry now asserts success with diagnostic errors before retaining
  the original exact-receipt assertion. No application behaviour/assertion relaxed.
- Failed cases map to 14 methods. Their affected-only rerun (including theory
  variants): **49 passed, 1 failed / 50 total** in 3m23s,
  `functional-release-affected.trx`; no second full run. SQL failures and the review
  receipt-retry case passed without increasing timeouts or relaxing assertions.
- The remaining retry failure revealed another old role-removal POST expectation
  previously hidden by fixture failure. Corrected it to require Login while retaining
  independent service denial of new/accepted requests and exact no-write counts.
  Only this method's two revocation variants were rerun: **2/2 passed** in
  `functional-release-revocation-final.trx`. All 17 initial failed cases now have
  passing affected coverage. Across the original run and targeted retries, all
  **295 non-opt-in cases** passed; this is not a claim of a second clean full run.
- Changed areas this interval: `TestExecution.cs` collection scheduling;
  `TestDatabase.cs` repeated-disposal guard; four HTTP test expectations in
  AdministratorServiceAuthorizationTests, SubmissionAuthorizationTests,
  SubmissionHistoryTests and MentorSubmissionHistoryTests; added diagnostic success
  assertion in ReviewWorkflowConcurrencyTests; this checklist. Application/domain/
  schema/UI behaviour unchanged. No assertions removed or timeouts increased.
- No browser checks were performed in the functional-release interval; the later
  lifecycle browser acceptance is recorded below. No confirmed missing agreed
  functionality identified from the existing checklist; local acceptance still
  requires the remaining manual journeys above. Production readiness is deferred.
- Do not clean uncertain temporary databases. Failed cleanup may have left test
  databases; none were manually removed in this interval. Development data,
  migrations and existing work preserved.
- Prior recommendation to perform lifecycle acceptance is completed below. Future
  course/private-file and archive/audit acceptance require separately bounded user
  intervals; no speculative implementation or visual polish before acceptance.

## Account-lifecycle acceptance — 7 November 2026

- **Passed:** one opt-in live Chrome acceptance case covering all six requested
  journeys, with actual HTTPS Kestrel/Razor/Identity requests and antiforgery.
  Separate private browser contexts test real session-cookie revocation; no
  injected authentication or static HTML snapshots are used for this acceptance.
- Activation: pending account cannot log in; expired/superseded credentials fail;
  valid activation works once. Recovery: expired credential fails; valid recovery
  works once, invalidates sessions at issuance and redemption, and replaces the old
  password. Deactivation denies login and existing sessions. Reactivation rejects
  blank reason even with browser constraint checks bypassed, requires a fresh
  activation and retains the ended membership. Role correction invalidates the old
  Candidate session. Last active Administrator cannot deactivate or remove its
  own Administrator role and retains authenticated administration access.
- Expiry is simulated by adjusting only disposable credentials' timestamps in the
  unique test database, not by waiting 24 hours or changing system/application time.
  Membership is created for the disposable account and ended through the existing
  authorised service. Final DB assertions confirm unchanged ended-member rowversion,
  end audit fields, no replacement membership, retained account-change records,
  no test passwords in audit details, and the active sole Administrator role.
- Actual result: `tests/CandidateProgressTracker.IntegrationTests/TestResults/lifecycle-browser-acceptance.trx`
  **1 passed / 0 failed / 0 skipped**, browser case 4m58s. Sanitised phase evidence:
  `TestResults/lifecycle-browser/acceptance.json` and `browser-output.txt`.
  Earlier automation attempts timed out or saw an unexpected response; their exact
  cause was not fully isolated. The final direct-host run retained strict HTTP,
  denial, credential-clearing and workflow assertions; no app assertion relaxed.
- No application defect reproduced or application behaviour changed. Only new
  `AccountLifecycleBrowserAcceptanceTests.cs`, `Browser/account-lifecycle.cjs` and
  this checklist were changed. Existing five lifecycle service/HTTP/concurrency
  cases are reused from the passing functional checkpoint, not claimed as freshly
  rerun. No full suite, migrations, Development-account changes or installations.
- Credentials generated at runtime stay out of reports/artifacts; no credential
  screenshots, DOM dumps or HAR are captured. Chrome's certificate exception is
  confined to the disposable acceptance process; no system security setting changed.
- Reproduce only when needed:
  `$env:CPT_LIFECYCLE_BROWSER='1'; dotnet test tests/CandidateProgressTracker.IntegrationTests --no-build --no-restore --filter FullyQualifiedName~AccountLifecycleBrowserAcceptanceTests`
  Uses the existing installed Chrome and fixture-owned temporary SQL database.
- No lifecycle interaction required from the user for this isolated acceptance.
  Development-account/production acceptance is not claimed. Final solution build:
  **zero warnings/errors**; `git diff --check` clean. The previous local server was
  no longer listening; restarted with bootstrap passwords disabled, startup/login
  **HTTP 200** verified at `https://localhost:7231` (session 59050).
  **Stopped here.** Await the user's next bounded interval; no other feature,
  production work, UI redesign or full-suite run started.

## Prior interval records (historical)

The dated evidence below is retained. Earlier "outstanding"/"deferred full suite"
and continuation notes do not override the current release status above.

| Status | Requirements / scope | Evidence / remaining acceptance |
| --- | --- | --- |
| Completed baseline | FR-01,05?10,12?17,21/22/24: scoped setup, individual/team submissions, revisions, credit, review and administrator approval reopening | Earlier 258-test checkpoint and user-confirmed team/Bob browser checks. Those results predate this interval. |
| Implemented; backend operational checks passed | FR-11/14/18; NFR-03/04: courses and private file evidence | Real Defender **3/3 passed**: actual PDF/PNG/JPEG, protected HTTP downloads, scanner cancellation/recovery and stale finalization. Browser acceptance outstanding. Prior affected course/file regression documented in [handoff](course-file-evidence-handoff.md). |
| Implemented; focused checks passed | FR-20: mentor review queue | Latest committed attempts, scoped cohort/cycle/project/course/status filters, oldest receipt first, 20/page; self-review/unavailable evidence blocks decisions. |
| Implemented; focused checks passed | FR-25/26; BR-02?04/10: candidate/mentor learning overviews | Four focused overview/queue cases passed across corrected runs. Separate theory/project totals, pending/changes/overdue, cycle selection. Frozen team work counts only for credited members retaining current Candidate/cohort/team access; current later joiners read without totals. |
| Implemented; focused checks passed | FR-02?04; NFR-02: administrator-mediated lifecycle | Activation 24h, recovery 1h, single use; issue/use invalidates prior credentials/sessions. Existing accounts stay active. Reasoned reactivation requires fresh activation; memberships are not restored. Role/profile correction and last-active-admin protection. |
| Implemented; focused archive/download checks passed | FR-27/28; BR-14: administrator archives/history | Reasoned, versioned, idempotent project/cycle archive/reopen; immutable outcome snapshots. Administrator-only archived history/downloads. Candidate/mentor archives remain inaccessible. Scope reopening never reopens an approval. |
| Implemented; focused checks passed | FR-29/30: transactional audit/search | Append-only business events; administrator actor/action/record/date search, 20/page. Existing typed history read without fabricated backfill. Audit rollback and append-only checks passed after query-translation fix. |
| Outstanding acceptance | SC-07/08; NFR-05?12 | Browser checklist below; broader regression/full integration deferred to release checkpoint. Accessibility, performance and production provisioning/support/retention remain unverified. Local acceptance is not production readiness. |
| Superseded draft rules | FR-23; BR-13 and older Identity design | Administrator approval reopening only; current team membership required for candidate reads; frozen credit alone grants no access. Keep Identity string keys. |
| Deferred / policy not agreed | Cancellation | Unavailable; no cancellation transitions or denominator exception implemented. |
| Explicitly deferred | FUT-01/02/03; DOCX; roster correction/supersession; general UI redesign | Scorecards, reminders, advanced reporting, DOCX and roster resets/corrections excluded. No next feature without `continue`. |

## Verification and continuation

- Real operational acceptance: `TestResults/operational-acceptance/real-defender-acceptance.trx`, **3 passed**.
- Overview/queue: **4 distinct passed** (three in corrected run, one archive-fixture rerun).
- Audit + populated upgrade: `TestResults/mvp/audit-upgrade-focused.trx`, **2/2 passed**.
- Lifecycle: **5 distinct cases passed** after per-case account isolation. Initial
  shared-fixture role mutation caused two setup failures; assertions were retained.
  Isolated run passed three and hit two SQL timeouts while backup comparison ran.
  Only those two were rerun without competing SQL work, **2/2 passed**, unchanged
  test timeouts/assertions (`lifecycle-timeout-recheck.trx`). Coordinated redemption,
  role/session revocation, activation/recovery/deactivation and current-access checks
  are covered. Expanded archive snapshot/download checks also passed.
- Model check: no pending changes. Diff check passed before latest helper edits;
  final build **0 warnings/errors** and `git diff --check` passed after the test host
  exited. An overlapping build previously hit the running test DLL; no source
  compilation defect. Startup verified: Development listens on HTTPS 7231 / HTTP 5296; anonymous login
  returned HTTP 200 with the login form and credential navigation. App is left running
  for review (tool session 13754). Bootstrap account creation was disabled by command-line
  overrides for this startup; no login or browser interaction was performed. Full integration deliberately not run.
- Exact continuation: **wait for browser review and explicit `continue`**. No next
  feature started; full integration remains deferred. App is available at
  `https://localhost:7231`; restart using the command below if the process has stopped.
- Lock order: affected cohort gates (sorted), then lifecycle gate, then user/role/state
  rows. Existing workflows retain cohort-first ordering and Serializable access checks.

## Migration / backup

Applied with explicit user approval:
`20261107083104_AddAccountLifecycleAuditAndArchives`.
Immediate predecessor: `20261107065418_AddTheoryCoursesAndPrivateEvidence`.

[Reviewed SQL](migrations/AddAccountLifecycleAuditAndArchives.sql) adds five tables:
AccountAccessStates, AccountCredentials, AccountChanges, AuditEvents, ScopeArchiveEvents.
Only existing-table change: AspNetUsers.EmailIndex becomes unique for non-null
normalized emails. Duplicate preflight aborts without rewriting records. No backfill,
existing-column changes or Identity key changes. Foreign keys restrict deletion;
new audit/change/archive events have append-only triggers. Unsafe downgrade is blocked.
Populated predecessor upgrade preserved seeded records and rowversions; no fabricated
account state or audit events. Existing accounts without state remain active.

Fresh target confirmed: `Lutho\SQLEXPRESS` / `CandidateProgressTracker_Dev`.
COPY_ONLY/CHECKSUM backup + VERIFYONLY + actual unique restore/CHECKDB passed:
`C:\Program Files\Microsoft SQL Server\MSSQL17.SQLEXPRESS\MSSQL\Backup\CandidateProgressTracker_Dev_pre_lifecycle_20261107_e9a23fbb67df49328de1cb0147c38e41.bak`.
Owned restore database: `CandidateProgressTracker_Tests_e9a23fbb67df49328de1cb0147c38e41`.
Post-upgrade exact bidirectional comparisons passed for **all 26 original tables**,
including every original column/row/rowversion. The recorded idle restore database
was removed only after success; backup retained. An earlier concurrent comparison
timed out; the subsequent serial comparison passed. A redundant retry found the
already-removed restore database and made no Development data changes.
Do not touch uncertain temporary databases. Earlier course/file migration was also
applied with verified backup/restore and complete original-data preservation.

Exact command used (already applied; do not reapply):

```powershell
dotnet ef database update 20261107083104_AddAccountLifecycleAuditAndArchives --project src/CandidateProgressTracker.Infrastructure --startup-project src/CandidateProgressTracker.Web --no-build --connection 'Server=.\SQLEXPRESS;Database=CandidateProgressTracker_Dev;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True' -- --environment Testing --ConnectionStrings:DefaultConnection 'Server=.\SQLEXPRESS;Database=CandidateProgressTracker_Dev;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True'
```

## Configuration and run

Existing Development user secrets supply the local SQL connection and
`Evidence:Root=C:\Users\Lutho Palaza\AppData\Local\CandidateProgressTracker\Evidence`.
Storage is private, outside repository/wwwroot, with restricted directory ACL.
Real Windows Defender is registered; no software installation/security-setting change.
Default authentication/credential limit: 30 requests/minute/IP, configurable with
`Authentication:RequestsPerMinute`. Automated shared-host tests explicitly raise that
limit; production code does not bypass scanning. No SMTP/public registration needed.

```powershell
dotnet run --project src/CandidateProgressTracker.Web --no-build --launch-profile https
```

Open `https://localhost:7231`. No browser interaction has been claimed as performed.
No commits, pushes or deployment. Further Development migrations need fresh approval.

## Consolidated browser review (outstanding)

Use an existing Administrator; current Candidate/course owner and assigned Mentor
with current cohort memberships; an out-of-scope account. Use separate mentor/candidate
accounts to avoid intentional self-review denial. Existing accounts remain active.

1. Administrator -> User accounts -> Create: unique email, issue activation; privately
   deliver credential. Account holder -> Activate account / recover password: set own
   password once. Issue recovery; earlier session must fail on its next protected request.
   Deactivate/reactivate with reason/fresh activation; memberships remain unchanged.
2. Administrator -> Cohorts -> Learning cycles -> Courses: offer/assign a course.
   Candidate -> My courses: actual PDF/PNG/JPEG, Processing -> committed receipt,
   exact submitted requirements/deadline and attachment downloads. Links alone fail.
3. Mentor -> Review queue: scoped filters/oldest pending work; review latest attempt.
   Candidate history shows feedback. Approval blocks new work; Administrator reasoned
   reopening permits eligible actions. Course files remain private to owner/scoped staff.
4. Candidate / Mentor -> Learning overview: selected-cycle theory/project totals,
   pending work, upcoming/overdue actions; current later team joiners have no frozen
   credit in totals. Historical roster remains unchanged.
5. Administrator -> Archive and historical records: archive with reason, inspect
   saved outcomes/history/downloads; candidate/mentor direct routes remain denied.
   Reopen with reason; approval remains effective unless separately reopened.
   Administration -> Search audit history: actor/action/record/date filters.

Scanner failure/recovery and stale finalization were operationally tested with the real
adapter in isolated databases. Do not disable Defender/settings for manual checks.
After browser review, wait for the user's explicit `continue` to choose the next interval.

Final interval result: overview/queue **4 distinct passed**; lifecycle **5 distinct
passed** across isolated run + two unchanged timeout retries; audit/upgrade **2/2**;
archive snapshot and protected archived download checks **2 passed**; earlier actual
Defender operational checks **3/3**. Build **0 warnings/errors**, model unchanged,
`git diff --check` clean. Both approved Development migrations applied; latest
migration preserved all 26 original tables/rowversions after verified backup/restore.
Browser review, broad regression, full integration and production readiness remain
unverified/deferred as described above. Stop here; do not add features before `continue`.


## UI interval checkpoint ? CAPACITI reference (7 November 2026)

- Applied only presentation changes: shared CSS, layout/scoped overrides and removal
  of template home-page boilerplate. Existing service/data/authorization work preserved.
- Verified live source: https://capaciti.org.za/wp-content/uploads/elementor/css/post-9.css
  (linked by the home page as `elementor-post-9-css`, version `1793216071`).
  `.elementor-kit-9` defines primary `#1D2951`, secondary/accent `#F25251`.
  These are observed live website tokens, not an assertion of a published brand guide.
  Shared `--brand-primary` / `--brand-accent` in `wwwroot/css/site.css` replace the
  generic navy/teal proposal. No external font/logo dependency introduced.
- White cards/light-grey page, dark neutral text, restrained borders/shadows.
  Primary drives actions/links/active navigation; coral is a small decorative mark.
  White/coral is only 3.44:1, so coral is not used for text or button backgrounds.
  Success/warning/error retain separate semantic colours and readable text.
- Contrast: primary/white 14.11:1; main text/page 13.91:1; muted text/page 5.63:1;
  control border/white 4.16:1; focus/page 13.05:1; alert text/background 7.08?7.21:1.
  Calculation artifact: `TestResults/ui-brand/contrast.json`.
- Actual isolated headless Chrome checks passed on Home/Login: primary computed
  button/link/active navigation colours; visible 3px keyboard focus on skip link and
  username; 390px viewport without horizontal overflow. Desktop/mobile screenshots
  visually inspected. Artifacts: `TestResults/ui-brand/` (browser-checks.json and PNGs).
- Web build: 0 warnings/errors after restarting the owned prior server to release
  its executable lock. No integration suite rerun for this CSS/layout change.
- Runnable review: https://localhost:7231 (server tool session 1255; bootstrap account
  creation disabled). Headless verification browser closed; app left running.
- Browser review still needed for authenticated candidate/mentor/admin pages, long
  evidence/history/table content and real keyboard journeys. Inspect a form, cards,
  history/status messages and mobile menu in each role. No broader accessibility or
  functional acceptance claim. Full integration remains deferred.
- **Stop at visual review. Await explicit `continue`; no next feature or scope expansion.**


## Shared shell + Candidate dashboard interval (7 November 2026)

- Implemented: persistent grouped role navigation on desktop, compact account bar
  with antiforgery logout, breadcrumbs, shared content widths, and an offcanvas mobile
  menu. CAPACITI tokens retained. Existing routes/permissions/workflows unchanged.
  Mentor/Administrator content only inherits the shell; dashboard redesign deferred
  until this layout is approved.
- Candidate `/Candidate`: selected-cycle theory/project totals; one prioritised action;
  changes-requested work before overdue/upcoming submissions; recent written feedback;
  compact assignment/history table and cohort/cycle access. Pending reviews are never
  overdue candidate actions. Current joiners may read team feedback but cannot act or
  count without frozen credit; credited leavers remain denied. No invented metrics.
- Data: existing `ILearningOverviewService` extended with at most three feedback items
  from already authorised assignments; individual feedback also requires the actual
  submission owner. Encoded Razor output; links use existing protected history routes.
  No schema changes or Development data seeding.
- Focused affected regression: **6 distinct tests passed** (five in affected run,
  one corrected fixture rerun). The fixture correction seeds an elapsed original
  deadline; it does not bypass the service's prohibition on backdating new deadlines.
  Initial test-browser failures were harness issues: missing scoped-CSS provider and
  fingerprinted asset URLs. Those assertions were retained and harness fixed.
- Authenticated visual verification: real Candidate HTTP HTML/assets from a uniquely
  named temporary database, rendered through a test-only GET snapshot bridge. Chrome
  desktop 1440px and **exact 390px** mobile passed: fixed 248px sidebar, one primary
  action, authorised feedback/actions, no page overflow, visible 3px skip focus,
  mobile focus trap and Escape focus restoration; no runtime errors. Screenshots
  inspected. A real overflow from screen-reader labels was fixed by making the
  scrolling table container positioned; content is not hidden to mask overflow.
- Artifacts: `TestResults/dashboard-visual/checks.json`, candidate desktop/mobile/menu
  PNGs, `candidate-dashboard-affected-regression.trx` (5 pass/1 fixture failure),
  `candidate-dashboard-final-http.trx` (corrected case 1/1). Temporary auth snapshot
  contains fictional fixture records only; no Development cookies/accounts used.
- Final Web build: **0 warnings/errors**; `git diff --check` clean. Real local startup
  verified with HTTP 200 login/shell response. Site left running at
  `https://localhost:7231` (tool session 64018), bootstrap account creation disabled.
  Full integration remains deferred. No migrations, commits, pushes or deployment.
- Exact next action: **stopped for Candidate layout review** at
  `https://localhost:7231/Candidate`. Use a current Candidate/cohort account: Overview ->
  select cycle -> inspect work priorities, feedback, courses/projects/history; narrow
  viewport -> open/close menu, Tab/Escape and horizontally scroll the history table.
  Confirm Mentor/Admin get the shell with their existing content. Wait for explicit
  layout approval and `continue` before their dashboard interval.


## Evidence-entry usability interval ? 7 November 2026

- Candidate dashboard layout approved. Scope is individual/team evidence entry only;
  no Mentor/Administrator dashboard redesign in this interval.
- Completed: both submission forms use shared `_EvidenceLinks.cshtml` and
  `wwwroot/js/evidence-links.js`: one initial row, add up to ten, remove/reindex
  without losing remaining values, accessible button names/live status and focus.
  Failed POSTs retain rows, values and validation messages. Blank optional rows are
  filtered only from the submission payload; partial rows still fail validation.
  Project files/links/both and actual course certificate-file requirements remain.
- New focused HTTP verification: **2/2 passed**, individual/team partial validation,
  field errors/value preservation, ignored blanks, successful save and no partial
  roster on failure. Chrome on actual HTTP-rendered editors passed both forms:
  add/remove/value preservation, contiguous binding, label/ID associations, max ten,
  focus and real Enter-key activation. The initial keyboard simulation omitted the
  Enter text event; the harness was corrected without changing application logic.
- Build: solution **0 warnings/errors**. JavaScript syntax and `git diff --check`
  passed. Local login startup **HTTP 200**. Full suite remains deferred.
- Affected regression: **23/23 passed**, existing individual/team submission-page
  and file-evidence cases, run once. Result: `evidence-entry-affected.trx`.
- Site: `https://localhost:7231` (session 20302), bootstrap passwords disabled.
  Browser recheck: Candidate -> Projects -> individual Submit evidence / team
  Submit evidence; add several links, remove the middle row, inspect retained
  values, submit a partial row and correct it. Course submit must still require
  a certificate file. User visual review remains outstanding.
- No schema, migrations, Development seeding, commits, pushes or deployment.
  Exact continuation: stop for evidence-entry review; wait for the user's next
  approved interval rather than starting another dashboard or feature.


## Mentor / Administrator landing dashboards ? 7 November 2026

- Approved: evidence-editor browser review passed. Retain approved CAPACITI colours,
  shared shell and Candidate dashboard. This interval changes only staff landing
  pages and their dashboard-specific styling/test support.
- Implemented: `/Mentor` reuses authorised learning overview/queue services, cycle
  selection, oldest eligible review action and five-item review list; separate
  changes-requested history links; required theory/project approval progress for
  current scoped candidates, with full detail/queue navigation. Reads further queue
  pages when unavailable/self-review rows would otherwise hide eligible work.
- Implemented: `/Admin` groups People, Learning and Governance; account/cohort
  management, scoped cohort selector with real cycle-specific course/project links,
  archives/audit access and five existing recent audit records. No invented metrics,
  backend service features, schema changes or deeper management-page redesign.
- Verified: **2/2 focused authenticated HTTP tests passed**, including current
  mentor scope/revocation, separated candidate changes, encoded text and correct
  management routes. Real HTTP HTML/assets from unique temporary test databases
  passed Chrome at **1440px desktop and exact 390px mobile** for both roles:
  one primary action, 248px desktop sidebar, no page overflow, visible 3px keyboard
  focus, mobile menu focus trap/Escape restoration and no runtime errors. Screenshots
  inspected in `TestResults/staff-mentor` and `TestResults/staff-admin`. The mentor
  progress table scrolls within its container on mobile. No Development fixture
  accounts or data were added. Solution build **0 warnings/errors**; diff check clean.
- Affected regression: **6/6 existing Candidate dashboard and overview/queue cases
  passed**, run once (`staff-dashboard-affected.trx`). New focused result:
  `staff-dashboard-focused.trx` **2/2 passed**. Full suite remains deferred.
- Local startup verified: login **HTTP 200**, site left running at
  `https://localhost:7231` (session 21341), bootstrap account creation disabled.
  No migrations or Development data seeding. No commits, pushes or deployment.
- Browser review: Mentor with current cohort membership -> Overview -> select cycle
  -> review oldest work / compare candidate changes / inspect progress. Administrator
  -> Overview -> People, select cohort under Learning, open Courses/Projects, then
  inspect Governance history/activity. Repeat narrow viewport and keyboard menu.
- Exact continuation: **stopped for Mentor/Administrator dashboard visual review**.
  User acceptance is outstanding. Wait for approval and the next explicit interval;
  do not begin deeper pages or another feature.
