# Team submissions and roster attribution

The first successful team attempt freezes one credited roster per assignment.
The server derives the preview from current team memberships, Candidate roles,
and candidate cohort memberships. Names and source membership IDs are saved with
the team name, freeze time and freezing actor. The preview fingerprint includes
the assignment, identities, names and source memberships; a changed fingerprint
requires reloading and confirming the preview. Another member may win the first
submission race only with the same confirmed roster; a different roster is never
silently accepted. Credit describes the shared assignment, not individual effort.

Current Candidate role, candidate cohort membership, current membership in the
assigned team and active ancestry are required for every candidate read. Current
later joiners can read shared history. Frozen roster credit alone grants no
access to history, evidence, approval decisions or feedback after leaving a team.
Historical credit and its audit/source records remain unchanged for authorized viewers.
Posting additionally requires current membership of the assigned team and, after
freeze, inclusion in its credited roster. This also applies to accepted retries.
Later joiners cannot acquire credit or submit on that assignment. With no
eligible credited members, further work needs a genuinely new team/assignment.
The Candidate assignments page links to credited assignments in current teams.
Former assignments disappear from that navigation and direct routes deny access.

Mentor and administrator authorization remain separate. Team mentors need the
current Mentor role and mentor cohort membership; candidate membership may end.
Neither the actual submitter nor anyone ever credited may review that assignment.
Administrators may reopen effective approvals with an audited reason, but do not
gain review powers. Review, requirements, approval invalidation and reopening
reuse the individual workflow policy. None of them changes the credited roster.

## Transaction and locking order

Workflow writes and consistent review/history reads use serializable transactions:

1. Resolve the cohort only for lock routing (a nonlocking read grants no access).
2. Acquire the transaction-owned exclusive SQL application lock
   `CandidateProgressTracker:Cohort:{cohortId:N}`.
3. For assignment operations, acquire `UPDLOCK, HOLDLOCK` on the assignment.
4. Re-read current roles, ancestry, team/cohort memberships, revisions and workflow
   state. Membership predicates use one UTC instant captured after waiting for
   locks: `StartsAt <= now && (EndsAt == null || now < EndsAt)`.
5. Save all inserts and advance Assignment.RowVersion, then commit.

Team membership creation/assignment/ending and cohort membership assignment/ending
and cohort archival take the same cohort gate before authorization or data reads.
New membership mutation paths must follow this order. Role mutations through
Identity are protected by the workflow's serializable row/range reads: a role
change is ordered before eligibility is read or waits until the workflow commits.
Roles granted/removed between opening a form and posting are rechecked. Scheduled
membership boundaries are evaluated at the transaction's authorization instant,
not the earlier form/HTTP receipt time. Receipt timing still uses the original
HTTP/service receipt instant against the submitted revision's deadline.

Roster and members are flushed first inside the transaction, followed by the
attempt, evidence and assignment update. Either both saves commit or neither
does. Database triggers prohibit editing/deleting rosters, adding members after
an attempt exists, changing a credited assignment's owner/scope, or saving a team
attempt without its own roster and a credited submitter. Composite foreign keys
tie every credited identity to its original team/cohort source memberships.
Request IDs remain unique per actual actor, with changed content rejected.
Retryable lock/concurrency failures require the same request ID or an explicit
preview reload; immediate success of every competing request is not guaranteed.
SQL execution timeouts also return an explicit retryable failure, rolling back
pending writes; accepted request IDs make retries safe if a commit response is lost.
The cohort gate intentionally serializes workflows in a cohort, trading throughput
for a simple consistent order across membership and assignment operations.

## Migration review

Migration: `20261106192833_AddTeamSubmissionRosters`.
Immediate predecessor: `20261106175348_AddIndividualSubmissionReviews`.
Generated upgrade SQL: [AddTeamSubmissionRosters.sql](migrations/AddTeamSubmissionRosters.sql).

The upgrade adds two tables, a nullable `Submissions.CreditedRosterAssignmentId`,
its index/check/FK, and composite unique keys on Assignments, TeamMemberships and
CohortMemberships for scoped foreign keys. Four triggers enforce immutability and
scope. Trigger creation uses its own dynamic SQL batch so the generated SQL remains
valid within the migration transaction. EF OUTPUT is disabled for trigger-backed
tables; normal assignment updates still return fresh rowversions. There are no
data updates, backfills, dropped columns, changed deadlines or fabricated rosters.
Existing individual attempts keep a null roster reference. Reviews, reopenings,
evidence and audit columns are untouched. Down removes the new schema and is not
appropriate after retaining team submissions; no downgrade is executed here.

All migration applications in automated verification target uniquely named
`CandidateProgressTracker_Tests_<guid>` databases. The populated upgrade test starts
at the immediate predecessor, snapshots all existing columns and rowversions,
applies this upgrade, and checks preservation and new relationship guards.
The user has applied the team migration and completed its five original browser
checks. The revised candidate visibility policy requires no schema change, and
this release batch does not apply any migration to Development.

## Implementation files

- Domain: `Learning/AssignmentCreditedRoster.cs` and the nullable reference in
  `Learning/Submission.cs`.
- Application: `Learning/ITeamSubmissionService.cs`, and shared history/review
  presentation fields in `IIndividualSubmissionQueryService.cs` and
  `IIndividualSubmissionReviewService.cs`.
- Infrastructure learning: `TeamSubmissionAccess.cs`, `TeamSubmissionService.cs`,
  `CohortWorkflowGate.cs`, `AssignmentWorkflow.cs`,
  `IndividualSubmissionReviewService.cs`, and the individual service's retryable
  lock-failure handling. Membership gates are added in `ProjectTeamService.cs`,
  `CohortMembershipService.cs`, `CohortMembershipEndingService.cs` and
  `CohortArchiveService.cs`.
- Persistence: `TrackerDbContext.cs`, `AssignmentCreditedRosterConfiguration.cs`,
  `SubmissionConfiguration.cs`, `AssignmentConfiguration.cs`, the new migration
  and designer, and `TrackerDbContextModelSnapshot.cs`.
- Web: Candidate `TeamSubmit`, `TeamSubmissionHistory`, and `CreditedTeamHistory`
  page pairs; Candidate `Assignments.cshtml`; shared `_MemberAssignments.cshtml`
  and `_SubmissionHistory.cshtml`; the Mentor `SubmissionHistory` page pair and
  `ReviewSubmission.cshtml`; Admin `Projects/ReopenAssignment.cshtml`; and
  service registration in `Program.cs`.
- Tests: `TeamTestContext.cs`, `TeamSubmissionTests.cs`, `TeamReviewTests.cs`,
  `TeamSubmissionConcurrencyTests.cs`, `TeamSubmissionPageTests.cs`,
  `TeamMigrationUpgradeTests.cs`, `SubmissionLockTimeoutTests.cs`, `PreTeamDbContext.cs`, plus registration in
  `TestDatabase.cs` and historical-schema support in `ReviewMigrationUpgradeTests.cs`.
- Documentation: this policy/locking/migration review and the generated SQL.

Earlier individual workflow files and other pre-existing uncommitted work are
retained. This inventory describes the team feature additions and extensions,
not every uncommitted file in the working tree.

## Verification results

- 50 new team service/HTTP/concurrency cases and 9 existing ProjectTeam cases passed.
- Both populated migration upgrades and both deterministic submission timeout/retry
  cases passed in an isolated serial run (4/4).
- The final serial individual submission/history/review, revision, member-assignment,
  cohort-ending and selected cohort authorization/archive regression run passed
  135/135. These focused runs cover 198 distinct passing cases in total.
- The solution build passed with zero warnings and errors.
- EF reported no pending model changes; `git diff --check` passed.

An earlier broader regression run was interrupted after SQL Server login and
cleanup timeouts; it is not counted as a successful run. Cleanup of temporary
databases from that interrupted run remains unverified. The passing runs above
used serial test-class execution for the upgrade/timeouts and final regressions;
their coordinated concurrency cases still use independent scopes/DbContexts.
These are historical implementation results. The user subsequently applied the
team migration and passed its five original browser checks. After the visibility
policy change, recheck only Bob's denied access and preservation of his credited
identity for current authorized viewers; the old former-member access expectation
is superseded. Production provisioning remains deferred.

## Revised visibility browser recheck

Use Bob with Candidate role and current candidate cohort membership, but an ended
membership in the assigned team. His old assignment must disappear from My
assignments and My credited assignments in current teams. Saved team history and
submission URLs must return Not Found; a form opened before leaving must not save
an attempt. Bob must not receive evidence, decisions or feedback.

Open the same history as Alice (current eligible team member), Charlie (current
later joiner), the assigned Mentor, and an Administrator through their existing
assignment pages. The original frozen Alice/Bob roster must remain visible and
unchanged. Charlie retains read-only access without gaining credit or submission
eligibility. This policy recheck remains a manual acceptance step; the five
previously passed workflow checks need not be repeated.

## First-release checkpoint: revised access policy

The four administrative account/cohort services now require an explicit caller
and current Administrator role inside their service transaction. Scoped candidate
and mentor cohort queries are unchanged. Candidate team history now requires
current assigned-team membership; frozen attribution never substitutes for it.
Navigation, direct routes, old opened forms, messages and existing policy tests
follow this rule without changing submission eligibility or mentor/admin access.

Verification on 7 November 2026:

- Focused authorization, account creation, cohort query and revised team-access
  tests: 19 passed, zero failed.
- Full integration suite, run once with serial test-class execution: 258 passed,
  zero failed, zero skipped (10 minutes 32 seconds).
- Solution build: zero warnings and errors. EF reported no pending model changes;
  `git diff --check` passed.
- Results: `TestResults/release-policy-20261107/release-policy-focused.trx` and
  `TestResults/release-policy-20261107/first-release-checkpoint.trx`.

Automated local acceptance passed. The revised-visibility browser recheck above
remains outstanding. Production provisioning is deferred; these local results do
not establish production readiness. No Development database changes or migration
applications were made, and uncertain temporary databases were left untouched.
