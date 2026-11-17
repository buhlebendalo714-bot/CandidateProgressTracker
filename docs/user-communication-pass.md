# User communication pass ? 17 November 2026

This is a source-review and evidence map. Current delivery status remains in
[delivery-checklist.md](delivery-checklist.md). No layout redesign, policy, schema,
authorization, audit-history rewrite or Development test-data changes are included.

## Source coverage

All application-owned Razor page/shared-view files listed below were included in
an inventory of headings, instructions, labels, buttons, status/empty-state text,
validation summaries and technical/development wording. Corresponding PageModel
outcomes and Infrastructure service-result messages were inspected for the affected
account, onboarding, assignment, team, evidence, review and archive workflows.
Existing clear wording was retained. This does not mean every conditional branch
was visited in a browser.

| Area | Communication changes / retained consequences |
| --- | --- |
| Accounts | Action-specific outcomes, activation/reset code names, recipient/expiry/next step, one-time private handoff; sign-out and previous-code invalidation explained; code inputs stay masked and cleared. |
| Cohorts/onboarding | Review candidates; create/add existing/already enrolled/needs attention labels; file-level errors omit Row 0; guidance follows actual confirmation state; explicit saved partial work and resume. |
| Setup | Specific course catalogue/cycle/assignment success messages; clear cohort/member outcomes; existing field limits/reasons preserved. |
| Team evidence/history | Included members and submitted-by person distinguished; shared credit is not proof of individual contribution; meaningful confirmation kept; shorter action; receipt/revision references in details. |
| Files/downloads | Friendly 5-file/10 MB limits with unchanged exact validation; saved versus checking/rejected/expired states; truthful retry guidance; unavailable downloads retain HTTP 404 and no-store. |
| Review/reopening | Approved versus changes-requested outcomes; approval-reopening message; raw decision labels humanized; references remain available in details. |
| Progress | Approval and shared-credit explanations in ordinary language; totals/denominators/policies unchanged. |
| Archives | Explicit archive/reopen outcome; retention/access/unchanged approval consequences visible. |
| Audit | Existing actions mapped at read time; current account names and recorded reasons where available; metadata-only code lookup; original action/record IDs/source retained in details; existing filters and event storage unchanged. |
| Shared/error/privacy | Semantic status/alert roles and atomic notices; broken separators repaired; development error instructions and privacy placeholder removed without inventing an approved policy. |

## Representative before / after

- `Account updated` after recovery ? `Password reset code created` with the recipient,
  single-use code, expiry and private-delivery instructions in one panel.
- `Proposed changes / EnrolExisting / Conflict` ? `Review candidates / Add existing
  candidate / Needs attention`.
- `Confirm credited roster and submit evidence` ? `Confirm members and submit`,
  beside the included-member list and shared-credit consequence.
- `Status: Committed` ? `Submitted ? awaiting review`; checking files never claims
  that the submission is saved.
- `AccountCredential Added` ? `Password reset code created` when the recorded code
  kind is Recovery; original stored metadata remains available in details.

## Verification scope and limits

Build and focused run results, actual browser outcomes and final site availability
are recorded in the delivery checklist after completion. Browser review targets
recovery (code removed before the snapshot bridge), empty bulk upload, team submit,
processing and audit, at 1366px/360px using authenticated disposable Razor responses.
This is response replay for representative communication/reflow review, not a new
live end-to-end account/file acceptance claim. Screenshots must contain no codes.

Not covered by representative browser checks: every setup/archive/history variant,
native OS picker or spreadsheet opening, physical Android, assistive-technology
certification, production failures or every framework/Identity-generated validation
message. Service access/state rules remain protected by focused existing regressions.
Third-party minified vendor-library wording and generated migration/designer code
are outside this application-copy pass. Original proposal/requirements documents
and historical audit events are not edited as product copy.

## Razor source inventory

- `Account/AccessDenied.cshtml`
- `Account/CompleteCredential.cshtml`
- `Account/Login.cshtml`
- `Account/Logout.cshtml`
- `Admin/Archives.cshtml`
- `Admin/Audit.cshtml`
- `Admin/Cohorts/Archive.cshtml`
- `Admin/Cohorts/BulkOnboard.cshtml`
- `Admin/Cohorts/Create.cshtml`
- `Admin/Cohorts/Cycles.cshtml`
- `Admin/Cohorts/EndMembership.cshtml`
- `Admin/Cohorts/Index.cshtml`
- `Admin/Cohorts/Members.cshtml`
- `Admin/Cohorts/OnboardingBatch.cshtml`
- `Admin/Courses/Index.cshtml`
- `Admin/Index.cshtml`
- `Admin/Projects/AssignmentHistory.cshtml`
- `Admin/Projects/Assignments.cshtml`
- `Admin/Projects/Index.cshtml`
- `Admin/Projects/Milestones.cshtml`
- `Admin/Projects/ReopenAssignment.cshtml`
- `Admin/Projects/ReviseAssignment.cshtml`
- `Admin/Projects/Teams.cshtml`
- `Admin/SubmissionHistory.cshtml`
- `Admin/Users/Create.cshtml`
- `Admin/Users/Index.cshtml`
- `Admin/Users/Manage.cshtml`
- `Candidate/Assignments.cshtml`
- `Candidate/Courses.cshtml`
- `Candidate/CreditedTeamHistory.cshtml`
- `Candidate/Cycles.cshtml`
- `Candidate/Index.cshtml`
- `Candidate/Processing.cshtml`
- `Candidate/Progress.cshtml`
- `Candidate/SubmissionHistory.cshtml`
- `Candidate/Submit.cshtml`
- `Candidate/TeamSubmissionHistory.cshtml`
- `Candidate/TeamSubmit.cshtml`
- `Error.cshtml`
- `Evidence/Download.cshtml`
- `Index.cshtml`
- `Mentor/Assignments.cshtml`
- `Mentor/Cohort.cshtml`
- `Mentor/Courses.cshtml`
- `Mentor/Cycles.cshtml`
- `Mentor/Index.cshtml`
- `Mentor/Progress.cshtml`
- `Mentor/ReviewQueue.cshtml`
- `Mentor/ReviewSubmission.cshtml`
- `Mentor/SubmissionHistory.cshtml`
- `Privacy.cshtml`
- `Shared/_AppNavigation.cshtml`
- `Shared/_CredentialReceipt.cshtml`
- `Shared/_EvidenceLinks.cshtml`
- `Shared/_Layout.cshtml`
- `Shared/_LearningCycleList.cshtml`
- `Shared/_LoginPartial.cshtml`
- `Shared/_MemberAssignments.cshtml`
- `Shared/_ProgressTotals.cshtml`
- `Shared/_ReviewEvents.cshtml`
- `Shared/_SavedRequirements.cshtml`
- `Shared/_SubmissionHistory.cshtml`
- `Shared/_ValidationScriptsPartial.cshtml`
- `_ViewImports.cshtml`
- `_ViewStart.cshtml`
