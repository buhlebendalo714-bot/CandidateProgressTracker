# Theory assignments and protected evidence: proposed delivery batch

Original planning record, 7 November 2026. Implementation and approved policy details
are now recorded in [the delivery handoff](course-file-evidence-handoff.md).
The proposed choices below are superseded by the approved decisions recorded there.

Planning baseline, 7 November 2026. No application/schema changes or new verification
runs accompany this document. Baseline: 258 integration tests passed, clean build,
team browser checks and revised Bob access recheck passed (user confirmation).
Those results do not establish full MVP scope completeness.

## Sources and precedence

- Scope **v0.2**: [Scope and MVP (1)](requirements/Candidate_Progress_Tracker_01_Scope_and_MVP%20%281%29.md).
  The similarly named file without `(1)` is the earlier v0.1.
- [SRS v0.1](requirements/Candidate_Progress_Tracker_02_SRS.md): especially
  FR-11â€“19, FR-21â€“24, NFR-01/03/04, AC-02/03/11.
- Supporting drafts: `04_System_Architecture.docx` sections 5â€“8/10â€“11,
  `05_Database_Design.docx` course/assignment/evidence/processing sections,
  `07_Application_Service_Contracts.docx` candidate/evidence contracts, and
  `06_Screens_and_Navigation.pdf` C03/C04/M03/A03 and essential states.
- Later explicit conversation decisions override drafts: current role/scope and
  unarchived ancestry checks; current team membership for candidate reading;
  frozen credit additionally required for team POST; no roster correction;
  administrator-only approval reopening; optional approval feedback; permanent
  requirement-change invalidation including revert; deadline-only preservation;
  eligible reconsideration after reopening; accepted idempotent retries.
  Keep Identity string keys and the existing cohort-gate/assignment-lock order.

## Proposed implementation

### Reuse the assignment workflow

Add a course catalogue (title/platform/external URL) and cycle-specific offering
with immutable assigned metadata snapshots. An administrator creates an offering
under a cycle and individual assignments with requirements, required flag and
deadline. Catalogue edits must not silently alter assigned offerings. Propose one
offering per course/cycle and one assignment per offering/candidate, matching the
database draft; recurring delivery in another cycle creates another offering.

Extend the existing Assignment target to either a milestone or a course offering;
courses always have CandidateId, never TeamId or a credited roster. Reuse
AssignmentRevision, Submission, SubmissionReview, AssignmentReopening and history,
not a parallel certificate-review state machine. Generalize currently mandatory
project/milestone joins in assignment queries, revision/history services,
AssignmentWorkflow authorization and individual submission eligibility. Courses
inherit cycle/cohort archive restrictions; milestones retain project restrictions.
Do not simulate courses using dummy projects or milestones.

Reuse exact-revision history, SAST display, timing separate from outcome,
newest-first 20-attempt pagination, feedback validation, approval blocking,
permanent requirement invalidation, latest-attempt review, self-review denial,
ChangesRequested/new-attempt rule, audited admin reopening and request replay rules.
Course submissions never grant team completion or team certificate access.

Add Admin cycle course setup/assignment navigation and Candidate My courses/detail
(C04); reuse the submission, mentor review, administrator reopening and shared
history presentation. Add files to existing individual/team milestone forms as
well. Course acceptance requires at least one clean certificate file; links may
supplement it, never replace it. Project evidence may be allowed files, HTTPS
links or both with explanation. Keep existing link-only milestone behavior.
Show processing, rejection/retry and committed receipt as different states.

### Private files and authorized downloads

Application contracts: private storage and scanner adapters; begin/receive upload,
get processing status, finalize validated evidence, and authorize/download evidence.
Infrastructure implements storage/scanning and durable leased job processing;
Razor handlers translate results and stream files. A supervised worker can share
the existing layered codebase; no business rules move into page handlers.

Store bytes outside wwwroot using generated immutable object identifiers. Never
use original filenames as paths or expose object keys/public URLs. Retain display
filename, detected type, length, SHA-256, scanner result/time and provenance.
Each download rechecks current database role and assignment scope in the service:
individual/course owner plus current candidate cohort membership; team current
membership (credit alone is insufficient); current assigned Mentor regardless of
candidate membership; current Administrator through explicit administrative access.
Preserve ancestor restrictions. Unknown/denied IDs disclose no evidence metadata.
Only committed, clean files are downloadable. Stream as an attachment with a safe
filename, verified content type, nosniff and private/no-store headers. No inline
document rendering or long-lived bearer storage links. External evidence stays
HTTPS-only and is never fetched automatically.

### Validation, durable failure handling and cleanup

1. Authorize before upload, enforce bounded streaming/count/aggregate limits, then
   quarantine privately. Check extension, actual structure/type, nonempty length,
   integrity and unsafe/encrypted content; browser MIME is not authoritative.
   DOCX, if approved, needs bounded package/decompression checks, not a blanket ZIP
   allowance. Malware scanning supplements rather than replaces type validation.
2. After the complete immutable manifest/bytes are durable, record a processing
   attempt, frozen revision, actor, request ID, payload/file hashes, expected token,
   receipt time and durable job. Pending evidence is neither submitted nor reviewable.
3. A leased worker scans with bounded retries; unknown/unavailable scanner results
   remain blocked, never treated as clean. Recover leases after restart and expose
   safe candidate status/retry guidance plus correlated operator diagnostics.
4. After clean objects are durably ready, finalize in a short SQL transaction using
   the existing cohort gate then assignment lock. Recheck current role/membership,
   ancestry, revision/token, approval, actor and manifest. For team files, recheck
   authoritative roster confirmation and freeze only on successful finalization;
   membership/credit changes require refreshed confirmation, never silent credit
   changes. No locks remain held while receiving bytes or scanning.
5. Atomically insert the numbered submission, file associations, existing link
   evidence, roster if applicable, and assignment-version advancement; complete the
   job. Repeated worker execution must return the same accepted receipt. Integrate
   processing IDs with existing actor-scoped submission request IDs so cross-path
   or changed-payload reuse cannot create a second request. Accepted retries retain
   their original receipt/revision after current submission authorization.

SQL and storage are not one transaction: persist immutable clean objects before
publishing references. A failed scan/storage write/SQL commit leaves the earlier
submission, review and approval unchanged. Missing/corrupt committed bytes block
downloads and new review decisions and raise an integrity incident, without
rewriting historical decisions. Never claim success while bytes are unavailable.
Reconciliation retries failed work and deletes only demonstrably unreferenced,
expired staged objects after checking active leases/jobs, submissions and backup
holds. Rejected quarantine expiry and operational retention need explicit policy;
do not delete retained evidence automatically or conflate this with SQL test DB cleanup.

### Minimal additive schema and verification

- New Course and CourseOffering tables; new nullable OfferingId on Assignments;
  make ProjectId/MilestoneId nullable for course targets. Use XOR target/owner
  checks, scoped offering/cycle FK and filtered unique offering/candidate index.
  Existing nonnull milestone targets retain their values; no fabricated courses.
- New upload-processing attempt, file metadata and durable job tables. Optional
  submission association is populated only on successful finalization, with scoped
  FKs and unique request/finalization constraints. Existing SubmissionEvidence
  remains the link table; no link-to-file conversion or old submission backfill.
- Preserve all existing revisions, review/reopening records, evidence, Identity
  string IDs and rowversions. Adapt assignment/roster triggers and queries for
  course targets without weakening team invariants. Do not replace the current
  schema wholesale with the older database draft or add its unrelated state tables.
- Later generate/review migration and SQL; populated upgrade tests must compare
  existing values and rowversions. Apply only to unique temporary test DBs until
  separately authorized for Development.
- Focused tests: course owner/cohort/mentor isolation; forged download IDs and
  former-team denial; mandatory certificate; type/size/content/scanner failures;
  immutable hashes and safe headers; worker restart/duplicate finalization;
  revocation/revision/review/membership races; storage/SQL rollback and safe orphan
  cleanup; course approval/reopening/history and existing individual/team regressions.
  Reuse current helpers and avoid duplicated service/HTTP boundaries.

## Choices requiring agreement before implementation

All numbers here are proposals, not approved limits.

| Choice | Recommendation / consequence |
| --- | --- |
| Formats and limits (SRS P-06/NFR-03) | Start with PDF/PNG/JPEG for certificates and files; decide whether DOCX is needed for project documents. The draft proposes PDF/PNG/JPEG/DOCX, five files, 10 MB each. If adopted, explicitly define bytes (recommend 10 MiB/file), 50 MiB file aggregate and bounded multipart overhead. Reject executables, scripts, archives, macro documents and uninspectable/encrypted content. Keep existing link limits separately. |
| Storage/scanner and operation | Recommend private durable filesystem adapter locally and a real ClamAV-compatible adapter with durable worker jobs; deterministic fake scanner only in tests. Confirm scanner availability and private storage location/capacity for local acceptance. Production provider/hosting remains deferred; use the same interfaces, coordinated backups and no unscanned production bypass. A managed scanner/private object store is an alternative if chosen. |
| Async receipt, staleness and expiry (WF-D02/NFR-04) | Recommend lateness from server acceptance of the complete durable payload, only when validation later succeeds; store finalization time separately. Preserve existing link-only receipt semantics. If assignment version changes before finalization, require refresh/new confirmation rather than silently rebasing. Agree pending/rejected/orphan expiry and backup hold policy before enabling deletion; recommend 24-hour abandoned-upload grace as a starting proposal, not permission to delete anything now. |

Certificate exceptions (WF-D03) are not needed for this batch: retain the required
file rule. No alternative evidence-only path is proposed.

## Compact remaining-MVP checklist

This is a targeted document/code comparison, not a fresh broad audit. SRS v0.1 is
a requirements draft; operational targets remain provisional until agreed.

| IDs | Status | Remaining work / disposition |
| --- | --- | --- |
| MVP-04; FR-11, course portion of FR-12/13/22; SC-02 | Implemented; browser acceptance pending | Delivered catalogue/offerings, individual assignments and course workflow. Independent theory totals remain in the dashboard batch. |
| MVP-05/10; FR-14/18; NFR-03/04; SC-06, AC-11 | Implemented locally; operational acceptance pending | File evidence, durable quarantine/scanning jobs, protected downloads and cleanup delivered. Real local benign-file scan succeeded; representative browser files and production operation remain unverified. |
| MVP-07/08; FR-20/25/26; BR-02â€“04/10; AC-07 | Missing / partial | Basic scoped assignment/cohort lists exist; filtered oldest-pending review queue, separate progress totals and actionable dashboards remain. Excluded from this batch, not deferred beyond MVP. Cancellation/denominator treatment still needs a baseline before progress implementation. |
| MVP-01; FR-02â€“04; NFR-02/12; AC-01 | Partial | Username account creation/login and current-role service guards exist. Draft normalized-email/profile/role management, activation/recovery/deactivation and comprehensive next-request account/session revocation are not established by that implementation. Separate account-lifecycle batch, not silently removed from MVP. |
| MVP-09; FR-27/28; BR-14; AC-12 | Partial / superseded in part | Provenance/history retained, but archived-cycle/project read views, archive/reopen operations and historical outcomes are incomplete. Candidate history remains subject to current membership and unarchived ancestry by later agreement; do not relax access to satisfy the older draft. |
| MVP-10; FR-29/30; NFR-10 | Partial | Typed creation/membership/revision/submission/review/reopening history exists; comprehensive protected audit/security events and administrator audit search remain. Audit search excluded from this batch, not a confirmed post-MVP deferral. |
| FR-19/21/23; BR-05/06/13; AC-09/10; P-01/03/05 | Superseded portions | HTTPS-only evidence; approval note optional; admin-only effective-approval reopening with eligible reconsideration; frozen roster/no correction; credited-current members submit, all currently eligible team members read, former members denied. Preserve implemented later policies. |
| NFR-05â€“09/11/12; SC-08; AC-13/14 | Unverified / partial; production work deferred | No new performance/browser/accessibility/restore/operational assessment here. Load, recovery, availability and upload numbers are proposals. CI/setup, storage/DB backup restore, monitoring, keys/secrets, retention, support owner and production provisioning need evidence before operational readiness. |
| FUT-01/02/03 | Deferred beyond MVP | Scorecards, scheduled reminders and advanced reporting. Presentation scheduling/rich workload visualization are proposed later work, not newly approved requirements. Integrations/course delivery and other scope exclusions remain excluded. |

Next delivery is this course/file batch only. Subsequent batches should address
progress/review queue, then unresolved operational/account/history/audit readiness
against the reconciled baseline. Do not treat those remaining MVP items as optional
because the existing 258 tests and browser checks pass.
