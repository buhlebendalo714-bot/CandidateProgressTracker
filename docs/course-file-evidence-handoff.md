# Course assignments and private file evidence

Implemented batch; verification results are recorded below when complete. Original
scope sources and the remaining-MVP checklist remain in
[the proposal](theory-file-evidence-proposal.md). The approved decisions in the
conversation replace its proposed formats, limits, retention and scanner choices.

## Behavior and configuration

Administrator: Administration -> Cohorts -> Learning cycles -> Courses. Create a
catalogue course, create its cycle offering, then assign it to an eligible individual
with saved requirements and a SAST deadline. Candidate: My cohorts -> My courses
and certificates -> Requirements and certificate submission. Mentor: My cohorts ->
Cohort course assignments -> History / Review latest attempt. Administrator course
assignment rows link to revisions and approval/reopening history.

The existing approval, revision, review, reopening and team attribution rules apply.
Course certificates remain individual. Project evidence supports files, HTTPS links
or both; existing link-only receipts keep their original timing. Course submissions
require actual files. Files allow PDF/PNG/JPEG only, five maximum, 10,485,760 bytes
per file and 52,428,800 total file bytes (HTTP multipart envelope: 53,477,376 bytes).
DOCX remains deferred. Structural validation and malware scanning are distinct;
mentor assessment still establishes whether evidence satisfies the assignment.

Set `Evidence:Root` (environment variable `Evidence__Root`) to an absolute private,
durable filesystem directory **outside the repository and wwwroot**. Example local
location: `%LOCALAPPDATA%/CandidateProgressTracker/Evidence`. Configuration is not
written automatically. Restrict directory access to the application/worker identity
and operators; do not mount it as static content. Application storage uses its own
`cpt-evidence-v1` subdirectory and generated immutable keys. Do not edit its objects
or ownership markers manually. Downloads expose neither those keys nor paths.

The default real scanner is installed Windows Defender, discovered under
`%ProgramData%/Microsoft/Windows Defender/Platform`. It invokes `MpCmdRun.exe`
custom-file scanning with remediation disabled on a private snapshot, enforces a
60-second timeout, and checks snapshot integrity. Only successful scan exit zero
permits finalization. Scanner errors, detection or timeout leave evidence blocked.
The ClamAV INSTREAM adapter is also present for a future explicitly configured
host; the default registration does not silently fall back to a test scanner.
No scanner or security software was installed or configured by this batch.

A real local benign-file probe on 7 November 2026 returned scan finished/no threats,
exit code 0. This confirms local scanner availability, not malicious-sample detection,
production operation, or the full browser upload workflow. Automated tests inject
an isolated deterministic scanner; their results are not operational scan evidence.

A hosted worker processes durable SQL jobs outside Testing, using short leases and
bounded scanner/storage retries. No in-memory queue is the source of truth. Complete
payload durability establishes file receipt time; Processing is not submission.
Finalization rechecks revision, assignment token, current authorization, approval
and team roster confirmation. It publishes submission metadata, evidence references,
roster freeze and assignment-version advancement atomically. Revisions while
processing require refresh/resubmit; accepted identical retries keep their receipt.
Missing/corrupt committed bytes block downloads and further review, preserving
historical decisions. Files are streamed as attachments after current authorization.

Processing older than 24 hours expires unless actively leased/held. Failed or expired
quarantine is retained seven days after its terminal outcome. Cleanup excludes
committed evidence, current leases, database holds and `.hold` storage markers.
Orphan cleanup requires a matching durable ownership marker, age over 24 hours,
no database reference and no explicit hold; uncertain files are retained. Upload
receipt has a ten-minute cancellation bound. Runtime cleanup never deletes SQL
databases or retained submission history. Coordinate evidence/SQL backups and holds
before real-data operation; production provisioning is outside this batch.

## Migration review

- Migration: `20261107065418_AddTheoryCoursesAndPrivateEvidence`.
- Immediate predecessor: `20261106192833_AddTeamSubmissionRosters`.
- SQL: `migrations/AddTheoryCoursesAndPrivateEvidence.sql`.
- Adds Courses, CourseOfferings, UploadAttempts (durable jobs/leases) and EvidenceFiles.
- Assignments gains nullable OfferingId; ProjectId/MilestoneId become nullable only
  for a constrained individual course target. Scoped FK and filtered uniqueness
  protect course/cycle ownership and prevent duplicate individual assignments.
- Replaces the assignment/project alternate-key roster FK with an assignment PK FK;
  existing immutable roster scope triggers still enforce the project/team match.
  Other roster/member relationships and attribution records remain unchanged.
- Adds immutable payload/committed-evidence guards. Trigger-backed tables disable
  EF SQL OUTPUT. No data backfill, fabricated course/file/roster, identity key
  conversion, legacy row update or evidence/review/reopening rewrite is intended.
- Downgrade is blocked to avoid losing retained evidence; use a reviewed forward
  repair. Development migration has not been applied in this batch.

## Local acceptance after separately authorized migration application

1. Configure private `Evidence__Root`; run with the installed Defender available.
   Create/offer/assign one theory course through the cycle Courses link.
2. Owner opens My courses, reads requirements and uploads a PDF/PNG/JPEG certificate.
   Observe Processing, refresh to a committed receipt, then inspect exact revision,
   saved deadline, SAST time and protected attachment download.
3. Assigned mentor opens course history/download, requests changes, then approves
   a new attempt. Candidate sees feedback; effective approval blocks new requests
   while an identical accepted retry retains its receipt. Administrator reopening
   retains decisions and allows eligible actions, using existing rules.
4. Check another candidate and an unassigned mentor cannot download by saved ID.
   For a project file, confirm a current later team joiner can read but not submit;
   a former member cannot download. Original credited roster remains unchanged.
5. With a scanner unavailable/timeout, evidence must remain unavailable. Change the
   assignment revision or end submitter eligibility while Processing; finalization
   must reject safely without a partial team roster. Restore scanner/configuration
   for later work; never enable a clean-scanner bypass.

No dashboard, review-queue expansion, account-lifecycle changes, audit search or
UI redesign is included. Full suite and production verification remain deferred.

## Verification and remaining acceptance

- Initial focused course/file and populated-upgrade run: **14 passed**.
- Focused team/file concurrency, rollback and cleanup run: **5 passed**.
- One affected regression run: **203 cases, 200 passed and 3 failed**. The failures
  exposed a test assumption that both concurrent job claimants must immediately
  finish all work. The real leased/READPAST worker permits deferred work. The tests
  now perform bounded recovery with separate scopes, retaining all atomicity,
  roster, attribution, numbering and authorization assertions, without sleeps.
- Only the affected race class was rerun after that correction: **4/4 passed**.
  All 203 distinct affected cases therefore have passing results across these runs;
  this is not a claim of a second all-green aggregate run. The new batch contributes
  19 service/HTTP/concurrency/upgrade cases. Full suite deferred to release checkpoint.
- Final solution build: **zero warnings/errors**. EF: **no pending model changes**.
  `git diff --check` passed.
- TRX files: `TestResults/course-files/course-files-focused.trx`,
  `course-files-races.trx`, `course-files-affected.trx` and
  `course-files-races-recovery.trx` in that directory.

No known failing affected test remains. The new browser/worker/storage workflow
has not been manually accepted, and the migration has not been applied to
Development. Automated file-flow fixtures use PNG; include representative real
PDF and JPEG certificates in local browser acceptance. The real scanner probe
used a benign file, not an infected sample; no production service identity,
filesystem ACLs, backup/restore or hosting is operationally verified. Configure
private storage and perform the acceptance above after separately authorized
migration application. No software installation, development data changes,
uncertain-database cleanup, commits, pushes or deployment were performed.
