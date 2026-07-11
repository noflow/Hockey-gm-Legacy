# Hockey GM Legacy – Codex v1.0

**Tagline:** You are not building a hockey team. You are building hockey lives.

Hockey GM Legacy is a living hockey universe simulator where the player builds a lifelong executive career, beginning in junior hockey and potentially rising through the professional ranks.

## Start Here

1. [Project Charter](docs/00-charter/PROJECT_CHARTER.md)
2. [Manifesto](docs/00-charter/MANIFESTO.md)
3. [Documentation Index](docs/INDEX.md)
4. [MVP Scope](docs/00-charter/MVP_V1_SCOPE.md)
5. [Rule Engine Implementation Spec](docs/09-implementation-specs/RULE_ENGINE_IMPLEMENTATION_SPEC.md)

## Current Implementation Anchor

The current implementation is focused on standalone LegacyEngine modules:

- Rule Engine
- Owners
- Scouting
- People
- Relationships
- Events
- World
- Recruiting
- Contracts
- Draft
- Rosters
- Player Development
- Injuries
- Alpha Integration
- Alpha Console
- Human Intelligence
- Alpha Daily Simulation Pipeline
- Alpha Desktop
- Training Camp
- Season Readiness
- Executive Reports
- Staff & Scouting Operations
- Player Dossiers
- Staff Control
- Selectable People Rows
- Alpha 2.2 UI/UX Structural Pass
- Alpha 2.2.1 Dossier Window, Roster Filters, Budget Overview, and Scouting Cleanup
- Alpha 2.3 Recruiting v2
- Alpha 2.3.1 Name Generation System + Deduping
- Alpha 2.4 Staff Control v2 + Hockey Operations Budget
- Alpha 2.5 Season Framework v1
- Alpha 2.6 Game Recap + Stats Polish
- Alpha 2.7 First Month Playability Pass
- Alpha 2.7.1 Roster & Front Office Realism Pass
- Alpha 2.7.2 Inbox Cleanup + League Transaction Wire
- Alpha 2.7.3 Live Draft Layout + Staff Hiring Layout Fix
- Alpha 2.8 GM Office Navigation Redesign
- Alpha 2.9 Action Center & Daily Workflow UI
- Alpha 3.0 Existing World History v1
- Alpha 3.1 Free Agent Market v1
- Alpha 3.2 Trade Engine v1
- Alpha 3.3 Trade Deadline Event v1
- Alpha 3.4 Career & History Framework v1
- Alpha 3.5 Save/Load v1
- Alpha 4.0 Multi-Season Playability v1
- Alpha 4.1 Contracts v2
- Alpha 4.2 Free Agency v2
- Alpha 4.3 Trade Engine v2 (Negotiation & Team Strategy)
- Alpha 4.4 Scouting v2 (Intelligence & Reports)
- Alpha 4.5 Player Development v2 (Development Plans & Progress)
- Alpha 4.6 Staff & Coaching v3 (Philosophy & Development)
- Alpha 4.7 Injury & Medical v2 (Health & Recovery)
- Alpha 4.8 Owner & Job Security v2
- Alpha 4.9 League AI & Team Identity v2
- Alpha 5.0 Playability & Polish
- Alpha 5.1 Multi-League Career Framework
- Alpha 5.3 Full League Teams + NHL/AHL Player Pipeline v1
- Alpha 5.3.1 Trade Window Interactions + Living Staff Market
- Alpha 5.4 NHL/AHL/Junior Player Pipeline v1
- Alpha 5.6 Salary Cap & Roster Compliance v1
- Alpha 5.7 Agent Engine v1
- Alpha 5.8 Dynamic Draft Classes v1
- Alpha 5.9 League AI v2
- Alpha 6.0 Player Life Cycle v1
- Alpha 6.1 Staff Life Cycle v1
- Alpha 6.2 Owner Life Cycle v1
- Alpha 6.2.1 Trades v3: Roster Assets, Draft Picks, and Counter Offers
- Alpha 6.3 Relationship Expansion v1
- Alpha 6.4 Roster V3 + Lineup Roles v1
- Alpha 6.4 Lineup & Role Management v1
- Alpha 6.5 Line Chemistry v1
- Alpha 6.6 Special Teams & Game Usage v1
- Alpha 6.7 Tactics & Coaching Style v1
- Alpha 6.8 Game Simulation v2
- Alpha 6.9 Playoffs & Championship Framework v1
- Alpha 6.10 Hockey Operations Command Center
- Alpha 6.11 Organization Command Center
- Alpha 6.12 Franchise Identity & Culture v1
- Alpha 6.13 Living Story Engine v1
- Alpha 6.14 Media & News v1
- Alpha 6.15 Awards & Records v1
- Alpha 6.16 Draft V4: War Room & Amateur Scouting
- Alpha 6.17 Player Ratings & Potential v1
- Alpha 7.0 Hockey Intelligence Rating Engine v1
- Alpha 7.1 Waivers & Roster Transactions v1
- Alpha 7.2 RFA/UFA Contract Rights v1
- Alpha 7.3 Salary Arbitration v1
- Alpha 7.4 Contract Buyouts v1
- Alpha 7.5 Offer Sheets v1
- Alpha 7.6 Hockey Intelligence Rating Engine v1
- Alpha 7.7 Attribute Development Engine v1
- Alpha 7.8 Scouting Intelligence v2
- Alpha 7.9 Draft Intelligence V4: War Room + Attribute Scouting
- Alpha 7.10 Player Value & Asset Evaluation Engine v1
- Alpha 7.11 Draft Board Realism & Position Value Engine
- Alpha 7.12 AI Roster Builder & Organizational Planning v1
- Alpha 7.13 AI Front Office Decision Cycle v1
- Alpha 8.0 Presentation Layer & Universal Clickable Profiles
- Alpha 8.1 Hockey Operations Visual Redesign
- Alpha 8.2 Draft War Room & Trade Center Visual Redesign
- Alpha 8.3 Team Branding, Icons & Visual Identity
- Alpha 8.4 Alpha UX Playtest, Accessibility & Navigation Pass

Alpha 1.3 - GM Character Creation + First GM Actions starts AlphaDesktop on a GM creation screen, then drops the created GM into the Prairie Falcons scenario two weeks before the draft. The player can review the club, re-rank the draft board, assign a scout focus, make a recruiting offer, and advance days to process responses.

Alpha 1.4 - Complete Draft Experience adds a playable draft loop. The player can prepare the draft board, star prospects, add GM notes, assign scouting focuses, reach draft day, let AI teams draft, make selections, receive reactions, complete the draft, and review a draft recap. Draft length comes from the active RuleEngine rulebook.

Alpha 1.6 - Training Camp + Roster Cutdown v1 adds the first post-draft camp loop. After draft/offseason setup, the player can open camp, review returning players, drafted prospects, recruits, and AHL-style invite sources, generate staff evaluations, make keep/cut/assign/return decisions, and complete camp with RuleEngine roster validation.

Alpha 1.8 - Opening Roster & Season Readiness adds the Opening Night gate. The desktop now shows roster compliance, unresolved GM decisions, owner/coach/scout reviews, staff recommendations, and a checklist. Begin Season stays blocked until the roster, camp, prospect, and pending-action requirements are resolved by the player.

Alpha 1.8.1 - Executive Reports adds permanent in-memory career reports for each season. Front Office Readiness is archived when Opening Night requirements are complete, and End of Season Executive Review is archived after the SeasonEngine marks a season completed. Reports include owner, coach, scout, development, medical, roster compliance, organization health, progress, and executive summary sections without adding game simulation, standings, playoffs, or save/load.

Alpha 1.9 - Staff & Scouting Operations v1 deepens daily scouting. The GM can review scout profiles, relationships, workload, strengths, weaknesses, and conflict warnings; assign scouts to regions or specific prospects; advance days to complete reports; and use basic staff controls to reassign, release, or hire a placeholder staff candidate. Report confidence now reflects scout fit, workload, and relationship/communication quality.

Alpha 2.0 - Player Dossier v1 + Name Cleanup adds clean player display names and a GM-facing dossier. The desktop can open player/prospect dossiers from roster, recruits, scouting, draft board, prospect list, and training camp contexts, with facts, scouting language, development summary, medical context, contract/rights status, staff opinions, relationships, and editable GM notes. Dossiers keep internal ability and potential values private.

Alpha 7.4 - Contract Buyouts v1 adds rulebook-driven buyout windows and penalty calculations for NHL-style teams. The GM can calculate, confirm, or cancel buyouts from AlphaDesktop; confirmed buyouts release the player to free agency, terminate the contract, record future cap/budget penalties, update relationship/history state, and persist through save/load.

Alpha 7.5 - Offer Sheets v1 adds rulebook-driven RFA offer-sheet pressure for NHL-style teams. Eligible RFAs can receive offer sheets with AAV-based compensation, required draft-pick ownership checks, cap validation, player/agent interest, urgent Action Center items, inbox messages, League News, dossier/history lines, and explicit Match Offer or Take Compensation decisions.

Alpha 7.6 - Hockey Intelligence Rating Engine v1 completes the current player-evaluation backbone. Players carry hidden true OVR/POT and attribute ratings while the GM sees scouted estimates, Unknown/Red/Green/Blue/Black confidence colors, ranges for uncertain information, category and sub-attribute reports, scouting source/date notes, rating history, development-curve context, and save/load preservation. AlphaDesktop uses scouted rating text only and does not expose hidden true ratings.

Alpha 7.7 - Attribute Development Engine v1 makes player growth attribute-specific. Speed, strength, Hockey IQ, leadership, durability, confidence, and role-related attributes now respond differently to age, training focus, coach specialty, injury drag, role fit, rushed usage, and development curve. Development reports can update visible estimates, routine changes stay in development history, and meaningful breakthroughs/setbacks surface in dossiers and Action Center without exposing hidden true ratings.

Alpha 7.8 - Scouting Intelligence v2 makes scouting the GM's main path to better rating truth. Each organization can hold player knowledge profiles with known and unknown attributes, confidence colors, estimate ranges, stale flags, scout-specific opinions, disagreement summaries, consensus OVR/POT estimates, and scout accuracy history. Completed scouting assignments now improve visible estimates and attribute knowledge based on scout quality, fit, workload, relationship, specialty, region, and bias; dossiers, the War Room, Action Center, and AlphaDesktop show this as scouting intelligence without exposing hidden true ratings.

Alpha 7.9 - Draft Intelligence V4 turns the Draft War Room into the main Hockey Intelligence showcase. The GM can review My Board, Scout Board, Consensus Board, watch tags, team needs, picks, prospect comparison, class summary, and Hidden Gems / Avoid List views. Draft rows and live draft cards now show visible OVR/POT ranges, confidence colors, public bio, attribute knowledge with unknown `???` values, scout disagreement, team fit, development curves, ETA, risk alerts, and post-draft history snapshots while keeping hidden true ratings internal.

Alpha 7.10 - Player Value & Asset Evaluation Engine v1 adds contextual asset value for players, prospects, contracts, draft picks, and market positions. The engine now evaluates current value, future value, contract value, trade value, organizational fit, and position scarcity for C, LW, RW, LD, RD, and G. AlphaDesktop exposes Position Market, asset value summaries, trade value comparison, free-agent demand notes, and draft/team-needs scarcity context without creating a public trade calculator or exposing hidden true ratings.

Alpha 7.11 - Draft Board Realism & Position Value Engine improves board shape and positional value so draft classes stop over-clustering one position. NHL-style boards now apply realistic forward/defense/goalie distribution pressure, while junior boards keep broader flexibility. Position value context can raise scarce positions without burying elite exceptions or exposing hidden true ratings.

Alpha 7.12 - AI Roster Builder & Organizational Planning v1 gives organizations a multi-year planning layer. The engine now creates organization plans with current and future depth charts, prospect development paths, succession planning, contract outlook, roster needs, trade/free-agency targets, and competitive-window reports. AlphaDesktop exposes Organization Planning in the Organization and Reports / History workspaces while keeping the planning layer advisory only.

Alpha 7.13 - AI Front Office Decision Cycle v1 lets AI-controlled organizations act on those plans through scheduled decision windows, explainable decision candidates, transaction plans, cooldowns, emergency overrides, and capped League News summaries. The player organization remains advisory-only, so the sim can create league movement without auto-managing the GM's roster, contracts, prospects, or staff.

Alpha 8.4 - Alpha UX Playtest, Accessibility & Navigation Pass improves real playtest comfort in AlphaDesktop. The GM Office now has compact breadcrumbs, bounded Back/Forward context history, focusable buttons, Ctrl+S save, Ctrl+F search, Escape-to-close safe popups, reset filters, clearer empty states, persistent success/status feedback, destructive-action confirmations, display density options, save/load metadata, and a documented UX findings checklist without adding gameplay systems.

Alpha 2.1 - Staff Control v2 turns the Staff tab into a front-office workspace. The GM can review full staff profiles, contract references, strengths, weaknesses, GM relationships, chemistry warnings, current assignments, current focus areas, candidate pool recommendations, and recent evaluations. Staff actions now include candidate generation/hiring, role changes, releases, development/medical/scouting focus changes, and staff evaluation messages while preserving Alpha 1.9 Scouting Operations.

The Alpha UI Interaction Pass replaces major text-dump people screens with selectable rows and detail/action panels. Staff, roster, recruits, scouting, scouting operations, draft board, prospect list, training camp, and dossier entry points now let the GM select the person first, then take valid staff/player/prospect actions from that selected detail panel.

Alpha 2.2 - UI/UX Structural Pass v1 turns AlphaDesktop into a more playable GM workspace. The dashboard now uses cards for current date, draft/training camp countdowns, unread inbox, pending decisions, roster issues, and scouting reports, with quick actions for advancing time and jumping to Inbox, Draft Board, and Pending Actions. Main tabs now show simple notification counts while the Inbox v2 layout remains intact.

Alpha 2.2.1 - Dossier Window, Roster Filters, Budget Overview, and Scouting Cleanup removes the permanent Player Dossier tab in favor of selected-person dossier windows with editable GM notes. The roster now has search/filter controls and richer non-hidden summary fields, Dashboard and Owner views show a simple Integration-level budget overview, and scouting assignments now use duration/return-date dialogs with available scouts only.

Alpha 2.3 - Recruiting v2 makes recruit management a front-office workflow. Recruits now expose richer priorities, family concerns, decision style, scouting context, competitor pressure, offers, promises, and GM notes. The desktop recruit panel supports calls, family calls, visit invites, offers, education-package promises, recruiting promises, scout information requests, offer withdrawals, and dossier access, with inbox messages and explainable recruit decisions.

Alpha 2.3.1 - Name Generation System + Deduping adds a standalone regional name generator for long-running player and staff creation. Generated names come from national/regional pools, never use visible numeric suffixes, keep uniqueness through PersonId, dedupe recruit/draft/scouting rows by person ID, and clarify true same-name players with position, age, and region/team context.

Alpha 2.4 - Staff Control v2 + Hockey Operations Budget adds league-driven staff and GM salary ranges, candidate salary asks, active staff salary impact, released-staff obligations, and a hockey operations budget breakdown. The desktop now shows GM salary, coaching, scouting, medical/training, staff total, player contracts, remaining budget, and owner warnings when hiring pushes the club over budget.

Alpha 2.5 - Season Framework v1 adds the first basic regular-season loop. Beginning the season now generates a league schedule, Advance Day and Advance 7 simulate scheduled games, standings update, team/player/goalie stats accumulate, game recap inbox messages are created, and AlphaDesktop exposes Schedule, Standings, and Stats tabs plus a dashboard next-game card.

Alpha 2.6 - Game Recap + Stats Polish turns simulated results into readable GM information. Completed games now create boxscore-style recaps with final score, shots/power-play placeholders, three stars, notable players, goalie summary, light medical/development notes, and narrative summaries. The desktop dashboard now shows last game, next game, and team record; Schedule separates today/upcoming/recent results and recaps; Standings show rank and goal differential; Stats now show team, league, skater, and goalie leaders without exposing hidden ratings.

Alpha 2.7 - First Month Playability Pass makes time advancement more playable. The desktop now supports Advance Day, Advance Week, Advance to Next Game, and Advance to Month End, with clear stop reasons for player games, injuries, urgent GM decisions, roster problems, scouting reports, and month-end reports. Inbox messages now carry priority, urgent/unread/newest sorting is clearer, routine league-wide games stay out of the GM inbox, and monthly GM summaries collect record, standings, owner/staff, medical, development, roster, budget, scouting, and pending-action context.

Alpha 2.7.1 - Roster & Front Office Realism Pass updates the junior roster target to 26 players, starts the New GM scenario with a legal roster, adds realistic draft prospect bios with physical/team/background context, expands hockey operations staff roles, adds rulebook-driven staff limits and vacancies, and improves staff candidate hiring information with salary, role, employer, experience, strengths, weaknesses, and chemistry risk.

Alpha 2.7.2 - Inbox Cleanup + League Transaction Wire keeps the GM inbox focused on your organization and decisions that need attention. Other-team signings, contract updates, roster moves, injuries, draft picks, and staff transactions now route into a separate League News / Transaction Wire feed with team, player/staff, category, date, and description.

Alpha 2.7.3 - Live Draft Layout + Staff Hiring Layout Fix reshapes the live draft modal into selected prospect, draft list, and draft status columns. Draft rows now surface basic visible bio and scouting context, dossier overviews include draft bio facts, and the Staff tab separates Current Staff, Hire Staff / Staff Candidates, and Vacancies so candidate actions no longer look like staff actions.

Alpha 5.6 - Salary Cap & Roster Compliance v1 adds rulebook-driven salary-cap behavior for professional leagues while junior leagues keep operating budgets. NHL-style and AHL-style rulebooks now expose cap amount, floor, roster limit, contract limit, and placeholders for retained salary/offseason rules. The desktop dashboard, budget workspace, free-agent decisions, and trade builder now show cap impact before the GM commits, and contract/trade/free-agent approval paths reject moves that would violate cap or contract-count rules.

Alpha 5.7 - Agent Engine v1 introduces recurring player representatives as the primary contract and free-agency negotiators. Agents now have profiles, agencies, personalities, negotiation styles, reputation, relationships with the GM and organization, client lists, representation history, and negotiation history. Contract/free-agent offer evaluations now show agent opinion, likelihood, biggest concern, requested improvement, risk, and counter suggestion, while accepted terms still create GM approval decisions rather than auto-signing.

Alpha 5.8 - Dynamic Draft Classes v1 gives each generated draft year a readable identity. Draft classes now have themes, quality, strengths, weaknesses, storylines, positional depth, regional distribution, scout quotes, risk/context notes, and rulebook-specific behavior: NHL-style drafts use ages 17-20 with most players 18-19 and broader geography, junior-style drafts use younger regional prospects, and AHL-style rulebooks remain draft-disabled. Draft Board, Live Draft, dossiers, scouting reports, and draft history now surface class context without exposing hidden ratings or using real player databases.

Alpha 5.9 - League AI v2 makes AI organizations behave less like clones. Every league team now carries an explicit OrganizationAiProfile with personality, strategy phase, current needs, urgency, suggested asset targets, draft/trade/free-agency/budget/scouting/staff behavior, strategy history, decision scoring, and limited league-news strategy headlines. Trade explanations now reference the other team's strategy and top needs, and save/load preserves organization AI profile state.

Alpha 6.0 - Player Life Cycle v1 turns tracked players into long-term career stories. Players now receive life stages, career phases, reputation categories, milestones, achievements, career story lines, legacy scores, key staff influence, and limited notable league-news items. Player dossiers and Reports / History now show career milestones, player stories, timeline context, legacy score, staff/scout/medical career notes, and Action Center items for meaningful career developments without adding Hall of Fame, retirement decisions, awards voting, or game simulation changes.

Alpha 6.1 - Staff Life Cycle v1 turns coaches, scouts, medical staff, and front-office staff into long-term careers. Staff now receive life stages, career phases, reputation categories, milestones, staff career stories, salary/role/organization history, coaching-tree links, scout discovery records, player-development influence, promotion readiness, concern summaries, limited staff League News milestones, and Action Center items for promotion/succession/performance review without adding Hall of Fame, retirement decisions, awards voting, or game simulation changes.

Alpha 6.5 - Line Chemistry v1 adds explainable fit grades for forward lines, defense pairs, goalie depth, and the team summary. Chemistry considers player-type blend, handedness, position fit, role pressure, age/experience mix, relationships, coach philosophy, morale/confidence, role promises, and recent-performance placeholders. The Lineup workspace now shows chemistry beside each line/pair, selected-line details show strengths/weaknesses/recommendations, player dossiers include chemistry notes, and Action Center only flags major chemistry problems.

Alpha 6.6 - Special Teams & Game Usage v1 expands lineup management into personnel deployment. The GM can now review and adjust PP1/PP2, PK1/PK2, goalie workload, extra attacker, three-on-three placeholder, and shootout order from the Lineup workspace. Player dossiers include a Game Usage section, development context receives small usage modifiers, coach recommendations flag PP/PK/goalie concerns, and Action Center only surfaces important usage issues without adding tactics, line matching, or game simulation changes.

Alpha 7.2 - RFA/UFA Contract Rights v1 adds rulebook-driven expiring-contract rights for professional leagues. NHL-style teams now classify expiring players as pending RFA or pending UFA, show qualifying-offer deadlines and amounts, let the GM qualify or decline rights, retain RFAs when qualified, move UFAs/not-qualified players into the free-agent market, record rights history, update dossiers, create Action Center warnings, and expose a Contract Rights view in AlphaDesktop. Junior-style rulebooks keep NHL-style RFA/UFA rules disabled unless enabled.

Alpha 7.3 - Salary Arbitration v1 adds the first rulebook-driven arbitration path for qualified RFAs. NHL-style leagues can define eligibility age, accrued-season threshold, filing window, hearing timing, walk-away permission, and award bounds. Eligible cases show in Hockey Operations / Arbitration, player dossiers, Action Center, inbox, and League News; the GM can file arbitration, negotiate a settlement, accept an award, or walk away where the rulebook allows. This does not add offer sheets or full CBA edge cases.

Alpha 6.7 - Tactics & Coaching Style v1 defines how the team plays without becoming a full tactics simulator. Hockey Operations now includes a Tactics view for style, forecheck, neutral-zone pressure, defensive-zone structure, breakout, shot preference, physicality, risk, PP style, PK style, coach philosophy, tactical fit, recommendations, and small player/development impact notes. Action Center only surfaces major tactical mismatches such as high-risk tactics on a young roster.

Alpha 6.8 - Game Simulation v2 upgrades the scheduled-game simulator while staying boxscore-based. Daily games now build explainable team profiles from lineup roles, line chemistry, special-teams usage, tactics, coach fit, goalie workload, injuries, home/away context, and public strength bands. Recaps now show top-line, special teams, tactical, chemistry, goalie usage, key concern, injury, development, and milestone context without adding play-by-play, line matching, shift simulation, or Godot.

Alpha 6.9 - Playoffs & Championship Framework v1 adds a basic postseason path after the regular season. The engine now seeds playoff teams from standings, builds a rulebook-aware bracket, simulates best-of series using the existing boxscore game simulator, tracks playoff stats separately from regular-season stats, records series results, crowns a champion, stores playoff history, and exposes Playoffs, Playoff Archive, and Champions views in AlphaDesktop.

Alpha 6.10 - Hockey Operations Command Center turns Hockey Operations into one integrated workspace. The GM can move between roster, prospects, AHL, junior rights, free agents, and trade targets from a left source rail; review lines, roster, development, contracts, scouting, trade, and free-agency context in the center; and keep a selected-player command card on the right with dossier, role, current line, contract/rights, development, scouting, medical, relationships, history, and quick actions. This is a UI/workflow pass only.

Alpha 6.11 - Organization Command Center adds the matching front-office workspace. The GM can select Owner, Front Office, Coaching, Scouting, Development, Medical, Equipment, Finance, or Facilities placeholder departments, review department health, organization chart, needs, budget, reports, and Action Center items, then select staff/vacancies on the right for profile, performance, contract, relationship, history, focus, movement, release, or hiring workflow actions. This is an executive UI/workflow pass only.

Alpha 6.12 - Franchise Identity & Culture v1 gives every organization a long-term identity, culture, current era, historical eras, reputation, team DNA, strengths, weaknesses, future goals, and identity-shift history. Organization Command Center now shows a Franchise Overview, executive reports include franchise identity context, League News can surface limited identity/culture headlines, and player/staff fit summaries explain why someone fits the club without exposing hidden ratings.

Alpha 6.13 - Living Story Engine v1 connects isolated history, milestones, relationships, career context, and franchise identity into readable story arcs. Player dossiers now include a Stories section, Organization Command Center shows the current organization story, reports/monthly summaries reference top stories, Action Center can surface only major story updates, League News gets capped story headlines, and save/load preserves story state without adding a media engine, articles, social media, or extra inbox spam.

Alpha 6.14 - Media & News v1 turns stories and major hockey events into short fictional media articles. The new media feed uses fictional sources such as League Wire, Hockey Daily, Prospect Central, Front Office Report, Local Beat, and Draft Desk; separates article/story coverage from the League News transaction wire; supports rumor confidence labels; surfaces top headlines on the dashboard; adds Media / News under Reports / History; and shows related media in player dossiers and Organization Command Center without adding social media, press conferences, real media brands, or inbox spam.

Alpha 6.15 - Awards & Records v1 adds basic accomplishment history. Season-end award generation now creates deterministic league/team/playoff/rookie/staff/GM/scouting/development winners from public stats, team success, role, reputation, and story context; record books track goals, assists, points, goalie wins, shutouts, games played, team wins, championships, and playoff points placeholders; winners and broken records create career timeline/media history; Reports / History now includes Awards, Record Book, Team Records, and League Records; player dossiers and Organization Command Center surface awards and records without adding Hall of Fame, jersey retirement, real trophy names, or a full voting system.

Alpha 6.16 - Draft V4: War Room & Amateur Scouting turns draft prep into a permanent Hockey Operations workflow. The Draft War Room now keeps a custom GM board, original board history, watch-list tags, team needs, draft class storylines, best-player opinions by department, scout consensus, prospect comparison, smarter AI draft selection, and a post-draft review. AlphaDesktop adds the War Room screen plus live draft Skip, View Dossier, and Compare controls without adding mock drafts, lottery logic, real player databases, Godot, or extra media systems.

Alpha 6.17 - Player Ratings & Potential v1 adds visible 0-100 OVR/POT estimates to the GM workspace while keeping scouting uncertainty intact. Roster, recruit, prospect, scouting, draft, free-agent, trade, live draft, and dossier views now show readable rating context; low-confidence prospects show ranges instead of hidden truth; development, injuries, league level, role, age, and scouting confidence influence the visible snapshot. Development curves now explain whether a player is an early bloomer, late bloomer, slow burn, boom/bust, high-floor, injury-risk, or patience-required prospect, including ETA, plateau risk, breakout chance, staff note, and best path. This does not add a full attribute system, editable ratings, a real player database, Godot, or a new game simulation engine.

Alpha 7.0 - Hockey Intelligence Rating Engine v1 adds the deeper rating backbone behind the visible OVR/POT layer. The engine now keeps hidden true ratings separate from scouted estimates, supports Offensive, Defensive, Skating, Physical, Skill, Mental, Team, and Goalie attribute categories, and uses Unknown/Red/Green/Blue/Black confidence colors so uncertain players show ranges or unknowns. Repeated scouting improves confidence, elite scouts improve it faster, development changes hidden truth before visible estimates, and dossiers show scouted category ratings without exposing internal truth. This does not add an editable rating editor, real player database, Godot, or a new simulation engine.

Alpha 7.1 - Waivers & Roster Transactions v1 adds rulebook-driven waiver movement for professional leagues. NHL/AHL-style careers now track waiver eligibility, exemptions, waiver placement, claim windows, claims, clears, affiliate assignments, recalls, waiver history, League News transaction entries, and player dossier waiver status. Junior-style leagues keep waivers disabled. This does not add LTIR, emergency recalls, conditional waivers, buyouts, full CBA edge cases, Godot, or a new simulation engine.

Alpha 6.4 - Lineup & Role Management v1 makes lineup roles playable. Hockey Operations now includes a selectable Lineup workspace where the GM can assign, remove, swap, and auto-fill slots across four forward lines, three defense pairs, and starter/backup goalie depth. Roster rows and dossiers show current, expected, promised, coach-recommended, and potential roles, plus promise status, satisfaction, morale notes, and development usage context. Contract offer evaluation now warns when a role promise conflicts with lineup capacity, while invalid placements warn instead of crashing. This does not add special teams, tactics, line chemistry, Godot, or game simulation changes.

Alpha 6.2 - Owner Life Cycle v1 turns ownership into a long-term organization story. Owners now receive life stages, career states, expectation history, confidence history, meeting history, permanent owner letters, job-security history, legacy profiles, budget relationship summaries, organization era context, owner milestones, limited League News milestones, Action Center owner items, and Reports / History views for Owner History, Owner Letters, Job Security History, and Expectation Results without adding actual firing, owner replacement, job offers, board logic, or game simulation changes.

Alpha 6.2.1 - Trades v3: Roster Assets, Draft Picks, and Counter Offers makes the trade builder usable for alpha testing. Trade windows now show both teams' roster players, prospect rights, draft picks, and future-consideration placeholders; draft picks have year, round, owner, protected-placeholder, and value context; proposal buckets are separated into You Give and You Receive; close offers can return concrete counter packages; and completed trades create readable League News summaries while still requiring Pending GM approval.

Alpha 6.3 - Relationship Expansion v1 makes relationship context visible and useful across the hockey universe. The engine now tracks expanded GM/player/staff/owner/agent/organization relationship profiles with trust, respect, loyalty, conflict, communication quality, trend, key moments, change history, conflicts, and chemistry summaries. Contract offers, trade context, staff chemistry, player dossiers, owner views, organization health, save/load, and Action Center warnings now use relationship context as a modest decision modifier without adding a full drama system.

Alpha 6.4 - Roster V3 + Lineup Roles v1 adds hockey-readable roster context. Teams now generate opinion-based lineup roles such as First Line Forward, Top Six Forward, Top Pair Defenseman, Starting Goalie, Depth Forward, and Prospect Goalie; the player organization receives default forward lines, defense pairs, and goalie depth; Hockey Operations now includes a Lineup view with coach recommendations; roster rows show role, potential role, line/pair, development stage, and contract status; and trade/team-browser screens identify target type without exposing hidden ratings or adding a tactics engine.

Alpha 3.4 - Career & History Framework v1 starts the long-save memory layer. The engine now seeds GM, staff, organization, and player career history, tracks player draft picks and draft classes, adds Where Are They Now records, records trade/free-agent/injury history, and expands Reports / History with GM Career, Organization History, Draft History, Drafted Players, Player Career Timelines, Staff History, and Transaction History views without exposing hidden ratings.

Alpha 3.5 - Save/Load v1 adds local JSON career saves. AlphaDesktop can start a new career, load an existing career, save the current career, or save as a new file. Saves include the current scenario snapshot, roster, staff, contracts, prospects, free agents, trades, schedule, standings, stats, inbox statuses, league news, pending GM actions, Action Center statuses, scouting/recruiting/training camp state, season readiness, budget snapshot, executive/monthly reports, and career/draft/organization history. Save files are local `.json` files under `%LOCALAPPDATA%\\HockeyGmLegacy\\Saves` by default.

Alpha 4.0 - Multi-Season Playability v1 proves the Alpha career can roll from one season into the next. Once the regular-season schedule is complete, the GM can finish the season, archive final standings, stats, recaps, and organization history, generate an End of Season Executive Review, enter the offseason, create the next draft class, convert expiring contracts into explicit pending GM decisions, reset current-season stats, preserve prior-season and career history, and save/load the career after rollover.

Alpha 4.1 - Contracts v2 adds explainable contract asks and offer evaluation without adding a full agent system. The GM can review contract asks for roster players, prospects, recruits, free agents, and staff; see salary, term, desired role, budget before/after, common expiry date, likelihood, risk warnings, and reasons. Accepted terms create pending GM approval actions and never sign automatically.

Alpha 4.2 - Free Agency v2 turns unsigned player shopping into a calendar-driven market. Free agency now has opening, active, slow, late, and closed phases; players expose top motivations, competing offers, staff recommendations, budget impact, offer likelihood, response timing, and decision explanations. Accepted offers still become pending GM approvals first, while other-team signings flow to League News instead of cluttering the GM inbox.

Alpha 4.3 - Trade Engine v2 (Negotiation & Team Strategy) makes AI trade logic depend on club direction, roster needs, prospect pipeline, budget pressure, owner/team context, and GM trade personality. Trade evaluations now explain asset fit, need matching, budget impact, simple counter requests, staff/player reactions, and estimated value without exposing hidden ratings. Multi-asset trades continue to require explicit GM approval before any roster or rights change.

Alpha 4.6 - Staff & Coaching v3 (Philosophy & Development) makes staff a living hockey operations layer. Coaches and staff now surface philosophy, specialties, personality, staff chemistry, player fit, monthly staff meetings, department grades, organization chart context, hiring fit, player dossier staff opinions, Action Center coaching items, and staff performance reviews without adding Godot, save/load changes, or game simulation changes.

Alpha 4.7 - Injury & Medical v2 (Health & Recovery) turns injuries into a GM health-management system. Roster players now have health profiles with durability, fatigue, recovery rate, injury risk, wear and tear, recurring concerns, medical confidence, and conditioning. Medical reports explain return timing, risk, staff confidence, and recommended return-to-play choices, while dossiers, Action Center, organization health, and executive reports surface medical context without changing save/load or game simulation.

Alpha 4.8 - Owner & Job Security v2 makes ownership a visible accountability layer. The owner now has a richer personality profile, season expectations, confidence/pressure/support state, scheduled meetings, letters, budget and strategy decisions, GM performance review, job security explanation, Action Center items, executive report context, and career-history hooks. Alpha 4.8 creates warnings and pressure only; it does not add automatic firing, job offers, save/load changes, or game simulation changes.

Alpha 4.9 - League AI & Team Identity v2 makes organizations recognizable by how they build. Every league team now receives an identity, AI GM personality, owner influence, current strategy, monthly needs, budget style, draft style, scouting focus, development grade, trade/free-agency/draft/scouting behavior, occasional League News direction headlines, and organization-history hooks. These profiles are deterministic and in-memory for Alpha, with no media engine, save/load changes, or game simulation changes.

Alpha 5.0 - Playability & Polish cleans up the day-to-day GM workflow without adding new simulation systems. The GM inbox now stays focused on decisions and important messages, routine updates move to a searchable Journal, League News remains separate, the dashboard explains inbox/journal/action counts, global search is active across core career data, and Reports / History includes a playtest checklist for quick first-month regression review.

Alpha 5.1 - Multi-League Career Framework adds league and team selection before GM creation. The player can start NHL-style, AHL-style, Junior, or Custom placeholder careers, each with its own LeagueProfile, rulebook, team options, focus, difficulty, rules summaries, current champion/history, and save metadata. NHL-style enables the draft and pro roster context, AHL-style disables the amateur draft and references a parent club, and Junior keeps recruiting/scouting/draft/development as the main loop while all scenarios reuse the same LegacyEngine systems.

Alpha 5.3 - Full League Teams + NHL/AHL Player Pipeline v1 fills out the fictional hockey map. League/team selection now exposes 32 NHL-style clubs, 32 AHL-style affiliates, and full WHL/OHL/QMJHL junior team lists with market, league/division, arena, owner/budget, organization identity, roster/prospect/staff quality, previous record, current strategy, and difficulty context. NHL and AHL teams now carry parent/affiliate links, AHL careers disable the amateur draft and start with parent-assigned/depth players, and drafted/prospect players track pipeline status, assignment status, rights holder, current level, parent/affiliate context, and assignment history in dossiers and save data.

Alpha 5.3.1 - Trade Window Interactions + Living Staff Market makes two front-office screens behave like actual tools. The trade builder popup now has selectable lists for your assets and the other team's assets, double-click/add/remove controls, You Give/You Receive proposal rows, live roster/budget/evaluation/counter context, dossier access, and a disabled Propose Trade button until both sides have assets. Accepted trades still become Pending GM Actions and do not auto-complete. Staff hiring now uses a living market with available, interested, employed, hired, withdrawn, and not-interested candidate states, salary asks, career history, current employer, hiring interest, movement records, save/load preservation, and league-news staff movement without inbox spam.

Alpha 5.4 - NHL/AHL/Junior Player Pipeline v1 makes prospect assignment rules explicit. NHL-drafted prospects remain rights-held/unsigned/signed prospects until the GM acts, CHL/junior-age players can be signed and returned to junior without filling NHL/AHL rosters, AHL assignments require the right rulebook eligibility, 18/19-year-old signed players show ELC slide status and games toward the threshold, and dossiers/prospect lists explain AHL eligibility, junior eligibility, rights status, development level, and staff recommendations without adding salary cap, waivers, or full CBA systems.

Alpha 4.4 - Scouting v2 (Intelligence & Reports) turns scouting into an information system instead of a rating reveal. Scouts now have tendencies, known regions, specialties, workload effects, confidence stars, viewing samples, tournament context, budget coverage notes, report comparisons, and scout career/development summaries. Player dossiers show multiple report-card style opinions, evidence, disagreements, and confidence while keeping hidden ratings private.

Alpha 4.5 - Player Development v2 (Development Plans & Progress) turns development into a GM-managed pathway. Tracked players now receive development plans with focus areas, ice-time roles, confidence, morale, coach comments, coach recommendations, yearly reviews, dossier development context, Action Center items, and career timeline hooks. Development remains player-facing and explainable while hidden ability and potential values stay private.

Alpha 2.8 - GM Office Navigation Redesign replaces the crowded top-level AlphaDesktop tabs with a cleaner GM Office shell: Dashboard, Inbox, Organization, Hockey Operations, Season, Reports / History, and Settings. Existing features are preserved inside workspace sub-navigation, the dashboard now acts as an action center with owner mood and grouped advance controls, and League News remains near the Inbox without adding save/load, Godot, or new game simulation behavior.

Alpha 2.9 - Action Center & Daily Workflow UI makes the dashboard answer "What should I do today?" The desktop now aggregates pending GM actions, urgent inbox messages, roster warnings, staff vacancies, budget warnings, scouting completions, upcoming games, injury issues, and season readiness into a filterable Action Center with daily agenda, Assistant GM recommendations, priority counts, and resolve/defer/dismiss workflow. It does not auto-complete GM decisions.

Alpha 3.0 - Existing World History v1 gives the New GM scenario a lived-in past. Roster players and prospects now have prior stats, older players have multi-year summaries, staff can carry career timeline entries, the organization has a prior-season record, and dossiers/roster views expose history without revealing hidden ratings.

Alpha 3.1 - Free Agent Market v1 adds unsigned player management to Hockey Operations. The scenario now generates a clean-name free-agent market with veterans, released players, undrafted players, depth skaters, goalies, prior stats, contract asks, interest, fit summaries, and staff recommendations. The GM can view dossiers, shortlist players, invite players to camp, submit offers, withdraw offers, and approve/decline pending signings; contracts are created only after explicit GM approval.

Alpha 3.2 - Trade Engine v1 adds the first player-controlled trade flow. The GM can review a league trade block, inspect available players, build simple offers from roster players, prospect rights, or draft-pick placeholders, propose trades, receive AI accept/reject/counter feedback, and complete accepted trades only through explicit Pending GM Action approval. Completed trades create league news while the GM inbox stays focused on team-relevant trade decisions.

Alpha 3.3 - Trade Deadline Event v1 makes the deadline a calendar-driven league event. The engine tracks approaching, deadline week, deadline day, and closed states from the SeasonEngine calendar, expands the trade block near the deadline, creates limited rumors and owner/coach/assistant pressure messages, adds deadline items to the Action Center and League News, and blocks new trade proposals after the deadline until the playoff/offseason window. Accepted trades created before the deadline remain valid pending GM approval.

Inbox v2 organizes GM messages into category tabs, supports read/unread, archive, delete, and pin state, and keeps Event Engine history intact.

Alpha 1.2 refines the desktop inbox into an email-style GM workspace with a category sidebar, message list, reading pane, row-level actions, and local filters.

## Alpha Console

Run the engine-only playtest harness:

```bash
dotnet run --project tools/AlphaConsole
```

Run the basic desktop UI:

```bash
dotnet run --project client/AlphaDesktop
```

The desktop playtest harness starts with league/team selection, GM creation, and draft preparation. It supports full fictional NHL/AHL/junior team browsing, parent/affiliate pipeline context, Staff & Scouting Operations, Scouting v2 report comparisons, Player Development v2 plans/reviews, Player Life Cycle career stories, Staff Life Cycle career stories, Owner Life Cycle history, Relationship Expansion chemistry/conflict views, Staff Control, living Staff Market hiring with salary asks and career history, staff vacancies, Hockey Operations Budget, Recruiting v2, Free Agency v2, Contracts v2, Trade Engine v2 with selectable asset proposal building, Trade Deadline Event v1, Player Dossier windows, roster filters, selectable person-specific action panels, lineup roles, line chemistry, special teams/game usage, team tactics/coaching style, a League News transaction wire, and a card-based dashboard with notification counts, then unlocks training camp after draft/offseason setup, Season Readiness before Opening Night, Executive Reports for the career archive, and a basic Schedule/Standings/Stats season loop with readable game recaps after Begin Season. The first-month flow adds smarter advance controls, priority inbox handling, monthly GM summaries, realistic draft prospect bios, a 26-player junior roster target, and a clearer three-column live draft experience. Smoke tests and the console harness keep a Jordan Hayes fallback when no custom GM is supplied.

Available commands:

- `help`
- `status`
- `inbox`
- `owner`
- `staff`
- `roster`
- `recruits`
- `scouting`
- `draftboard`
- `relationships`
- `advance`
- `advance 7`
- `exit`

First rulebook:

`data/rulebooks/junior_v1.json`

## Repository Structure

```text
docs/
  00-charter/
  01-game-bible/
  02-technical-bible/
  03-ai-bible/
  04-communication-systems/
  05-hockey-bible/
  06-ui-ux-bible/
  07-milestones/
  08-rfcs/
  09-implementation-specs/
  10-codex-prompts/

data/
  rulebooks/
  templates/

engine/
  LegacyEngine/

client/
  GodotClient/

tests/
tools/
```

## Architecture Pillars

1. Legacy Engine — the living world.
2. Event Engine — what happens.
3. Human Intelligence System — how people think.
4. Communication Engine — who knows what.
5. Rule Engine — what each league allows.
