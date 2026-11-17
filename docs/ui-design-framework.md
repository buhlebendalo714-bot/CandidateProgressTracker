# CAPACITI Structured Workspace — UI specification

12 November 2026 (SAST). **Approved for production UI implementation by the user.**
The standalone previews remain design references. Production implementation reuses
existing authorised data, forms and routes; no new workflow or schema is approved.
Implementation and actual verification evidence are recorded in the delivery checklist.

## Baseline and references

The [delivery checklist](delivery-checklist.md) is the current functional status;
[completed deliverables D01–D16](completed-deliverables-test-checklist.md) define
the implemented test baseline. Later conversation decisions supersede older SRS,
screen drafts and the 8 November handover's now-completed acceptance tasks.

| Reference inspected | Pattern adopted | Deliberately outside this design |
| --- | --- | --- |
| [TalentLMS redesign guide](https://help.talentlms.com/hc/en-us/articles/14758084707228-TalentLMS-redesign-guide) | Persistent left navigation, grouped by role and user goal; compact workspace/account bar. | No copied branding, course store, notifications, calendar, widgets, role-switcher or unsupported global search. |
| [Canvas SpeedGrader guide](https://community.instructure.com/en/kb/articles/662775-what-is-speedgrader) | Bring submitted evidence, exact assignment context and decision into one readable review workspace; keep attempt history nearby. | No scores, rubric, annotation/PDF viewer, inbox or new grading functionality. Existing protected file download remains the action. |
| [Moodle assignment workflow](https://docs.moodle.org/503/en/Using_Assignment) | Understandable course/activity → requirements → submission → feedback path; clearly separate submission from review. | Do not adopt Moodle's draft, marking-release, gradebook or reopening policies. Our durable Processing and administrator reopening rules govern. |

These are references, not integrations/dependencies or copied screen layouts.
CAPACITI tokens were previously verified from its [live style asset](https://capaciti.org.za/wp-content/uploads/elementor/css/post-9.css),
as recorded in the delivery checklist. They are observed website tokens, not a
claim of a published official brand manual. No third-party fonts/logo assets required.

## Reviewable previews

Open the [preview index](ui-previews/index.html), then resize either page:

- [Candidate assignment/submission](ui-previews/candidate-assignment.html).
- [Mentor review](ui-previews/mentor-review.html).
- Screenshots: [Candidate desktop](ui-previews/candidate-desktop.png),
  [Candidate mobile](ui-previews/candidate-mobile.png),
  [Mentor desktop](ui-previews/mentor-desktop.png),
  [Mentor mobile](ui-previews/mentor-mobile.png).

All people, content, dates, file names/sizes and receipts are illustrative. A separate
preview toolbar selects alternate fixture states; it is **not proposed product UI**.
Forms make no requests/uploads/decisions. Route links display their existing target;
they do not navigate to live accounts. Shared local CSS/JS require no internet.
Screenshots are design artifacts, not authenticated functional acceptance evidence.

### Preview verification actually performed

Standalone installed Chrome **157.0.8103.39**, file URLs only, at **1440×1000** and
**390×1000** for both pages. Four full-page PNGs rendered and visually inspected;
desktop exports use a taller artboard to include the full form/sidebar. Checked
page overflow, explicit field labels, one visible primary action, 3px keyboard
focus, mobile menu focus/inert background/Escape restoration, state-control
visibility, removable link-row value retention and identical saved-revision text
between Candidate/Mentor fixtures. Mobile panel/input alignment and review content
order were corrected after initial visual inspection. Existing nodes move with the
responsive layout so mobile DOM/keyboard order follows the displayed sections.

Contrast calculations: primary/white **14.11:1**, main text/page **13.91:1**,
muted/white **6.09:1**, success **6.75:1**, warning **6.79:1**, error **6.83:1**.
No preview runtime errors or external network requests observed. Evidence:
[preview-checks.json](ui-previews/preview-checks.json). These checks do not certify
WCAG, screen-reader behavior, all targets/viewports, or authenticated workflows.
360px/1366px and broader device checks remain later implementation acceptance.

Re-render only when preview changes justify it:
`node docs/ui-previews/render-previews.cjs` (existing Node/Chrome required; no install).
No build, functional test suite, migration, database operation or live app page
change was performed for this design interval.

## Shared design system

Use existing Razor Pages/Bootstrap and refine the approved shell. Content is
predominantly white on light grey; navy is confined to navigation, primary actions,
links and focus. Coral is a small decorative accent. No marketing hero, gradient,
unnecessary animation, decorative metric or grid of shortcut cards.

| Token / rule | Specification |
| --- | --- |
| Primary / accent | `--brand-primary: #1D2951`; `--brand-accent: #F25251`. Do not use white text on coral (insufficient normal-text contrast). |
| Page / surface | `#F5F6F8` / `#FFFFFF`. |
| Text / secondary text | `#23272B` / `#5C636A`; no faint placeholder-only labels. |
| Borders / controls | `#D9DEE5` for decorative separators; `#767D87` for inputs/interactive boundaries. |
| Semantic status | Success `#226043` on `#EDF6F0`; warning `#745017` on `#FFF7E8`; error `#9E2633` on `#FFF0F1`. Always include explicit text. |
| Font | Existing system stack: system-ui, Segoe UI, sans-serif. No font service. Body 15–16px; readable long instructions at 1.6–1.75 line height. |
| Type hierarchy | Heading 30px desktop/25px mobile; section 18px; field/body 13–15px; metadata 11–12px; uppercase eyebrow 10–11px with restrained tracking. |
| Spacing | Base 4px; section gaps 24–32px; controls 8–12px internal padding; panels 24px desktop/20px mobile. |
| Shape / shadow | Inputs/buttons 5–6px radius, panels 8px; thin borders. No shadow on ordinary content; restrained shadow only for overlays. |
| Keyboard | 3px navy focus ring + 3px offset; white focus ring inside navy sidebar. Skip link, logical DOM order, visible input labels and inline errors. |
| Targets | Product actions/controls at least 44px high. No hover-only controls. Long URLs, file names, receipts and names wrap without page overflow. |

Desktop: 232–248px persistent sidebar; 64–68px compact top bar; main content max
1280px including 24–36px side padding. Two columns only when useful: 2:1 or flexible
evidence plus 292–350px context. Sidebar is the only large navy surface.

At ≤900px, sidebar becomes an off-canvas menu: labelled button, focus moved into
the menu, background inert, trapped Tab, Escape/backdrop/close dismissal and focus
restoration. At ≤680px use one column with 16px page gutters; no sticky overlay
covering form controls or validation. Validate at 360/390px and 1366/1440px during
later implementation; preview rendering is narrower evidence, not WCAG certification.

## Role navigation mapped to existing routes

Labels may improve; routes, query parameters and generated identifiers must not
change. `{id}`/`{assignmentId}` etc. denote existing model-bound identifiers.
Context links use existing Razor tag helpers rather than constructing guessed URLs.

| Role / group | Label | Existing route / target |
| --- | --- | --- |
| Candidate / Learning | Overview | `/Candidate` |
| | Courses & certificates | `/Candidate/Courses` |
| | Projects & milestones | `/Candidate/Assignments` |
| Candidate / Your record | Submission history | `/Candidate#submission-history`, then existing assignment history links |
| | Progress details | `/Candidate/Progress` (preserve selected `cycleId`) |
| | Learning cycles | `/Candidate#learning-context`, contextual `/Candidate/Cycles/{id}` |
| Mentor / Mentoring | Mentor workspace | `/Mentor` |
| | Review submissions | `/Mentor/ReviewQueue` (preserve existing filters/page) |
| | Learner progress | `/Mentor/Progress` |
| Mentor / Assigned work | Course work / Project work | `/Mentor/Courses`, `/Mentor/Assignments` |
| Mentor / Context | Cohort/cycle scope | `/Mentor/Cohort/{id}`, `/Mentor/Cycles/{id}` |
| Administrator / People | People & access | `/Admin/Users` → existing Create/Manage pages and bound user identifiers |
| Administrator / Learning | Set up learning | `/Admin/Cohorts` → Members/Cycles → cycle-specific Courses/Projects |
| Administrator / Governance | Archive & history | `/Admin/Archives`, contextual `/Admin/SubmissionHistory?assignmentId={assignmentId}` |
| | Audit history | `/Admin/Audit` with Actor/Action/Record/From/To/PageNumber |
| Administrator / Overview | Administration | `/Admin` with existing cohort selector and real recent activity |
| All | Sign out | Existing POST `/Account/Logout` with antiforgery; no new account-settings feature |

Multi-role accounts retain only their actual currently authorized destinations;
group navigation rather than inventing a role-switch session. Hiding links never
replaces service/page authorization. Candidate/mentor menus have no administrative
management or archive destination.

## Page templates across implemented MVP scope

These are layout specifications for subsequent approved intervals, not newly built
pages. All keep current form names/limits, GET/POST handlers, route filters,
antiforgery, concurrency tokens, request IDs and authorization.

| Page family and actual pages | Desktop organization | Mobile organization / main action |
| --- | --- | --- |
| Login / credential completion (`/Account/Login`, `/Account/CompleteCredential`, AccessDenied) | Quiet narrow form, clear title and generic failure message; secret fields masked, no disclosures. | Single column; errors above fields and at fields; one Log in / set-password action; existing expiry/single-use behavior. |
| Role overviews (`/Candidate`, `/Mentor`, `/Admin`) | Retain approved hierarchy: Candidate Theory/Projects/actionable work/recent feedback; Mentor oldest eligible reviews and scoped progress; Admin People/Learning/Governance and real activity. | Same priorities stacked, not many independent shortcut cards. No new backend summaries. |
| Candidate cycles/courses/assignments (`Cycles/{id}`, `Courses`, `Assignments`) | Context and current cycle above compact list; each owner/milestone has deadline, readable workflow status, submission/history links. External courses show platform plus certificate requirement. | Stacked rows with clear primary eligible action; separate external course link from certificate/history. No unsupported course-content player. |
| Candidate submission (`Submit/{assignmentId}`, `TeamSubmit/{assignmentId}`) | Requirements/form in main column; deadline/owner and prior feedback in context rail; history stays one link away. See detailed preview below. | Deadline and useful feedback precede requirements/form; history follows. Preserve values/validation; never duplicate a live form across layouts. |
| Processing (`Processing/{attemptId}`) | Plain status, message, processing ID, existing refresh link and return to relevant assignments/courses. | Status first, refresh/return actions; no fake percent, guaranteed ETA or committed receipt. |
| History (`SubmissionHistory/{assignmentId}`, `TeamSubmissionHistory/{assignmentId}`, Mentor equivalent, Admin `SubmissionHistory`) | Newest attempt sections with receipt, original revision/deadline, timing, evidence and append-only decisions. 20/page; frozen roster shown once with actual submitter per attempt. | Stack metadata/decisions, wrap identifiers, keep pagination explicit. Preserve role-specific access; no privileged details on denial. |
| Candidate team-history index (`/Candidate/CreditedTeamHistory`) | Existing currently authorized shared-history entries only; label credit and current action eligibility separately. | Same authorized list. Despite legacy route name, no credit-only access or former-member navigation. |
| Mentor cohort/cycle/course/project lists | Existing scope/filter context first; compact rows lead to review/history; no candidate management controls. | Filters above list with persistent selected values; no cross-cohort shortcuts. |
| Mentor review queue (`/Mentor/ReviewQueue`) | Existing cohort/cycle/project/course/status filters, oldest eligible pending work, 20/page; ChangesRequested separate. | Accessible stacked filters and compact work rows; each eligible row has Review latest attempt. No invented bulk review / queue expansion. |
| Mentor review (`ReviewSubmission/{assignmentId}`) | Evidence/explanation/earlier feedback left; exact saved requirements/deadline and decision right. No inline PDF annotator. | Evidence → saved requirements → decision → history. Anchors make the long page navigable. Timing and review outcome remain separate. |
| Admin accounts (`Users`, `Users/Create`, `Users/Manage`) | Search/list where already available; management form grouped profile/roles/lifecycle; sensitive actions visibly distinct; single-use receipt in existing protected flow. | Labelled stacked fields and explicit action/reason; do not add confirmation workflows beyond approved behavior. Last-admin protection unchanged. |
| Admin cohorts/memberships (`Cohorts`, Create, `Members/{id}`, `EndMembership/{cohortId}/{membershipId}`, `Archive/{id}`) | Cohort context and readable member/date table; creation/ending/archive forms retain reason and dated history. | Tables scroll in their own labelled region or become semantically equivalent rows; no page-wide overflow; preserve current effective dates. |
| Admin learning (`Cohorts/Cycles/{id}`, `Projects/{cycleId}`, `Teams/{projectId}`, `Milestones/{projectId}`, `Assignments/{milestoneId}`, `Courses/{cycleId}`) | Contextual directory and table, one relevant create/assign action, related management links secondary. Group existing multi-action course forms clearly by catalogue/offering/assignment. | Select context → list → existing form. No generic card wall, new bulk import, roster reset or scorecard. |
| Requirements (`Projects/ReviseAssignment/{assignmentId}`, `AssignmentHistory/{assignmentId}`) | Current form, reason and original revision history; current vs saved labels explicit. | Inputs then history; conflict retains text and offers refresh. Approval impact explained from existing state, not guessed from revision number. |
| Approval reopening (`Projects/ReopenAssignment/{assignmentId}`) | Effective approval/history, bounded reason, administrator-only reopen action when eligible. | Same order; earlier decisions remain visible. Scope reopening is a different action, not combined. |
| Archives / audit (`/Admin/Archives`, `/Admin/Audit`) | Scope context, retained outcome/history, reasoned archive/reopen; audit filters above table, 20/page. Existing historic typed records remain distinguished. | Filters stack; meaningful columns retained through scroll region; reasons/wrapped record IDs readable. No audit editing/search expansion. |
| Privacy / error (`/Privacy`, `/Error`, `/Account/AccessDenied`) | Narrow readable page with existing return path, generic safe errors. | Same content; no exception details, storage paths or inaccessible record data. |

## Candidate assignment/submission preview

Default illustrative state: individual project, current revision 3, earlier attempt 1
ChangesRequested, new attempt required. Desktop header gives project/milestone/title,
review state and revision. The context rail shows SAST deadline, owner, cycle and
prior written feedback; avoid duplicating those fields into several cards.

Requirements contains instructions, acceptance expectations and evidence policy.
The submission panel has explanation, real-looking but inert file chooser, and the
approved one-row evidence editor: Label + HTTPS URL, add up to ten, removable rows
with retained values. Only Submit evidence is primary; full history is secondary.
Project copy allows files/links/both; course variant requires an actual certificate.
Use exact approved byte limits, not ambiguous rounded MB language.

Mobile prioritizes deadline/owner, actionable feedback, requirements and the form;
history follows. No fixed action bar hides validation. Preview fixture selectors
illustrate validation, Processing, effective/historical/reopened approval, stale
requirements, a later team joiner, and safe denial. These are **alternate examples**,
not client-side authorization or a claim that any POST was accepted.

Team variant, after approval, must show authoritative pre-freeze roster confirmation
or immutable credited roster, actual submitter and current eligibility separately.
No editable credit selector. A later current joiner can read but cannot submit;
former members cannot read on historical credit alone. No eligible credited member
means a clear blocked action, not automatic membership restoration or roster reset.

## Mentor review preview

Default illustrative state: latest committed Attempt 2, Naledi's individual assignment,
submitted revision 3, validated evidence, current scoped Mentor eligible to decide.
Attempt number, server receipt/SAST time and timing are near the evidence. Download
uses `/Evidence/Download/{fileId}` in implementation; external links remain HTTPS-only.
Explanation and prior feedback are readable, not squeezed into management tables.

Saved requirements panel **always** identifies the submitted revision and its original
deadline. No latest-revision substitution. Decision selector retains Approved /
ChangesRequested; feedback optional for approval, nonblank for ChangesRequested,
10,000-character bound; one Record decision action. No score or approve-all action.

On mobile, evidence comes first, then original requirements, then decision; in-page
anchors preserve orientation. An existing pending-review form may only display if
the service says eligible. Review details/token must continue to be read consistently.

## Required state and policy behavior

| State | Presentation / action contract |
| --- | --- |
| Empty | Honest No assignments / No attempts / No submissions match filters; only already-supported setup or navigation actions. No fabricated progress. |
| Loading / submission in flight | Disable repeat activation while that request is in flight; descriptive status text. Preserve request IDs. UI state does not replace server idempotency. |
| Validation error | Summary + linked field message, `aria-invalid` where applicable; focus summary/first invalid control; keep explanation/link rows and values. Browser cannot restore selected files after navigation: clearly ask reselect when required; never claim file retention without durable storage. |
| Processing | Payload durable, not yet an accepted attempt. Files neither downloadable nor reviewable. Show existing status/message and refresh, not a progress percentage/approval. |
| Rejected / scanner or storage unavailable | No false success/receipt. Use actual service guidance for scheduled retry vs refresh/new request; keep unsafe files unavailable. No scanner bypass button. |
| Committed / awaiting review | Show original receipt, attempt number, exact revision and SAST receipt time. Pending review is not overdue candidate work. |
| ChangesRequested | Written feedback linked to reviewed attempt; candidate new-attempt action when eligible; no further mentor decision until fresh attempt. |
| Effective approval | Clear Approved state; new submissions/decisions blocked except authorized accepted retries. Deadline-only revisions preserve approval. |
| Historical approval | Explain intervening requirement change; retain original approval/requirements/deadline. Change-then-revert never silently restores effectiveness. Fresh attempt/approval required. |
| Reopened approval | Show append-only administrator event and reason; original decision retained. New submission or eligible latest-attempt reconsideration follows existing policy; no bypass of ChangesRequested. Roster unchanged. |
| Stale form | Preserve entered text, explain conflicting change/new attempt, provide refresh; do not overwrite saved work or silently rebind to newest revision. Current accepted retries stay idempotent. |
| Self-review / missing evidence | No decision controls. Explain relevant eligibility without exposing unauthorized records; actual submitter and all frozen credited members prohibited, including dual-role/former members. |
| Current team joiner | Read-only shared history; explicitly no frozen credit/submission right. Candidate current role/cohort/team access still required. |
| Denied / archived | Generic unavailable/denied page; no candidate name, title, evidence, approval or feedback disclosure. Candidate/Mentor archives stay inaccessible; Admin has separate authorized history. |

Late/on-time is **submission timing**, calculated using original received time and
referenced original deadline. It is never an approval/review label. Exactly-at-deadline
is on time. File durability time and existing link-only timing rules remain unchanged.

## Subsequent implementation intervals — approval required

| Bounded interval | Scope | Acceptance criteria |
| --- | --- | --- |
| 1. Candidate assignment journey | Apply approved tokens/layout to existing course/project list → requirements → individual/team submission → Processing → history. Keep accepted landing dashboard behavior. | Role/service authorization, exact revision/receipt/timing, frozen credit, value/error retention and file safety unchanged; affected tests only; actual authenticated desktop/mobile and keyboard review before stop. |
| 2. Mentor decision journey | Existing queue, scoped work and review/history layout; no new queue/backend features. | Current scope/self-review/latest-attempt rules, matching details/token, stale/idempotent decisions and feedback bounds preserved; no annotation/score feature; readable mobile evidence/requirements/decision flow; stop for review. |
| 3. Administrator management/governance | Group existing People/Learning/Governance pages using shared list/form/table patterns; split further if needed. | No account lifecycle, dated membership, archive/reopening, audit search or migration behavior change; destructive/admin actions remain distinct and protected; affected checks and responsive review; stop. |

This specification does not authorize those intervals yet. D01–D16 remain the
functional regression contract. No cancellation, reminders, scorecards, advanced
reporting, DOCX, roster correction, former-member access, production setup or database
changes enter the redesign. Stop for review after implementation and verification.

## CAPACITI branding update ? 12 November 2026

The supplied `CAPACITI-Logo.webp` replaces the sidebar dot/text heading. Its
original bytes, aspect ratio and colours are retained on white; accessible name
CAPACITI and the existing role dashboard link are preserved. Shared application
name, footer and browser-title suffix now use CAPACITI; internal names are unchanged.

Candidate, Mentor and Administrator landing workspaces use a static navy-to-indigo
CSS gradient across the entire growing content area, with restrained coral and
pale diagonal accents at the edges. Indigo is a decorative reference colour, not
an assertion of an official brand token. White panels, controls and tables retain
dark text. Exterior headings/breadcrumbs use light text; decoration adds no DOM
interaction layer. Other pages retain their approved light workspace backgrounds.

Actual verification and screenshots are recorded in the delivery checklist.

## Dashboard glass and watermark refinement ? 12 November 2026

Later user decisions replace opaque dashboard cards with restrained frost: 90%
white, 14px backdrop blur, 12px radius, fine white border and soft shadow; full-opacity
navy text. Forms/controls stay at least 98% white, table backgrounds transparent.
Top bar shares the frost; logo area/sidebar remain unchanged. Opaque white defaults
provide fallback when backdrop filtering is unavailable. Purple and accents soften.

The official logo also appears as a decorative fixed-centre background watermark,
80% of visible workspace width at 8% opacity, natural aspect ratio, no pointer
interaction/animation and mobile left offset zero. Keep it behind content panels;
its appearance through the frost is intentionally faint. Do not change sidebar logo
or workflow/layout to implement this treatment. Current evidence is in the delivery
checklist and `TestResults/dashboard-branding/`.


## Overview background update ? 16 November 2026

User requested removal of the decorative Overview theme across all roles. Use the
shared light-grey workspace and opaque white panels; remove gradient/geometric
backgrounds, watermark and dashboard frost overrides. Retain CAPACITI branding,
sidebar navigation and existing layouts. This supersedes earlier dashboard-only
background/glass/watermark direction.
