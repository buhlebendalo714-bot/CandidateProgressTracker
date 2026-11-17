# CAPACITI Integrated Candidate Progress Tracker
## 02 — Software Requirements Specification

**Version:** 0.1 — requirements draft  
**Date:** 29 October 2026  
**Project lead:** Buhle Palaza  
**Basis:** Project Proposal_Candidate Progress Tracker 1.pdf and Scope and MVP Definition v0.2.  
**Stage:** Requirements analysis. This is a project-specific SRS, not a claim of certification against a formal standard.

## 1. Purpose and requirement status

Specify the behaviour and quality expected of the first operational release so architecture, implementation and tests share one reference. This document defines what the system must do; detailed database structure, frontend choice and hosting remain design work.

Confirmed decisions: build from scratch with ASP.NET Core and SQL Server; support daily operational use; support individual and team projects, with corresponding submission ownership; defer scorecards, scheduled reminders and advanced reporting. Razor Pages versus React remains open.

All detailed requirements below are the proposed interpretation of those decisions and the proposal. “Shall” expresses intended testable behaviour once this draft is baselined; it does not imply that every rule has already been approved. Explicit provisional decisions are tracked as P-01 through P-10. The user's instruction to continue is not treated as answering the team-submitter question.

## 2. System boundary and vocabulary

The system tracks assignments, evidence, mentor review and progress. External learning platforms continue to deliver courses. The MVP does not synchronise with Coursera, iKamva or Teams, grade code automatically, provide public profiles or send scheduled deadline reminders.

| Term | Meaning |
| --- | --- |
| Cohort | Group of candidates with assigned mentors. |
| Learning cycle | Dated period containing course assignments and practical projects. |
| Project mode | Individual or team; determines who owns a milestone assignment and its submissions. |
| Assignment | A specific required course or milestone allocated to a candidate or team, with a deadline. |
| Team | Project-specific group of candidates; not the entire cohort by default. |
| Submission version | Immutable evidence snapshot with its actual submitter and timestamp. |
| Review | Decision and written feedback linked to one submission version. |
| Approved | Evidence accepted by an authorised reviewer; not a numerical score or independent technical certification. |
| Archived | Historical record available for permitted reading but locked against ordinary changes. |

## 3. Proposed role-permission matrix

All permissions are checked on the server and on file downloads. Roles alone are insufficient: record ownership, cohort assignment, team membership and archive state also apply.

| Action | Candidate | Mentor | Administrator |
| --- | --- | --- | --- |
| Manage users, roles and account status | No | No | Yes |
| Manage cohorts, cycles, projects, teams and assignments | No | No | Yes |
| View private candidate records | Own | Assigned cohorts | Yes, for administration |
| Submit course evidence | Own | No | No |
| Submit individual milestone evidence | Own assignment | No | No |
| Submit team milestone evidence | Active member of assigned team, P-01 | No | No |
| View team evidence and shared feedback | Eligible team member, P-03 | Assigned cohort | Yes |
| Review course or milestone evidence | No | Assigned cohort | No by default, P-02 |
| Reopen an approved item | No | Assigned cohort, with reason | No review authority by default |
| Archive/reopen a cycle or project | No | No | Yes, with reason |
| View audit records | No general audit access | Scoped submission/review history | System audit view |

An administrator who must assess work needs a mentor role and the relevant cohort assignment under P-02. Self-review is prohibited even if an account has multiple roles. Account provisioning must prevent removal or disabling of the last active administrator.

## 4. Functional requirements

All FR requirements are proposed MVP requirements. Parenthetical MVP references map to the scope document; acceptance scenarios appear in section 9.

### 4.1 Accounts and membership

| ID | Requirement |
| --- | --- |
| FR-01 | The system shall authenticate active accounts, support logout, and reject unauthenticated access to protected pages and operations. (MVP-01) |
| FR-02 | Administrators shall create accounts with unique normalised email addresses, assign roles, correct profile details and deactivate accounts without deleting historical records. No public registration is provided. (MVP-01) |
| FR-03 | The system shall support verified account activation and password recovery using expiring, single-use credentials; passwords shall not be sent in plaintext or logged. Delivery and verification method remains a design decision. (MVP-01) |
| FR-04 | Role changes and deactivation shall invalidate existing privileged access on the next protected request; disabled users shall be unable to submit, review or download evidence. (MVP-01) |
| FR-05 | Administrators shall create cohorts and assign candidates and mentors, preserving effective membership dates and change history. (MVP-02) |
| FR-06 | Administrators shall create dated learning cycles and associate their assignments with the appropriate cohort; invalid date ranges shall be rejected. (MVP-03) |

### 4.2 Project, team and course setup

| ID | Requirement |
| --- | --- |
| FR-07 | Administrators shall create a project with a title, brief, cycle and individual/team mode. Mode shall be locked after the first submission; a replacement project is required to change it thereafter. (MVP-03) |
| FR-08 | Administrators shall create project teams and allocate eligible cohort candidates. A candidate shall belong to at most one active team per project, while being able to join teams in other projects. (MVP-03) |
| FR-09 | Administrators shall define ordered milestones with deliverable instructions, acceptance expectations, required evidence and a deadline. The system shall support configurable project length rather than hard-code four weeks. (MVP-03) |
| FR-10 | Milestone assignments shall belong either to an individual candidate or a project team, consistently with project mode. Duplicate active assignments for the same owner and milestone shall be rejected. (MVP-03) |
| FR-11 | Administrators shall create external courses with a title, platform and valid web link, and assign them to individual candidates with deadlines and evidence expectations. Courses shall never inherit team completion. (MVP-04) |
| FR-12 | Candidates shall view their assignment instructions, deadline, owner, status and evidence requirements before submission. Mentors shall see equivalent information for assigned cohorts. (MVP-04/05) |
| FR-13 | Administrators shall amend assignments or deadlines with a recorded reason. Changes shall be versioned, preserve prior values and refresh affected dashboard calculations. An assignment with evidence shall not be silently deleted. (MVP-03/10) |

### 4.3 Evidence and review

| ID | Requirement |
| --- | --- |
| FR-14 | Candidates shall submit evidence matching assignment requirements. Course verification shall require a certificate file in the MVP; project evidence may include allowed files, web links and explanatory text. (MVP-05) |
| FR-15 | A team submission shall create one team-owned evidence version, record the actual submitting member, and become visible to eligible team members and assigned reviewers. It shall not create a separate upload for every member. (MVP-05) |
| FR-16 | Every accepted submission shall record owner, assignment, version number, submitter, server timestamp and evidence metadata. Successful confirmation shall be shown only after the submission is committed. (MVP-05/10) |
| FR-17 | Submitted versions shall be immutable. When changes are requested, a resubmission shall create a new version and preserve all earlier versions and review decisions. (MVP-05/06) |
| FR-18 | Evidence files shall be subject to type, size, content and authorisation checks, private storage and safe download handling. Unsafe or unvalidated evidence shall not be made downloadable or reviewable. Limits are provisional in section 8. (MVP-05/10) |
| FR-19 | Evidence links shall accept only valid HTTP/HTTPS URLs; the application shall not automatically fetch arbitrary submitted URLs. Links shall be labelled as external. (MVP-05/10) |
| FR-20 | Assigned mentors shall have a queue of current submitted versions, filterable by cohort, project/course and review status, ordered by oldest pending submission by default. (MVP-06/08) |
| FR-21 | A reviewer shall approve or request changes on the current pending version, with a written decision note. The system shall record reviewer, timestamp and version; reviews of stale versions or self-owned evidence shall be rejected. (MVP-06) |
| FR-22 | Candidates shall see their applicable review decision, feedback and chronological submission history. Team feedback is shared; another member's private course records remain inaccessible. (MVP-06/09) |
| FR-23 | An assigned mentor shall be able to reopen an approved assignment with a reason while its project/cycle is active. Approval ceases to count toward current completion until a subsequent version is approved; the original decision remains in history. (MVP-06/10) |
| FR-24 | Concurrent submissions or reviews shall not overwrite each other. A stale action shall return a conflict and require refresh; accidental retries shall not create duplicate committed versions. (MVP-05/06) |

### 4.4 Progress, history and administration

| ID | Requirement |
| --- | --- |
| FR-25 | Candidate dashboards shall show separate theory and project completion for a selected cycle, with numerator/denominator, pending reviews, requested changes, upcoming deadlines and overdue action items. Empty assignment sets shall display “No assignments”. (MVP-07) |
| FR-26 | Mentor overviews shall show only assigned cohorts, their candidates' progress and outstanding work. “Needs attention” shall be explained by observable overdue or changes-requested items, not an unexplained risk score. (MVP-08) |
| FR-27 | Administrators shall archive projects/cycles without deleting evidence, reviews or recorded progress. Archived records shall be read-only; reopening requires an audited reason. (MVP-09) |
| FR-28 | Authorised users shall access previous cycles and submission histories according to ownership and historical membership rules. Deactivation and membership changes shall not erase provenance. (MVP-09) |
| FR-29 | The system shall record successful account/role changes, membership changes, assignment/deadline edits, submissions, reviews, reopening and archiving in protected audit records. Security logs shall capture denied access and authentication events without credentials or file contents. (MVP-10) |
| FR-30 | Administrators shall search/filter audit events by actor, action, affected record and date. Ordinary application users shall not edit or delete audit events. (MVP-10) |

## 5. Proposed business rules and state behaviour

| ID | Rule |
| --- | --- |
| BR-01 | Course assignments are individual. Milestone ownership follows project mode. |
| BR-02 | Required assignments contribute equally within their own stream; approval, not submission, contributes to completion. Cancelled assignments are excluded with the cancellation reason retained. |
| BR-03 | Completion = approved required assignments / active required assignments × 100. Round displayed percentages to one decimal place and show counts. Do not combine theory and project totals into one score. |
| BR-04 | Under P-04, team approval gives eligible members the same project milestone completion, labelled “team outcome”. It is not evidence of individual skill or contribution. |
| BR-05 | Each assignment shall retain a participant roster. Before the first submission it follows team membership; at first submission it is fixed. Later membership changes affect future unsubmitted assignments, not earlier credit automatically. Any correction to a fixed roster requires an administrator reason and audit event. |
| BR-06 | Any active team member may submit under P-01, but nobody may change another member's immutable submitted version. One current pending version exists per assignment. |
| BR-07 | Late submission is permitted on active assignments under P-05. A due date is not an automatic closure date. Archived assignments reject submissions and reviews. |
| BR-08 | Store times as instants; display deadlines in Africa/Johannesburg with an explicit date and time. A submission at the deadline is on time; one after it is late. |
| BR-09 | A version's original lateness is retained against the deadline effective when it was submitted. Later extensions change the current due date but do not silently rewrite the historical result. |
| BR-10 | An item is currently overdue when the deadline has passed and candidate action is required (Not submitted or Changes requested). Submitted items show “Awaiting review”; their version may separately show “Submitted late”. |
| BR-11 | Resubmission uses the existing deadline unless an administrator explicitly extends it. The original and resubmission timestamps remain visible. |
| BR-12 | Feedback belongs to the reviewed evidence version. Reviewer actions apply only to the latest current pending version. |
| BR-13 | A candidate may read their own historical individual records and team versions whose fixed participant roster includes them. New members do not automatically gain access to old team evidence; former members cannot make new team submissions. |
| BR-14 | Archived progress is shown as a historical outcome. Archive freezes ordinary changes; an audited reopen permits subsequent changes without erasing previous states. |

| Current state | Action and actor | Next state | Condition |
| --- | --- | --- | --- |
| Not submitted | Candidate submits | Submitted | Eligible owner/member; valid evidence; active assignment. |
| Submitted | Mentor approves | Approved | Current version; assigned reviewer; written note. |
| Submitted | Mentor requests changes | Changes requested | Current version; assigned reviewer; written note. |
| Changes requested | Candidate resubmits | Submitted | New immutable version; valid evidence. |
| Approved | Mentor reopens | Changes requested | Reason required; active project/cycle. |

No direct candidate transition to Approved is allowed. Archiving is a separate record state that disables transitions. Cancellation is an audited administrative exception; it preserves history and excludes the item from current progress. The UI shall explain denominator changes after cancellation or assignment additions.

## 6. Data and interface requirements

The design must represent accounts/roles, cohorts and dated memberships, cycles, projects and mode, teams and membership history, course/milestone definitions, owner-specific assignments, fixed participant rosters, evidence versions, file metadata, external links, reviews, deadline history and audit events. These are conceptual records, not a final table design.

Files require a private storage identifier, original display filename, verified type, size, integrity information and validation status. Store only the personal details necessary for access and learning records; do not collect identity numbers or unrelated personal information by default. Retention, deletion and audit retention periods require an operational owner decision before real-data use.

The browser interface shall provide role-appropriate navigation, form validation, clear save/submission confirmation, accessible errors, empty states and conflict recovery. SQL Server is the confirmed database. The frontend and whether browser calls require a Web API remain architecture choices. Email required for secure account activation/recovery, if selected, is distinct from the deferred scheduled-reminder feature.

## 7. User stories and acceptance scenarios

| Story | User need | Requirements |
| --- | --- | --- |
| US-01 | As an administrator, I need to provision users and organise cohorts so the right people receive assignments. | FR-01–13 |
| US-02 | As a candidate, I need to understand and submit my individual work so my learning can be reviewed. | FR-12,14,16–19 |
| US-03 | As a team member, I need to submit shared evidence and see shared feedback without exposing private records. | FR-08,10,15,22,24 |
| US-04 | As a mentor, I need to review the latest evidence and explain my decision. | FR-20–24 |
| US-05 | As a candidate or mentor, I need accurate progress and deadline information to identify the next action. | FR-25–26 |
| US-06 | As an administrator, I need historical records and auditability for daily operation and issue investigation. | FR-27–30 |

## 8. Non-functional requirements and provisional targets

Targets in this section are planning proposals, not measured results or agreed production commitments. Load and service targets must be validated against actual cohort size, infrastructure and budget.

| ID | Requirement / proposed acceptance target |
| --- | --- |
| NFR-01 Security | Enforce server-side authorisation on every protected operation and file; encrypt production transport; use established password hashing and appropriate protection against injection, cross-site scripting and request forgery. Automated tests must cover cross-user, cross-team and cross-cohort access. |
| NFR-02 Sessions | Rate-limit authentication and recovery. Proposed idle timeout: 30 minutes. Logout and account/role changes must invalidate access as specified in FR-04; exact recovery-token lifetime is a design decision. |
| NFR-03 Uploads | Proposed allow-list: PDF, PNG, JPEG and DOCX; maximum 10 MB per file and five files per version. Reject scripts, executables, ZIP archives and macro-enabled Office files. Validate content as well as extension and quarantine pending security checks. Review limits with actual deliverables before implementation. |
| NFR-04 Integrity | Submission/review commits must be atomic at the record level. Storage failure must not expose a successful submission with missing evidence. Recovery/cleanup must handle orphaned staged uploads. No silent overwrite under concurrency. |
| NFR-05 Performance | Initial test envelope: 500 active candidates, 50 concurrent users and 10,000 assignment records. For ordinary reads and saves excluding file transfer, p95 server response ≤2 seconds under a documented representative 15-minute mixed workload, with <1% unexpected errors. Reassess once actual usage is known. |
| NFR-06 Usability | Core flows must work at 360 px and 1366 px viewport widths, using keyboard navigation, visible focus, labelled fields, readable errors and non-colour-only statuses. Check candidate submission and mentor review manually as well as automatically. |
| NFR-07 Compatibility | Verify core workflows in the stable desktop Chrome and Edge versions selected at test time and one representative Android Chrome device. Record tested versions in the test report. |
| NFR-08 Recovery | Proposed recovery point objective: ≤24 hours of data loss; recovery time objective: ≤4 hours. Back up database and evidence consistently, protect backup access and demonstrate a full restore before go-live. Validate objectives and backup retention with the operational owner. |
| NFR-09 Availability | Proposed availability target: 99.5% during agreed service hours, excluding agreed maintenance. Service hours and measurement method remain open; do not promise this target before hosting/support decisions. |
| NFR-10 Observability | Record correlated errors without secrets, expose an operational health check, and route service/storage failures to a named operator. Establish log retention and incident handling before go-live. |
| NFR-11 Maintainability | Keep business rules testable, version database migrations, document local setup and deployment, and run relevant build/tests in CI. Use fictional test data and keep secrets outside source control. |
| NFR-12 Privacy and retention | Define approved data access, retention and deletion procedures before live-data onboarding. The MVP shall support account deactivation and protected historical records without assuming indefinite retention. This draft makes no legal-compliance certification. |

## 9. Acceptance checks and initial traceability

These are initial acceptance checks, not a completed execution report or exhaustive test suite. Each check requires later test data and execution evidence.

| ID | Given / when / then | Coverage |
| --- | --- | --- |
| AC-01 | Given an active provisioned account, correct login grants its permitted view; incorrect login does not. Logout or deactivation prevents the next protected request. Used/expired recovery credentials cannot be reused. | FR-01–04; NFR-01/02 |
| AC-02 | Given a cohort and dated cycle, an administrator creates both project modes and assigns courses. Invalid date ranges and duplicate owner assignments are rejected. | FR-05–13 |
| AC-03 | Given an individual milestone, its owner submits valid evidence and receives one committed version. Another candidate cannot view, alter or download it by changing record identifiers. | FR-12,14,16,18; NFR-01 |
| AC-04 | Given a team assignment, an active member submits; eligible members see the same version and named submitter. A non-member is denied. No other member's course certificate becomes visible. | FR-08,10,15,22; BR-01/06/13 |
| AC-05 | Given a pending version, an assigned mentor requests changes with feedback. A resubmission creates v2, retains v1 and its feedback, and approval of v2 updates progress. | FR-17,20–22; BR-12 |
| AC-06 | Given two reviewers opening the same pending version, the first valid decision commits; the second is rejected as stale. Two simultaneous team submissions cannot silently replace one another or create duplicate current versions. | FR-21,24; NFR-04 |
| AC-07 | Given one approved and one pending theory assignment, theory displays 1/2 (50.0%). Given one approved of four project assignments, project displays 1/4 (25.0%). A stream with none shows “No assignments”. Team approval is labelled as a team outcome. | FR-25–26; BR-02–04 |
| AC-08 | Given deadline 17:00 SAST, a submission at 17:00 is on time and one after is late. A pending on-time review is not shown as overdue candidate action. Unsubmitted work after 17:00 is overdue. | FR-16,25; BR-07–11 |
| AC-09 | Given a team version with a fixed participant roster, a new member does not inherit historical credit/access automatically; a departing member loses submission rights but retains eligible historical reading. A roster correction records its reason. | FR-08,15,28; BR-04/05/13 |
| AC-10 | Given approved work, an authorised mentor reopens it with a reason; progress decreases, history remains, and new approval requires resubmission. An archived assignment blocks the same action. | FR-23,27; BR-14 |
| AC-11 | Given disallowed, oversized or unsafe evidence, submission is rejected or held unavailable pending checks. A storage failure does not yield successful committed evidence. An unprivileged direct download is denied. | FR-18–19; NFR-01/03/04 |
| AC-12 | Given a completed cycle, archive preserves evidence and feedback for authorised readers. An administrator can find actor/time/reason for a deadline edit; ordinary roles cannot modify audit records. | FR-13,27–30 |
| AC-13 | Given a normal operating dataset and its backups, restore database and evidence to a clean environment and verify accounts, assignments, evidence access and review history within agreed recovery targets. | NFR-08/10 |
| AC-14 | Execute the declared load, viewport, keyboard and browser checks; record observed results and defects against the agreed targets. | NFR-05–07 |

## 10. Provisional decisions and open operational questions

| ID | Proposed treatment / question | Must resolve by |
| --- | --- | --- |
| P-01 | Any active team member may submit; record the actor. A designated team leader restriction has not been selected by the user. | Workflow baseline. |
| P-02 | Mentors assess evidence; administrators assess only with an explicit mentor role and cohort assignment. Self-review denied. | Permission baseline. |
| P-03 | Historical team evidence access follows the fixed participant roster; current membership controls new submission rights. | Workflow/database design. |
| P-04 | Team approval updates members' project completion as a shared outcome; no individual contribution score in MVP. | Progress-rule baseline. |
| P-05 | Permit late submissions on active assignments; allow reasoned mentor reopening. | Workflow baseline. |
| P-06 | Use proposed upload formats/limits; confirm whether additional document types are necessary. | Evidence-storage design. |
| P-07 | Validate expected hubs, cohorts, candidates, concurrency and data volume against the provisional load envelope. | Architecture sizing. |
| P-08 | Identify deployment owner, operator, service hours, budget and required backup/retention periods. | Operational design/go-live planning. |
| P-09 | Confirm activation/recovery identity-verification and delivery method, including support responsibility. | Authentication design. |
| P-10 | Confirm how reassignment, withdrawals, roster corrections and cancelled requirements affect historical and current progress. | Workflow/database design. |

Unresolved capacity or operator details do not prevent requirements drafting. They do prevent claiming production readiness or finalising service commitments.

## 11. Release boundaries and completion criteria

Retain the confirmed future backlog: **FUT-01 scorecards**, **FUT-02 scheduled reminders**, **FUT-03 advanced reporting**. None is cancelled. Basic progress views and deadline indicators remain in the MVP. Presentation scheduling and rich workload visualisation remain proposed later work.

Requirements are ready for implementation when workflow/permission questions P-01 through P-05 and P-10 are resolved or explicitly baselined as assumptions, acceptance checks are reviewed, and architecture-impacting uncertainties have owners. Architecture may explore options before that baseline, but must not silently convert assumptions into confirmed requirements.

Operational release requires passing agreed acceptance and access-control checks; no unresolved blocking defects; documented limitations; tested backup restoration; monitoring and account recovery; an identified operator; and user acceptance of real workflows. No development, deployment or test execution is claimed by this document.

**Next deliverable:** user journeys and workflow/state diagrams for individual work, team work, theory verification and mentor review. Then architecture and database design will use this requirements baseline.
