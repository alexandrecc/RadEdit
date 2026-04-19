# Proofing Architecture

## Purpose

This document describes the target proofreading architecture for RadEdit before implementation work starts. The goal is to make the direction explicit, reduce ambiguity during refactoring, and keep product decisions aligned with the real editing workflows used through `WM_COPYDATA`, Dragon dictation, and the proofreading UI.

## Why The Current Design Is Not Enough

Today the proofing flow is effectively driven by full-document snapshots inside `Form1`:

- a text change schedules a proofing pass for the whole document
- the LLM path re-splits the whole document into sentence segments
- suggestions are rebuilt from provider output for the current snapshot
- dismissed LLM suggestions do not have stable identity across rechecks
- `SendCopyData` edits are skipped only at insertion time, not treated as permanently trusted

That creates the current problems:

- dismissed suggestions can reappear
- long documents can stall the UI while segmenting, diffing, and redrawing
- caret/selection can drift after apply
- hover popup behavior interferes with text selection
- text inserted via trusted automation can be re-proofed later
- the model/server configuration is too static for runtime discovery

## Design Goals

- Only proof the parts of the document that actually need proofing.
- Treat automation-inserted scaffold text as trusted by default.
- Support slot-based templates where `[]` marks editable dictation zones.
- Keep suggestion dismiss/apply state stable across local edits.
- Keep providers simple and stateless.
- Move proofing work off the UI thread.
- Make caret and selection behavior deterministic after suggestion apply.
- Discover server availability and loaded models at startup without blocking the editor.

## Non-Goals

- This design does not require removing LanguageTool.
- This design does not require changing the external `WM_COPYDATA` command surface first.
- This design does not require a new editor control; it can be layered onto the existing `RichTextBox`.

## Architectural Shape

The target architecture is:

- a stateful `DocumentSession`
- a stateful `SuggestionManager`
- stateless proofing providers
- an incremental scheduler that works on local proofing units instead of full-document snapshots

The core principle is:

- document memory lives in RadEdit
- proofreading providers answer isolated requests
- the UI renders current state but does not own proofing logic

## Main Components

### `DocumentSession`

`DocumentSession` is the source of truth for the editable document.

Responsibilities:

- current text revision
- tracked ranges and anchors
- region metadata
- slot metadata
- local proofing unit metadata
- active and historical suggestion state
- caret mapping through edits

### `EditorController`

`EditorController` converts UI events and `WM_COPYDATA` actions into normalized edit transactions.

Responsibilities:

- translate typing, dictation, paste, load, and suggestion-apply operations into the same edit model
- preserve edit provenance
- send document updates into the session
- request UI refresh for only the affected ranges

### `ProofingScheduler`

`ProofingScheduler` decides what to proof, when, and with what priority.

Responsibilities:

- debounce local edits
- build affected proofing units
- cancel stale work by revision
- keep proofing off the UI thread
- limit concurrency so long documents do not flood the provider

### `SuggestionManager`

`SuggestionManager` owns suggestion identity and lifecycle.

Responsibilities:

- keep active suggestions
- remember dismissed suggestions
- remember applied suggestions
- invalidate stale suggestions when a proofing unit changes
- filter suggestions against allowed edit ranges

### `IProofingProvider`

Providers stay stateless.

Examples:

- `LlmProofingProvider`
- `LanguageToolProvider`

Responsibilities:

- given a proofing request, return normalized suggestion candidates
- no provider-owned memory about document history

### `ProviderDiscoveryService`

This service handles proofing-server runtime discovery.

Responsibilities:

- probe the configured server
- list models with loaded instances reported by the server
- choose the active model automatically from those loaded instances
- expose availability state to the UI

## Core Data Model

The exact class names can change, but the state model should follow this shape.

### Edit Source

Each mutation must record where it came from.

Examples:

- `UserTyping`
- `DragonDictation`
- `SendCopyDataTrusted`
- `ApplySuggestion`
- `LoadDocument`

This is required because RadEdit must treat trusted automation differently from organic text entry.

### Regions

The document should track region semantics, not only raw text.

Region kinds:

- `TrustedScaffold`
- `Editable`
- `EditableSlot`

Meaning:

- `TrustedScaffold` is visible context but should not be proofed by default
- `Editable` is normal proofable user text
- `EditableSlot` is a template-defined editable region created from `[]`

### Slots

Slots are explicit objects, not just text patterns.

A slot should track:

- `slotId`
- current `range`
- `policy`
- empty or filled state

Recommended policies:

- `Strict`: only text inside the slot may be changed by suggestions
- `BoundaryAware`: allow limited correction near the slot boundary when agreement requires it

### Proofing Units

A proofing unit is the smallest local area RadEdit asks a provider to check.

Usually:

- one sentence
- one sentence centered around an edited slot
- one local paragraph window when sentence boundaries are unclear

A proofing unit should track:

- `unitId`
- `contextRange`
- `editableRanges`
- `revision`

`contextRange` is what the provider reads.

`editableRanges` is what suggestions are allowed to change.

### Suggestions

Suggestions need stable identity across rechecks.

Each suggestion should include:

- `suggestionId`
- `provider`
- `unitId`
- source fingerprint
- local range
- replacement
- status

Suggested status values:

- `Active`
- `Dismissed`
- `Applied`
- `Stale`

Suggested identity shape:

- provider
- unit id
- normalized source text fingerprint
- local span
- replacement fingerprint

This prevents dismissed suggestions from coming back unless the local unit has materially changed.

## Trusted Automation And `WM_COPYDATA`

The architecture must explicitly support RadEdit's automation workflow.

Rule:

- text inserted through trusted `WM_COPYDATA` flows should be considered already corrected

This means:

- skipping only the immediate proofing event is not enough
- the inserted region must be marked trusted in the document state
- later proofing passes must continue to skip it unless the user edits inside that region

### `SendCopyData` Template Scenario

A template may be inserted with trusted scaffold text and embedded slots:

`La patiente presente [] au sein gauche.`

After `SendCopyData`:

- `La patiente presente ` becomes `TrustedScaffold`
- `[]` becomes an explicit `EditableSlot`
- ` au sein gauche.` becomes `TrustedScaffold`

No proofing is scheduled for the scaffold.

If Dragon dictates into the slot:

- only that slot becomes dirty
- only the local proofing unit around that slot is scheduled
- the scaffold remains trusted context

This is one of the strongest reasons the architecture must be stateful.

## Proofing Rules

### What Gets Proofed

- new user-typed text
- dictated text
- text inside editable slots once filled
- text manually edited inside a previously trusted region

### What Does Not Get Proofed By Default

- scaffold inserted by trusted automation
- unchanged proofing units
- empty slots
- dismissed suggestion candidates whose local identity still matches

### Allowed Edit Range Filtering

Providers may see surrounding context, but RadEdit should only surface suggestions that touch allowed editable ranges.

That means:

- scaffold can provide context
- scaffold should not usually be editable
- slot policy decides whether limited spillover is allowed

## Provider Contract

The provider contract should be normalized and provider-agnostic.

Each request should include:

- unit text
- unit revision
- provider settings
- editable ranges inside the unit
- optional slot policy metadata

Each response should return:

- normalized suggestions
- provider diagnostics if needed

Important:

- the provider should not know about full-document history
- the provider should not own dismissal memory
- the provider should not decide whether a suggestion is still valid after later edits

That is RadEdit state, not provider state.

## Startup And Model Discovery

Startup should be asynchronous and resilient.

Rules:

- if no proofing config exists, use the compiled default server address
- probe server availability in the background
- query `/api/v1/models`
- if the server reports loaded instances, pick the first loaded model
- if no model has a loaded instance, show `No loaded model on server`
- never trigger server-side model loading implicitly from RadEdit
- expose the current model as read-only UI state
- persist user-selected server and auto-discovered model for future launches

The editor should remain usable even if the server is offline.

## UI Direction

The proofreading UI should reflect document state rather than drive it.

### Suggestions

- render only active suggestions
- preserve dismissed state until the underlying proofing unit changes
- update only affected ranges

### Apply

Applying a suggestion must go through one edit transaction that:

- replaces text
- remaps caret and selection
- restores focus to the editor
- invalidates only the affected local proofing units

### Hover Menu

The current hover-on-mousemove approach should be replaced or constrained.

Desired behavior:

- do not block text selection
- do not open aggressively while dragging or selecting
- prefer explicit click, right-click, keyboard action, or delayed hover with suppression

## Recommended Migration Path

The design should be implemented in phases so the application stays working during the refactor.

### Phase 1: Extract Session And Edit Provenance

- introduce `DocumentSession`
- normalize edits into transactions
- add edit source tracking
- keep existing provider behavior temporarily

### Phase 2: Trusted Regions And Slots

- mark trusted `SendCopyData` insertions as scaffold
- parse `[]` into explicit slots
- stop re-proofing trusted regions

### Phase 3: Incremental Proofing Units

- replace full-document proofing passes with local proofing units
- build units around changed text or changed slots
- move proofing work fully off the UI thread

### Phase 4: Suggestion Identity

- add stable suggestion ids
- persist dismissal and applied state by local fingerprint
- remove snapshot-only suggestion behavior

### Phase 5: Caret And Popup Cleanup

- centralize apply logic into one transaction path
- map selection/caret explicitly
- replace aggressive hover popup behavior

### Phase 6: Runtime Provider Discovery

- add server probe and model discovery service
- show current auto-discovered model in read-only UI
- persist user-chosen endpoint and auto-discovered model

## Decision Summary

The target solution is:

- stateful suggestion management
- stateful document/session model
- stateless proofing providers
- incremental local proofing
- explicit support for trusted automation and slot-based dictation templates

This is the architecture that best matches RadEdit's real usage model and resolves the current proofing issues without losing the existing automation workflow.
