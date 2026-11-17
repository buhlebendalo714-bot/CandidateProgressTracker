# Local release handoff

11 November 2026 (SAST). This handoff describes the locally accepted implementation;
it is not deployment approval or production-readiness certification. The
[delivery checklist](delivery-checklist.md) remains the single current status record.
Use the [completed-deliverables test checklist](completed-deliverables-test-checklist.md)
to design tests without treating older draft policies as current requirements.

## Start the existing local installation

Prerequisites already used for acceptance: .NET 10 SDK, local SQL Express at
`.\SQLEXPRESS`, the existing `CandidateProgressTracker_Dev` database, configured
Development user secrets, and Windows Defender for real file processing. Chrome
is needed only for the opt-in browser tests. No software installation is implied.

From the repository root:

```powershell
dotnet run --project src/CandidateProgressTracker.Web --no-build --launch-profile https -- --DevelopmentAdmin:Password= --DevelopmentCandidate:Password=
```

Open `https://localhost:7231/Account/Login` (HTTP profile also listens on 5296).
`--no-build` requires an existing build. If code changed, build separately before
starting; do not repeatedly rebuild or rerun passing suites without cause. Empty
bootstrap-password overrides prevent provisioning bootstrap accounts at startup.
Use existing administrator-mediated accounts; credentials do not belong in this
handoff, source, screenshots, test output or issue reports.

Configuration keys, without secret values:

| Key | Local purpose |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | Existing SQL Express Development connection; never replace/reset the database to fix a connection failure. |
| `Evidence:Root` | Private durable filesystem root outside repository and `wwwroot`; existing local setting is `%LOCALAPPDATA%\CandidateProgressTracker\Evidence`. |
| `Authentication:RequestsPerMinute` | Authentication/recovery rate limit; default 30 per minute/IP. Isolated test hosts explicitly raise their own limit. |

The default scanner is real Windows Defender. Scanner failure/timeouts keep files
unavailable; there is no operational test-scanner fallback. The application hosts
`EvidenceWorker` outside Testing. Its durable SQL jobs and private storage must
remain together. Do not publish the storage directory or edit object/ownership
markers manually. Configuration/ACL changes require their own authorised scope.

## Find delivered workflows

| Role | Navigation / action |
| --- | --- |
| Candidate | **Overview**, **Courses & certificates**, **Projects & milestones**, **Submission history**, **Progress details**. Individual and team cards lead to requirements, evidence submission and history. |
| Mentor | **Mentor workspace**, **Review submissions**, **Learner progress**, **Course work**, **Project work**. Queue links open the latest eligible attempt. |
| Administrator | **People & access** for accounts/lifecycle; **Set up learning** for cohorts, memberships, cycles and cycle-specific Courses/Projects; **Archive & history** and **Audit history** for governance. |
| Approval reopening | Administrator cycle/project or course assignment links lead to review history / **Reopen effective approval**. This is separate from reopening an archived project/cycle. |

Access requires current roles and applicable memberships. Candidate team history
requires current assigned-team membership; frozen credit alone grants no access.
Archived candidate/mentor routes stay inaccessible. Administrator status alone
does not grant mentor decision powers. See test criteria for the detailed policies.

## Database and evidence handling

Already applied with prior explicit approval and backup verification:

| Migration | Immediate predecessor |
| --- | --- |
| `20261106192833_AddTeamSubmissionRosters` | `20261106175348_AddIndividualSubmissionReviews` |
| `20261107065418_AddTheoryCoursesAndPrivateEvidence` | `20261106192833_AddTeamSubmissionRosters` |
| `20261107083104_AddAccountLifecycleAuditAndArchives` | `20261107065418_AddTheoryCoursesAndPrivateEvidence` |

Do not reapply them or generate a migration for this documentation delivery.
Reviewed SQL, populated-upgrade and backup/restore evidence are linked from the
delivery checklist. Future Development migrations require fresh approval, target
confirmation and a recoverable backup. Never reset Development or delete existing
records. Never delete uncertain test databases by prefix.

For new automated tests, reuse `WebAccessFixture`, `TestDatabase`,
`ReviewTestContext` and existing team/file helpers. Every test database must be
uniquely named and owned by that test. Run fictional account/membership mutations,
lease expiry, concurrency and failure injection only there. Private test storage
must also be uniquely owned. Cleanup may remove only those proven resources.

## Verification baseline and limits

These are recorded prior results, not tests rerun for this handoff:

- Initial full integration run: **278 passed, 17 failed, 3 skipped / 298**. All
  failed cases subsequently had passing affected coverage; **295 non-opt-in cases**
  passed across the original and targeted runs. This was not a second clean full run.
- Lifecycle live Chrome acceptance: **1/1 passed** across the six agreed journeys.
- Course/file live Chrome acceptance: **1/1 passed**, including real Defender PDF,
  protected downloads, isolation, approval/feedback and Theory 1/1 at 100%.
- Archive/audit live Chrome acceptance: **1/1 passed**, including preservation and
  unchanged rowversions. Hosted-worker operational acceptance: **1/1 passed**.
- Earlier real Defender format/failure/stale-finalization acceptance: **3/3 passed**.
- Latest test-project build: **0 warnings/errors**. Initial shared shell/dashboard
  and evidence-editor visual framework accepted by the user; further polish deferred.

Evidence-worker restart acceptance simulated expired lease metadata; it did not
kill a process or change the system clock. Browser expiry tests used disposable
credential timestamps. No production operational guarantees follow from these tests.
Use the delivery checklist for artifact paths, earlier failures and exact scope.

SQL Express has intermittently timed out under severe local memory/paging pressure.
If startup fails, check service/resource status and one minimal read-only query;
do not repeatedly launch browser suites, reset data, alter security, or increase
timeouts to hide it. Ask before restarting SQL or terminating unrelated processes.

## Checkpoint and next decision

The recorded functional acceptance journeys are complete. New test authors can
work from the companion checklist. Deferred work remains: visual refinement,
broader accessibility/viewport/browser and performance acceptance, production
hosting/storage/scanner provisioning, monitoring/support, secrets/keys, coordinated
database-plus-evidence restore and approved retention procedures. Some draft
nonfunctional targets remain proposals requiring operational agreement.

Cancellation policy, DOCX, roster correction/reset/supersession, scorecards,
reminders and advanced reporting are not delivered features. Do not silently
turn them into acceptance expectations. Choose a separately bounded interval for
any further implementation or production work; no commit/push/deployment is implied.


## 13 November 2026 continuation

The delivery checklist now contains a direct original-proposal/Scope MVP-01?10
achieved/remaining map; the original PDF is preserved unchanged under requirements.
A focused core-form Chrome/Edge 1366px/360px keyboard/error matrix passed (1 test,
12 viewport combinations). Read the delivery checklist for exact versions, earlier
harness failures and evidence. Native file-transfer/browser POST and real Android
acceptance remain outstanding; no broader accessibility/performance/production claim.
Next bounded interval is remaining hands-on/device acceptance, then agreement on
performance workload/operational targets. No need to repeat completed discovery or
functional suites merely to regenerate a status report.
