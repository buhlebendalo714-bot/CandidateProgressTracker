# Completed deliverables: test-design checklist

Updated 17 November 2026 (SAST). A test-authoring reference, **not a second delivery-status
tracker**. Current status and actual results remain in the
[delivery checklist](delivery-checklist.md); setup is in the
[local release handoff](local-release-handoff.md).

Source baseline: [original proposal](requirements/Project_Proposal_Candidate_Progress_Tracker.pdf), [Scope v0.2](requirements/Candidate_Progress_Tracker_01_Scope_and_MVP%20%281%29.md)
and [SRS v0.1](requirements/Candidate_Progress_Tracker_02_SRS.md). Later explicit
conversation decisions override draft requirements. `D01`–`D16` below are local
deliverable/test-group identifiers, not new SRS requirement IDs. All describe
implemented behavior; unchecked boxes are space for **your new test execution**,
not an assertion that implementation or prior tests are missing. Existing evidence
does not prove every possible edge case or full original-scope completeness.
Coverage references below are in the [existing integration tests](../tests/CandidateProgressTracker.IntegrationTests/);
named contracts are in [Application](../src/CandidateProgressTracker.Application/).

## Prepare test data and record results

Use only a uniquely named temporary database/private storage owned by the test.
Reuse existing integration helpers; do not alter Development to simulate failure.
Prepare an Administrator; Candidates Alice/Bob/Charlie; a scoped Mentor; another
cohort/candidate/mentor; and a dual-role account for self-review checks. Include
current, ended and future memberships. Never record credentials or private paths.

For each derived case record: **case ID (e.g. D07-01), source/override, role,
preconditions, steps, exact expected state/no-write assertions, actual result,
evidence path, date and defect reference**. Use Passed/Failed/Blocked/Not run;
a failed fixture is not an application assertion result. Keep browser and service
tests separate where they exercise different authorization boundaries.

## D01 — Authentication and role boundaries

Source: FR-01/04; NFR-01/02; agreed current-role/service authorization rules.
Where: `/Account/Login`, role landing pages, protected services/downloads.
Evidence: `WebAccessTests`, authorization tests and lifecycle browser acceptance.

- [ ] Correct active credentials reach the permitted workspace; wrong credentials
  and unauthenticated requests do not reveal protected records. Logout prevents
  subsequent protected access; mutating forms require antiforgery.
- [ ] A role/cohort/team mismatch is denied both through direct routes and service
  calls. Removing a role after opening a form revokes stale access; services recheck
  current authorization rather than trusting the rendered page or cookie alone.

## D02 — Administrator-mediated account lifecycle

Source: FR-02–04; approved lifecycle rules replacing unspecified draft delivery.
Where: **People & access**, `/Admin/Users/Create`, `/Admin/Users/Manage`,
`/Account/CompleteCredential`; `IAccountCreationService`, `IAccountLifecycleService`.
Evidence: lifecycle service/HTTP tests and `AccountLifecycleBrowserAcceptanceTests`.

- [ ] Administrator creates an account with a unique normalized email and role;
  no public registration. Activation expires after 24 hours and works once;
  expired/superseded credentials fail without redisplaying secrets.
- [ ] Recovery expires after one hour and works once. Issuance/use invalidate earlier
  credentials and sessions; the old password fails after successful recovery.
- [ ] Deactivation denies login, existing sessions and protected actions. Reactivation
  requires an administrator reason and fresh activation, preserving ended memberships
  and history without restoring memberships. Existing legacy accounts stay active.
- [ ] Role changes revoke stale access. The last active Administrator cannot be
  deactivated/demoted. Profile/role changes remain audited; secrets are not logged.

## D03 — Cohorts, dated cycles and project setup

Source: FR-05–10. Where: **Set up learning → cohort → Learning cycles → Projects**;
cohort/membership, `ILearningCycleService`, `IProjectService`, `IMilestoneService`,
`IMilestoneAssignmentService`, `IProjectTeamService`.
Evidence: existing cohort, cycle, project, milestone, team and assignment tests.

- [ ] Administrator creates cohorts/cycles and dated Candidate/Mentor memberships;
  invalid ranges and unauthorized callers fail, preserving membership history.
- [ ] Individual/team project mode determines valid assignment ownership. Create
  ordered milestones with instructions, acceptance/evidence expectations and deadline;
  project length is not hard-coded. Duplicate owner/milestone assignments fail.
- [ ] Only eligible cohort candidates join a project team; at most one current team
  per project. Membership in another project's team remains independent.
- [ ] Candidate/Mentor queries expose only current permitted learning scope;
  account/cohort administration does not become generally available to those roles.

## D04 — Courses and individual course assignments

Source: FR-11/12/14; approved certificate-file requirement.
Where: cycle **Courses**, `/Admin/Courses/{cycleId}`, `/Candidate/Courses`,
`/Mentor/Courses`; `ICourseService` and shared submission/review services.
Evidence: course/file integration tests and `CourseBrowserAcceptanceTests`.

- [ ] Administrator creates a catalogue course, offers it in a cycle and assigns it
  individually to an eligible candidate with saved requirements and SAST deadline.
  Invalid course URLs, duplicates and unauthorized setup fail.
- [ ] Owner sees the course and requirements; another candidate cannot inherit access
  or completion. Team approval never completes an individual course.
- [ ] Links alone do not satisfy course evidence: at least one actual allowed file
  is required. Committed certificates use the same review/history/progress policies.

## D05 — Exact requirements revisions and historical timing

Source: FR-12/13/16/17; agreed revision/approval policies superseding older drafts.
Where: assignment revision/history links; `IAssignmentRevisionService`, history services.
Evidence: assignment revision, submission history and review workflow tests.

- [ ] Reasoned administrator edits append revisions; preserve old instructions,
  acceptance, evidence policy and deadlines. A new submission requires latest revision.
- [ ] Each attempt/history/review shows its exact submitted revision and original
  deadline, never a substituted current revision. Timing is separate from review status.
- [ ] `ReceivedAt == referenced DueAt` is on time; `ReceivedAt > DueAt` is late.
  Late submissions are accepted. Display receipt times/deadlines in SAST.
- [ ] Deadline-only changes preserve effective approval. Any intervening change to
  Instructions, AcceptanceExpectations or EvidencePolicy invalidates that approval
  permanently, even if later reverted. Compare snapshots across intervening revisions.

## D06 — Individual evidence submission and isolation

Source: FR-14/16–19/24; HTTPS-only and service-authorization decisions.
Where: `/Candidate/Submit/{assignmentId}`, `IIndividualSubmissionService`.
Evidence: individual/page/authorization/concurrency tests; course browser journey.

- [ ] Current Candidate, assignment ownership, current cohort membership and active
  project/cycle/cohort are required. Another candidate in the same cohort is denied
  form, POST and history. Revoked role/membership or archive after GET blocks POST.
- [ ] Valid project evidence may be files, HTTPS links or both; explanation is required.
  Invalid/partial evidence yields readable validation without partial saves. Earlier
  success messages do not conceal errors from a later failed POST.
- [ ] Accepted link-only submissions return committed receipts; file uploads initially
  show Processing, not a committed attempt. Individual service rejects team assignments.

## D07 — Assignment-specific frozen team credit

Source: FR-08/15/22/24/28; **current-team access decision overrides BR-13/AC-09**.
Where: team cards, `/Candidate/TeamSubmit/{assignmentId}`, team history;
`ITeamSubmissionService`, team access and roster entities.
Evidence: team submission, page, concurrency and review tests; user-confirmed browser checks.

- [ ] First successful submission freezes the assignment's current eligible team
  Candidate/cohort roster, including submitter. Save identity/membership snapshots,
  freeze time/actor, and actual submitter separately. Another assignment freezes
  independently. Failed processing creates no partial roster/submission/evidence.
- [ ] Preview is authoritative; forged/stale confirmation fails. Concurrent first
  submissions cannot silently credit conflicting rosters or lose submitted evidence.
- [ ] After Alice/Bob freeze, Bob leaves and Charlie joins: roster credit remains
  Alice/Bob; current Alice can submit; current Charlie can read shared history but
  cannot submit or gain credit; Bob cannot read/history/download/POST on credit alone.
- [ ] Later submissions require current Candidate/cohort/assigned-team membership
  **and frozen credit**. Role/cohort loss revokes reads too. No eligible credited
  member means submission is blocked with an explanation; no automatic reset or
  restoration. Revisions, approval changes and reopening never reset attribution.

## D08 — Evidence-link editor and safe rendering

Source: FR-14/19/22; approved evidence-editor/HTTPS policies supersede HTTP draft.
Where: individual/team forms, shared `_EvidenceLinks` and submission history.
Evidence: evidence-entry HTTP and keyboard/browser checks; user review passed.

- [ ] One empty Label/HTTPS URL row initially; Add another link stops at ten.
  Remove/reindex a middle row without losing other values or breaking binding/focus.
- [ ] Failed POST retains rows, values and validation messages. Entirely blank optional
  rows are ignored; partially filled rows are validated. Server still enforces limits.
- [ ] Explanations/labels/feedback render as encoded text; only valid HTTPS evidence
  URLs become clickable. Unsupported schemes are denied; submitted URLs are not
  automatically fetched. Course copy still requires an actual certificate file.

## D09 — Private file validation, jobs and downloads

Source: FR-18; NFR-03/04; approved file/storage/scanner policies.
Where: `IFileEvidenceService`, private storage/scanner, `/Candidate/Processing/{attemptId}`,
`/Evidence/Download/{fileId}`, `EvidenceWorker`.
Evidence: file/race tests; real Defender operational cases; course browser and hosted-worker checks.

- [ ] Accept PDF/PNG/JPEG only; validate actual content and extension, not client MIME.
  Enforce at most 5 files, 10,485,760 bytes each, 52,428,800 total file bytes.
  Test each exact boundary and one above it in isolated tests; DOCX is not allowed.
- [ ] Use generated private storage identifiers outside repository/`wwwroot`.
  Scanner failure/timeout, unsafe/missing/uncommitted files never permit download
  or review. Scan and later serve the same verified bytes; expose no physical path.
- [ ] File receipt time is complete-payload durability time. Finalization atomically
  publishes metadata/evidence/roster/version; recheck revision, authorization and
  workflow state. Intervening revision rejects with refresh/resubmit guidance.
- [ ] Downloads reauthorize owner/current team/current scoped mentor or administrator;
  use safe attachment/no-store headers and exact bytes. Altering an identifier cannot
  expose another candidate's certificate or another team's files.
- [ ] Actual worker picks up durable jobs; fresh host recovers expired leases without
  duplicate attempts or receipts. Accepted retry keeps original receipt. Record
  clearly whether lease expiry or failures were simulated and whether scanning was real.
- [ ] Abandoned staging expires after 24 hours; failed/quarantined retention is seven
  days. Cleanup excludes active jobs, holds and committed evidence and deletes only
  demonstrably unused subsystem-owned files. Never simulate failures in Development.

## D10 — Mentor decisions and eligibility

Source: FR-20–22/24; agreed review policies override mandatory approval notes in draft.
Where: `/Mentor/ReviewSubmission/{assignmentId}`, `IIndividualSubmissionReviewService`.
Evidence: individual/team review, page and concurrency tests; course browser acceptance.

- [ ] Require current Mentor role/current cohort mentor membership/unarchived
  ancestors in the decision transaction. Candidate membership ending does not revoke
  mentor access. Another cohort or Candidate-only account cannot review.
- [ ] Reject self-review by the actual submitter **or any frozen credited member**,
  including former team members and dual-role accounts. Administrator alone is not
  a reviewer. Review references the latest eligible attempt and its original revision.
- [ ] Approved feedback is optional; ChangesRequested requires nonblank feedback.
  Feedback limit is 10,000 characters. Decisions append, retaining reviewer/time/receipt.
- [ ] After ChangesRequested, a fresh attempt is needed before another decision;
  reopening does not bypass this. Deadline-only revisions permit eligible review;
  any intervening requirement change requires a new attempt.

## D11 — Effective approval and administrator reopening

Source: FR-23 superseded by **administrator-only** reopening and agreed invalidation rules.
Where: `/Admin/Projects/ReopenAssignment` assignment links;
`IAssignmentReopeningService` and shared workflow policy.
Evidence: review/reopening service, page, team and race tests.

- [ ] Effective approval blocks new submissions; identical accepted retries still
  return their original receipts after current submission authorization is checked.
- [ ] Only current Administrator can reopen an effective approval, with a nonblank
  reason of at most 1,000 characters. Append an event; preserve decision/revision/
  deadline/roster. Team workflows obey the same rule; no mentor powers are added.
- [ ] Reopening permits a new submission or reconsideration of the latest eligible
  attempt. Repeat approve/reopen cycles retain history; ChangedRequirements and
  ChangesRequested rules remain intact. Stale forms/changed request reuse fail.

## D12 — Concurrency, idempotency and rollback

Source: FR-16/17/24; NFR-04; assignment-version/locking decisions.
Where: submission, file, revision, review, reopening and membership services.
Evidence: existing submission/team/review/file concurrency and rollback tests.

- [ ] Coordinate separate DI scopes/DbContexts with barriers, not arbitrary sleeps.
  Identical concurrent requests create one submission/evidence set/receipt. Request
  IDs are scoped to actual submitter; changed assignment/revision/text/evidence reuse fails.
- [ ] Distinct concurrent requests never duplicate attempt numbering. Explicit
  retryable conflicts are acceptable; retries must preserve accepted data.
- [ ] Newer submission/review/revision/reopening invalidates stale review/reopening
  forms; accepted identical retries preserve receipts. Coordinate writes through
  assignment versions and documented lock order, including role/membership changes.
- [ ] Injected storage/database/finalization failure leaves no partial visible
  evidence, submission, roster or approval, and no false success. Durable recovery
  must not overwrite history. Run faults only against uniquely owned test resources.

## D13 — Read-only history and feedback

Source: FR-12/16/17/22/28 plus current access/revision decisions.
Where: candidate individual/team history, mentor history, administrator history;
individual submission/history and archive query services.
Evidence: submission/mentor history tests, browser course/archive journeys and team recheck.

- [ ] Show newest attempts first, 20 per page; verify 0/1/20/21 attempts, navigation
  boundaries, no missing/duplicate rows, attempt number, receipt and SAST receipt time.
- [ ] Show exact saved requirements/deadline/revision, explanation, evidence,
  actual submitter, frozen team credit and retained decisions/feedback as applicable.
  Clearly distinguish effective, requirement-invalidated and reopened approval.
- [ ] Current Candidate role, ownership/current assigned-team membership, current
  cohort membership and unarchived ancestors govern candidate reads. Historical
  attribution survives denial. Current scoped mentors can read after candidate departure.

## D14 — Progress, dashboards and mentor review queue

Source: FR-20/25/26; agreed required-assignment and current-access progress rules.
Where: `/Candidate`, `/Candidate/Progress`, `/Mentor`, `/Mentor/Progress`,
`/Mentor/ReviewQueue`, `/Admin`; `ILearningOverviewService`.
Evidence: overview/queue/dashboard tests and accepted UI framework; course browser totals.

- [ ] Separate selected-cycle Theory/Project numerator, denominator and percentage;
  only effective approval completes required work. Optional work is excluded;
  empty streams say No assignments. Percentage parsing accepts locale formatting.
- [ ] Frozen team work counts only for credited members retaining current Candidate/
  cohort/team access; eligible members count before freeze. Later joiners read without
  frozen credit/totals. Team outcomes are not proof of individual contribution.
- [ ] Prioritize ChangesRequested and upcoming/overdue candidate action. Pending
  on-time mentor review is not overdue candidate work. Recent feedback/routes use
  authorized records; staff dashboards do not invent decorative metrics.
- [ ] Queue is mentor-scoped, oldest eligible pending receipt first, 20 per page,
  with cohort/cycle/project/course/status filters. Missing/pending evidence and
  self-review block decisions. Candidate changes remain separate from pending review.

## D15 — Administrator archives and searchable audit

Source: FR-27–30; administrator-only archives and separate scope/approval reopening policies.
Where: `/Admin/Archives`, `/Admin/SubmissionHistory`, `/Admin/Audit`;
`IArchivedHistoryService`, `IAuditQueryService`.
Evidence: `ArchiveAuditTests`, archived-file coverage and `ArchiveBrowserAcceptanceTests`.

- [ ] Reasoned project/cycle archive retains saved outcomes, receipts, evidence,
  decisions and historical-record metadata/rowversions. The changed scope's own
  rowversion advances; do not expect every database rowversion to remain unchanged.
  Current candidate/mentor history/review/download
  direct routes are denied while administrator archived history/downloads remain available.
- [ ] Reasoned scope reopening restores eligible reads/writes but never reopens
  effective approval. Preserve archive/reopen snapshots and reasons; stale forms and
  changed reuse fail. No historical state is overwritten.
- [ ] Administrator searches actor/action/record/date with 20-row pagination;
  transactional audit survives successful changes and rolls back with failed writes.
  Existing typed history is read without fabricated backfill. Ordinary roles cannot
  change/delete audit records; output does not include credentials/file contents.

## D16 — Shared UI framework and additive data preservation

Source: NFR-01/04/06/11; approved CAPACITI shell and migration-preservation requirements.
Where: shared layout/CSS/navigation, evidence editor, existing migration upgrade tests.
Evidence: user-approved UI framework, focused desktop/mobile checks and populated upgrades.

- [ ] Role-appropriate desktop sidebar, compact account controls, breadcrumbs and
  mobile menu preserve routes/auth. Keyboard focus is visible; forms have labels
  and validation; main actions are usable without depending on color alone.
- [ ] Keep observed CAPACITI tokens `#1D2951` and `#F25251`, light surfaces and
  restrained status colors. Do not treat broader WCAG/browser/device/load certification
  as already verified; record viewport/browser/version for any new checks.
- [ ] For each schema upgrade, migrate a populated uniquely named temporary database
  from its immediate predecessor; compare old records/audit fields/rowversions;
  no fabricated reviews/rosters/account states or Identity string-key conversion.
  Inspect additive SQL/constraints; never run destructive upgrade tests on Development.

## Outside the completed-deliverable checklist

Do not convert these into supposedly missing implemented features:

| Classification | Scope / test implication |
| --- | --- |
| Superseded draft expectations | HTTP evidence links; mentor approval reopening; former-team-member reads on credit alone; new-member denial of all history; mandatory fresh attempt after every administrator reopening; revision-number-only approval invalidation; older Identity key types. Use D05–D13 instead. |
| Explicitly deferred / policy open | DOCX, cancellation, roster correction/reset/supersession, scorecards, reminders, advanced reporting. No acceptance test should require them as delivered behavior. |
| Partially verified / unverified nonfunctional work | Full 360px/1366px keyboard/device matrix, Edge/Android compatibility, broader accessibility, representative load/performance, production availability/monitoring, coordinated database/evidence restore, approved retention/privacy procedures. Original NFR-05–12 contain proposals/open decisions; agree targets before asserting compliance. |
| Simulation limits | Expiry and expired leases changed only disposable timestamps. Host restart is not a killed-process crash; benign real Defender samples are not malicious-sample detection certification. |
| Verification-history limitation | Earlier full suite had failures later covered by passing affected retries. New opt-in acceptances passed separately; no fresh fully green full-suite run or production acceptance is claimed. |

Reuse existing coverage before adding tests. Choose a focused set during changes;
do not repeatedly run passing suites. Broader release runs require their own
checkpoint; record failures honestly rather than weakening expected behavior.
## Structured Workspace UI review (approved 12 November 2026)

Use the same D01–D16 functional scenarios above; the redesign adds no business
requirements. Compare production pages with [the UI framework](ui-design-framework.md).

- [ ] Each role has its permitted grouped navigation, account controls and breadcrumbs;
  desktop sidebar and mobile menu retain visible keyboard focus and Escape restoration.
- [ ] Candidate courses/projects lead to original requirements, individual/team evidence
  entry, Processing and history. Form errors retain values; roster and actual submitter
  remain distinct. History keeps exact revisions, deadlines, timing and review status.
- [ ] Mentor queue leads to evidence, saved submitted requirements, decision and earlier
  feedback; mobile order remains usable. Verify existing stale/self-review denials.
- [ ] Administrator People/Learning/Governance pages use consistent forms, filters,
  scrollable tables and history. Archive scope reopening remains separate from approval
  reopening. Account activation/recovery forms retain their existing security behavior.
- [ ] At desktop and 390px width, inspect long titles/feedback, empty/results states,
  validation, evidence rows and horizontally scrolling tables. No page-wide overflow.

Automated presentation coverage: `WorkspacePresentationTests` plus opt-in
`CPT_WORKSPACE_VISUAL=1`, reusing authenticated HTTP responses from disposable data.
Browser snapshots verify layout, not real POST interactions; existing affected HTTP
workflow tests retain authorization, antiforgery, concurrency and validation checks.
Actual run results and any remaining manual review are in the delivery checklist.

- [ ] CAPACITI branding: supplied logo appears undistorted on white in desktop/sidebar
  and the mobile drawer, with accessible name CAPACITI and the correct role dashboard
  link. Footer/title use CAPACITI. Preserve role navigation and current page layouts.
- [ ] Current Overview baseline (16 November): all three roles use shared light-grey
  workspace backgrounds, opaque white panels, dark neutral text and navy primary
  actions. No gradient, diagonal decorations, background logo watermark or dashboard
  glass/blur. These expectations supersede the earlier decorative/glass theme.
- [ ] Inspect dropdowns, keyboard focus, table scrolling and page-bottom rendering at
  desktop/mobile sizes. Snapshot checks do not replace human visual approval or
  fresh authenticated workflow acceptance; record actual results in the delivery tracker.

## NFR-06/07: original-SRS core-form usability

Source: SRS ?8 (360px/1366px, keyboard, labels, readable errors, selected Chrome/Edge
and representative Android); proposal ?6.1/6.3/6.5 supplies the underlying workflows.
See the delivery checklist for current evidence and remaining scope.

- [ ] Individual and team forms: error summary and field message remain readable;
  entered values remain; Tab/Shift+Tab reaches labelled file/explanation/link fields
  and the form action with visible focus. Inspect narrow layouts and mobile menu.
- [ ] Mentor: original evidence/requirements remain readable; blank ChangesRequested
  feedback produces an understandable error; keyboard reaches decision/feedback/action.
- [ ] Record actual Chrome/Edge versions and widths. Test each browser's actual
  identity; a shared debugging port must not substitute Chrome for Edge.
- [ ] Complete hands-on native file selection, submit/download/review and a real
  Android Chrome device journey. Desktop viewport emulation is not Android acceptance.

Opt-in automated helper: `UsabilityBrowserAcceptanceTests`, reusing real authorised
HTTP validation responses and the GET-only browser snapshot bridge. Browser replay
checks rendering/keyboard behavior; it does not claim browser POST/file-transfer
acceptance. Uses isolated temporary data and existing installed browsers only.


### Hands-on native/device continuation ? 13 November 2026

Use [native transfer, Android and current UI task script](native-file-android-ui-acceptance.md).
Actual Edge browser-managed disk download now has focused passing evidence; see the
single delivery tracker for exact results/limits. Windows OS-picker interaction,
physical Android and human UI approval are separate unchecked acceptance tasks.
The Mentor mobile full-page capture requires a scrolling check; do not infer its
blank offscreen regions prove either a passed UI or an application defect.


## Bulk cohort onboarding extension ? Interval 1 test design

Source: explicit bulk-onboarding request. This section is a test-design reference;
actual run results and acceptance status belong in the delivery checklist.

- [ ] Current Administrator: Cohort ? Members ? Bulk onboard ? Download template ?
  upload CSV ? preview for one cohort and membership start date. Interval 1 had no
  Confirm action; current Interval 2 enables protected confirmation only after its migration.
- [ ] UTF-8 (including Excel BOM), quoted/escaped fields, CRLF/LF and blank rows work;
  malformed quoting, invalid encoding, unsupported/duplicate columns, empty data,
  over 100 candidates or over 1,048,576 bytes produce actionable errors.
- [ ] Match using Identity normalized username, show matched username/email, preserve
  existing email/profile. New pending-account proposals require valid unique email.
- [ ] Active Candidate without membership ? Enrol existing; current membership covering
  the batch date ? Skip (including a scheduled end); ended/future/conflicting memberships, inactive/pending/locked accounts or
  additional/missing Candidate role ? Conflict for individual handling.
- [ ] Duplicate normalized usernames/emails mark every affected row. Errors identify
  CSV starting line, field and correction; no invalid row becomes a create proposal.
- [ ] Service and page deny noncurrent/non-Administrator and archived cohort access;
  antiforgery enforced on upload; no-store matching responses; encode user text.
- [ ] Compare accounts/password/security stamps, roles, memberships/rowversions,
  credentials/account states and audit counts before/after preview: unchanged.
- [ ] Template exports headers only. Raw upload is not stored; no candidate rows or
  formula-like user data exported in this interval. Results CSV sanitization is
  required in Interval 3 before any user-value export is enabled.

Interval 1 evidence covers preview only. Interval 2 confirmation/import and tracking
are recorded separately below; export/activation handoff and broader acceptance
remain later intervals. Do not infer future-deliverable acceptance from preview tests.


## Bulk cohort onboarding ? Interval 2 test design

- [ ] Preview remains read-only. An eligible preview offers antiforgery-protected
  confirmation using an actor/cohort-bound ticket; expired/corrupt tickets fail.
- [ ] Revalidate current account/cohort eligibility and matching identity; stale
  preview, archived cohort and revoked/non-Administrator access cannot import.
- [ ] Concurrent/double confirmation saves one batch, account and membership per
  intended row; same request with changed payload fails. Existing accounts keep
  password/security stamps, profiles, roles and credentials unchanged.
- [ ] New account is passwordless/pending with its membership; no activation token
  or email is issued. Membership starts at the selected date's SAST midnight.
- [ ] Simulate membership write failure after Identity writes: no orphan account;
  earlier rows remain committed, later rows unprocessed. Retry only unfinished rows,
  retaining original IDs and rowversions for successful rows.
- [ ] Saved owner-only batches allow recovery after a lost response/expired ticket.
  Status page distinguishes completed, skipped, failed and pending rows; resume
  requires antiforgery and current authorization again.
- [ ] Upgrade a populated predecessor in an owned temporary database; compare every
  original table/column/row/rowversion. New batch tables start empty; populated
  downgrade is blocked. Review SQL and obtain fresh approval before Development use.

These boxes are test-design tasks, not duplicated delivery statuses. Actual runs,
manual acceptance and limitations are recorded only in delivery-checklist.md.


## Bulk cohort onboarding ? Interval 3 test design

- [ ] Saved results show each row outcome and link successfully processed/matched
  accounts to the existing account management page. Created accounts use that page's
  reasoned activation workflow; opening results or exporting never issues credentials.
- [ ] Owner/current Administrator can download a UTF-8 BOM results CSV with row,
  username, status and details; no passwords, credentials, tokens or raw upload.
  Incomplete batches export current partial outcomes, including pending/failed rows.
- [ ] Export quotes delimiters/quotes/newlines and neutralizes spreadsheet formula
  prefixes, including leading whitespace. Download has no-store and attachment headers.
- [ ] Non-Administrators, revoked Administrators and another batch owner cannot
  export private results. Existing owner/current-Administrator service checks apply.
- [ ] Find batch and row audit events using the batch identifier. Verify attributed
  account/membership events and retained successful-row history; exporting adds no
  account/membership/credential changes. Audit details contain field names, not secrets.

Actual results and remaining hands-on acceptance are recorded in delivery-checklist.md.


## Bulk cohort onboarding ? Interval 4 acceptance test design

Latest actual outcome is in the [MVP/delivery checklist](delivery-checklist.md):
seven focused cases passed in the prior checkpoint; latest isolated browser
acceptance passed 1/1 after instrumented asset probes. Earlier failed runs and
limits remain recorded there. Unchecked items below are spaces for your new test
execution, not an assertion that the recorded automated checks are missing.

- [ ] Live Chrome: cohort members ? bulk onboard ? template ? actual CSV file
  input ? duplicate preview blocked ? Create / Enrol existing / Skip preview ?
  confirm ? persisted row results ? private results CSV ? existing activation
  management screen without issuing a credential ? batch audit search.
- [ ] Preview/duplicates/conflicts leave accounts and memberships unchanged; current,
  ended/future membership and inactive/role conflicts follow the agreed rules.
- [ ] Concurrent confirmation/replay preserves one account/membership per row.
- [ ] After an injected membership-write failure, earlier successful rows remain;
  unfinished identities are absent. Revoked Administrator cannot read/resume;
  archived cohort pauses processing. Once eligibility is restored in disposable
  test setup, a fresh service scope resumes only unfinished rows with original
  successful IDs/rowversions unchanged.
- [ ] Keep interruption evidence precise: row rollback and fresh-scope recovery do
  not certify hard process termination during commit or power-loss recovery.

Actual passes/failures and acceptance limitations belong in delivery-checklist.md.
