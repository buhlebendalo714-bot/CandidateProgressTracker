# CAPACITI Integrated Candidate Progress Tracker
## 01 — Project Scope and MVP Definition

**Version:** 0.1 — working draft  
**Date:** 29 October 2026  
**Project lead:** Buhle Palaza  
**Stage:** SDLC planning  
**Status:** Proposed scope for discussion; not an approved CAPACITI implementation.

## 1. Basis and decision status

Source: *Project Proposal_Candidate Progress Tracker 1.pdf*, supplied by the user. Sections 1–8 describe the problem, roles and intended capabilities; section 9 proposes the technology stack.

Confirmed in this conversation: development starts from scratch; use the proposal's ASP.NET Core and SQL Server stack; proceed step by step through planning, requirements and design before implementation. Razor Pages versus React remains undecided.

The proposal establishes the overall product vision. Release priorities, detailed rules and recommendations in this document are proposed decisions, not requirements already confirmed by CAPACITI. The proposal's descriptions of current processes are stakeholder input, not independently validated operational findings.

## 2. Problem statement

According to the proposal, candidates complete external theory courses alongside practical projects with weekly deliverables. Evidence and deadlines are spread across platforms and folders, and verbal or high-level feedback makes progress and expectations difficult to verify.

The system should provide one place to see assigned work, deadlines, evidence, review decisions and feedback across successive learning cycles. It complements existing learning platforms and submission workflows rather than delivering course content itself.

## 3. Product objective and intended outcomes

Enable candidates and authorised staff to answer: What is assigned? When is it due? What has been submitted? What has been verified? What requires attention?

| Objective | Observable outcome |
| --- | --- |
| Make workload visible | Candidates see theory and practical deadlines together. |
| Make completion verifiable | Submitted evidence and approved work are distinguishable. |
| Make feedback actionable | Review decisions have written feedback linked to a specific submission. |
| Support early intervention | Mentors can identify overdue work and pending reviews within their assigned cohorts. |
| Preserve learning history | Previous cycles, submissions and feedback remain accessible to authorised users. |

These are intended outcomes. Claims about time saved or improved learner performance require a later pilot and baseline measurements.

## 4. Users and boundaries

| Role | Primary responsibilities | Proposed access boundary |
| --- | --- | --- |
| Candidate | View assignments; submit evidence; read feedback; resubmit when requested. | Own learning records and evidence. |
| Technical mentor / facilitator | Review evidence; record feedback; monitor progress. | Assigned cohorts and candidates. |
| Administrator | Manage accounts, roles, cohorts, cycles and assignments; oversee records. | System administration, with sensitive actions recorded. |

Whether administrators also assess evidence, whether users can hold multiple roles, and whether team members share submissions must be resolved in requirements analysis. No employer, public visitor or parent role is proposed for the MVP.

## 5. Proposed MVP scope

An MVP is the smallest usable release that demonstrates the complete learning-evidence-review workflow. It still requires access control, validation and reliable record keeping.

| ID | Capability | First-release boundary |
| --- | --- | --- |
| MVP-01 | Accounts and access | Login/logout, administrator-managed accounts and role-based access. Account recovery mechanism defined before release. |
| MVP-02 | Cohorts and membership | Create cohorts; assign candidates and mentors; preserve historical associations. |
| MVP-03 | Learning cycles and projects | Create cycles and projects with dates and weekly milestones. Four weeks is the initial example, not a hard-coded constraint. |
| MVP-04 | Theory assignments | Record course title, external platform, link and deadline; assign to candidates. No automatic platform synchronisation. |
| MVP-05 | Evidence submission | Submit course certificates and milestone files or links; retain submission timestamps and revision history. |
| MVP-06 | Review and feedback | Authorised reviewer approves evidence or requests changes with written feedback; candidate can resubmit. |
| MVP-07 | Candidate dashboard | Separate theory and project completion, upcoming deadlines, overdue work and items awaiting review. |
| MVP-08 | Mentor overview | Cohort/candidate views showing outstanding work and a review queue. |
| MVP-09 | Historical records | Archive completed projects/cycles and retain authorised access to evidence and feedback. |
| MVP-10 | Audit and quality foundations | Record key assignment, submission, review and access-management changes; validate inputs and restrict evidence access. |

Basic workload visibility means a deadline list and counts of outstanding assignments. Estimating effort or balancing workload automatically is outside this release.

## 6. Deferred proposal features and exclusions

The following proposal features are deferred, not removed from the product vision:

| Later capability | Reason for deferral |
| --- | --- |
| Presentation scheduling and scorecard capture/upload | Requires agreed assessment categories, scoring rules and visibility. |
| Proactive email or scheduled reminders | Dashboard deadline indicators support the first workflow; background delivery can follow. |
| Advanced analytics and report exports | Requires stable source data and agreed reporting questions. |
| Rich workload visualisation | Requires a definition of workload beyond assignment counts. |

Explicitly outside the proposed MVP: Coursera/iKamva or Microsoft Teams integrations, course delivery, automatic grading/code-quality assessment, attendance management, recruitment/placement management, native mobile apps, public portfolios, public registration and multi-organisation tenancy.

The system records mentor assessment; an upload or approval alone does not independently prove code quality or technical competence.

## 7. First implementation increment

Build one complete milestone workflow before expanding the product:

1. Administrator creates a cohort, assigns a mentor and candidate, and creates a project milestone with expectations and a deadline.
2. Candidate signs in and views the assignment.
3. Candidate submits evidence and receives confirmation.
4. Assigned mentor reviews it and records either approval or a request for changes.
5. Candidate sees the feedback and can resubmit if required.
6. Progress and the review queue reflect the latest decision, while earlier versions remain traceable.

This increment is a development checkpoint, not the entire MVP. Theory tracking and the other MVP capabilities follow.

## 8. Proposed operating rules to refine in the SRS

- Submission does not equal completion; approval is required for verified completion.
- Use separate theory and project percentages. Initially propose approved required assignments divided by total required assignments, multiplied by 100; show “No assignments” when the denominator is zero.
- Track deadline status separately from review status. Preserve whether evidence was submitted late even when subsequently approved; do not label an on-time submission overdue merely because review is pending.
- Allow late submissions unless a defined closure policy prohibits them; this requires confirmation.
- Require a reason when requesting changes. Preserve who reviewed which evidence version and when.
- Restrict access on the server, including direct file access; hiding buttons is insufficient.
- Keep historical evidence when archiving. Retention and deletion rules remain to be agreed; indefinite retention is not assumed.
- Changes to approved submissions, deadlines and membership require explicit rules and traceable records.

## 9. Constraints, assumptions and dependencies

**Confirmed technical constraints:** ASP.NET Core; SQL Server; browser-based interface; role-based access. Frontend choice, framework versions, hosting and evidence storage are architecture decisions still to be made.

**Working assumptions:** begin development with fictional data; use one organisation and a small demonstration cohort; manually record external-course assignments and verify evidence; administrators provision accounts initially. These are reversible planning assumptions.

**Dependencies:** representative course and milestone examples; agreement on individual/team submissions; reviewer responsibilities; later hosting and storage choices. No budget, fixed delivery date, production load or formal CAPACITI sponsorship has been supplied.

## 10. MVP success and release criteria

| ID | Evidence required |
| --- | --- |
| SC-01 | Demonstrate assignment, submission, requested changes, resubmission and approval using fictional accounts for all three roles. |
| SC-02 | Demonstrate theory certificate verification and project evidence review with independent progress totals. |
| SC-03 | Test that candidates cannot access other candidates' records or evidence, and mentors cannot access unassigned cohorts. |
| SC-04 | Verify deadline and progress calculations against agreed examples, including no assignments, late submissions and pending reviews. |
| SC-05 | Demonstrate that resubmissions and archiving preserve feedback, reviewer identity and evidence history. |
| SC-06 | Verify that invalid or unauthorised evidence uploads/downloads are rejected under the agreed upload policy. |
| SC-07 | Complete core user acceptance scenarios; resolve blocking defects; document remaining limitations. |
| SC-08 | For a hosted release, demonstrate setup, deployment smoke checks and backup/restore procedures. |

Performance, accessibility, file-size and recovery targets will be defined in the SRS and design stages rather than invented here.

## 11. Main risks and responses

| Risk | Planned response |
| --- | --- |
| Scope grows into an LMS or HR platform | Keep release priorities and exclusions explicit; assess additions against the MVP goal. |
| Individual/team assessment is misunderstood | Resolve submission ownership before database design. |
| Reviews become a bottleneck | Make pending reviews visible; define who is responsible for reviewing. |
| Dashboard overstates progress | Separate submitted from approved work and define calculations explicitly. |
| Evidence or candidate records are exposed | Design record-level permissions, protected storage and access tests. |
| External evidence links become unavailable | Label links as external evidence; decide which deliverables require retained uploads. |

## 12. Decisions needed and next deliverable

| ID | Question | Timing |
| --- | --- | --- |
| D-01 | Is the first release a portfolio/learning demo, a CAPACITI pilot, or intended for live operational use? | Before finalising scope and quality requirements. |
| D-02 | Are practical projects individual, team-based, or both? If teams, how is individual progress evidenced? | Before workflow and database design. |
| D-03 | Who may verify courses and review milestones: mentors only, or administrators too? | During SRS. |
| D-04 | Can candidates submit after deadlines; can reviewers reopen approved work? | During SRS. |
| D-05 | Is deferring scorecards, reminders and advanced reports acceptable for the MVP? | Before baselining scope. |

Next deliverable: Software Requirements Specification with numbered functional requirements, role permissions, business rules, user stories and testable acceptance criteria. Keep this scope at version 0.1 until key decisions are resolved; record agreed changes in the next version.
