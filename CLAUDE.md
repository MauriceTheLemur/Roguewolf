# CLAUDE.md

This file gives Claude (and other contributors) the context and rules needed to work on this Unity project safely and consistently. Fill in the bracketed sections with details specific to your project — the rest is a reusable framework for good Unity practice.

## Project Overview

- **What this game/app does:** This is a social deduction game based on the game Werewolf but with Rogue-like elements. The werewolf will try to hunt down all the Villagers, and the Villagers will try to vote or kill the werewolf, and by each night everyone will have a powerup or an action.  
- **Unity version:** [e.g. 2022.3.x LTS] — check `ProjectSettings/ProjectVersion.txt` before assuming.
- **Render pipeline:** URP
- **Target platforms:** PC
- **Key packages:** Netcode for GameObjects
- **Architecture pattern:** SOLID Principles, MVC, Good script abstraction with interfaces. Note that these are guidelines and may be broken if proven not to be effective or if there are more effective patterns that apply for a specific use case.  

## Project Structure

```
Assets/
  _Project/            # [or however you namespace your own content vs. imported packages]
    Scripts/
    Prefabs/
    Scenes/
    ScriptableObjects/
    Materials/
    Art/
    Audio/
  Plugins/              # third-party assets
Packages/                # UPM manifest & lock
ProjectSettings/
```

Note where things actually live if this differs — Claude should follow existing folder conventions rather than inventing new ones.

## Setup & Commands

| Task | Command |
|---|---|
| Open project | Open via Unity Hub, version pinned above |
| Run EditMode tests | `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results-edit.xml -logFile -` |
| Run PlayMode tests | `Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults results-play.xml -logFile -` |
| Build (CLI) | `[e.g. Unity -batchmode -projectPath . -executeMethod BuildScript.Build -quit -logFile -]` |
| Lint/format | `[e.g. dotnet format, or your IDE's C# analyzer config]` |

Adjust paths/flags for your actual CI setup. Always run tests headless via `-batchmode -nographics` in CI.

## C# Code Style

- Follow Microsoft's standard C# naming conventions: `PascalCase` for types, methods, and public members; `camelCase` for locals and parameters; `_camelCase` for private fields (or match whatever convention is already in the codebase).
- One `MonoBehaviour`/class per file, filename matching the class name.
- Use `[SerializeField] private` for fields exposed in the Inspector instead of making fields `public`, unless there's a specific reason.
- Cache component references (`GetComponent`, `transform`, etc.) in `Awake`/`Start` rather than calling them repeatedly, especially inside `Update`.
- Avoid `FindObjectOfType`, `GameObject.Find`, and `SendMessage` in hot paths — they're slow and fragile. Wire references via the Inspector, dependency injection, or a lightweight service locator/event system instead.
- Unsubscribe from events / `Action` delegates in `OnDisable`/`OnDestroy` to avoid leaks and null-reference callbacks on destroyed objects.
- Prefer `ScriptableObject`-based data/config over hardcoded constants or singletons where it improves reusability and designer access.
- Don't use `GameObject.Tag`/magic strings scattered through code — centralize as constants or use typed references.

## Performance

- Avoid per-frame heap allocations in `Update`, `FixedUpdate`, and `LateUpdate` — watch for LINQ, string concatenation, boxing, and `new` on hot paths. Profile with the Unity Profiler before optimizing, not instead of it.
- Use object pooling for frequently instantiated/destroyed objects (projectiles, VFX, enemies) rather than repeated `Instantiate`/`Destroy`.
- Batch physics/raycast calls where possible; prefer `Physics.OverlapSphereNonAlloc`-style non-allocating APIs in hot paths.
- Be deliberate about `Update()` usage — consider whether logic belongs in a coroutine, an event callback, or a centralized manager tick instead of every object polling every frame.
- Mind draw calls and batching (static/dynamic batching, GPU instancing) when adding new renderers or materials.
- Check texture import settings, mesh complexity, and audio compression against target platform budgets before committing new art/audio assets.

## Scenes & Prefabs

- Prefer prefabs (with nested prefabs / prefab variants) over scene-only objects for anything reused.
- Keep scenes focused; use additive scene loading for large levels rather than one monolithic scene.
- Don't leave orphaned/disabled test objects in scenes before committing.
- When editing a prefab, check whether the change should be an override in an instance or a change to the source prefab — avoid accidental instance overrides that silently diverge from the prefab.
- Avoid direct cross-scene references that break when scenes load out of order; use scene-independent references (ScriptableObjects, singletons/managers, addressables) instead.

## Testing Requirements

- Use the Unity Test Framework: EditMode tests for pure logic (no scene/play required), PlayMode tests for behavior that depends on the game loop, physics, or coroutines.
- New gameplay logic that can be isolated from `MonoBehaviour`/scene state should be — pure C# classes are easier to unit test than components.
- Bug fixes should include a regression test where feasible; if a bug can only be reproduced via manual play-testing, note the manual repro steps in the PR/commit instead.
- Run the relevant test suite before considering a task done.

## Version Control (Unity-specific)

- Ensure `.gitignore` excludes `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `.vs/`, `.idea/`, `UserSettings/` — these are regenerated locally and should never be committed.
- Set **Asset Serialization → Force Text** and **Version Control → Visible Meta Files** in Project Settings so scenes/prefabs/meta files are diffable and merge-friendly.
- Every asset needs its `.meta` file committed alongside it — never commit an asset without its meta, or vice versa.
- Use Git LFS (or Unity's Plastic/DevOps equivalent) for binary art/audio assets if the repo doesn't already have it configured.
- Scene and prefab merge conflicts are high-risk — prefer smaller, more frequent commits to scenes/prefabs, and avoid two people editing the same scene simultaneously where possible.
- Don't rename/move assets outside Unity's Project window (via raw filesystem or shell) — this breaks GUID references in `.meta` files. Use Unity's own rename/move so `.meta` files move with them, or if done via script, move the `.meta` file too.

## Before Committing / Finishing a Task

1. Project compiles with no console errors or warnings introduced by the change.
2. Relevant EditMode/PlayMode tests pass.
3. No leftover `Debug.Log` spam, commented-out code, or test/scratch GameObjects left in scenes.
4. Scene/prefab diffs reviewed — check for accidental transform drift, unintended Inspector value changes, or missing references (`Missing (Mono Script)`), which are common from merge conflicts or partial saves.
5. `.meta` files are present and committed for every new/moved/renamed asset.
6. Commit message explains the *why*, not just the *what*.

## Working Style / Guardrails for Claude

- If a change could affect scene/prefab serialization (adding/removing/reordering serialized fields on a `MonoBehaviour`), flag that existing scene data may need re-saving, and that reordering enum values or renaming serialized fields can silently break existing references — prefer `[FormerlySerializedAs]` when renaming.
- Don't restructure prefabs, scenes, or folder layout unless asked — these changes are expensive to review and easy to conflict on.
- Ask before introducing a new third-party package/asset dependency.
- When fixing a bug that only reproduces in the editor/at runtime, explain the suspected root cause and, where possible, add an EditMode/PlayMode test rather than only patching the symptom.
- Make the smallest change that correctly solves the problem; avoid unrelated refactors in the same pass.
- Don't push anything to the repository without consent

### Ask before editing — always propose first

- **Never apply an edit automatically.** Before creating, modifying, or deleting any file, describe the change and wait for an explicit "yes" from me.
- Propose in this shape: (1) which file(s), (2) what changes in plain terms, (3) why, (4) anything that could break. Show the code snippet if it helps, but don't write it to disk yet.
- If a task needs several edits, list the whole set up front and get approval for the plan, then confirm again before each file if the plan changes along the way.
- Read-only work (reading files, searching, running tests, explaining code) does not need approval — go ahead.
- If I say "go ahead" for a specific change, that approval covers only that change, not the next one.

### I'm learning — explain accordingly

- Assume I'm still learning Unity, C#, and netcode. Explain the *why* behind a suggestion, not just the code.
- Lead with a short plain-English summary (2–4 sentences) before any code or detail. I should be able to stop reading there and still understand what's happening.
- Introduce one concept at a time. Don't stack three unfamiliar patterns into one answer — pick the one that matters most now and mention the others exist as a "later" note.
- Define jargon the first time it appears in a session (e.g. "a ScriptableObject is a data container asset that lives in the project, not in a scene").
- Keep responses short by default. Depth on request: end with a brief offer like "want me to go deeper on X?" rather than pre-emptively dumping it.
- Prefer concrete examples from this project over abstract theory.
- When there's more than one reasonable approach, name the trade-off in one line each and give me your recommendation — don't hand me an exhaustive survey to sort out.
- It's fine to tell me something I'm asking for is a bad idea, but explain why in a way I can learn from.

## Project-Specific Notes

[Anything unique to this project: custom build pipeline, addressables setup, multiplayer/netcode considerations, performance budgets per platform, art pipeline quirks, areas Claude should never touch, etc.]
- When any code changes occur that spans 3 or more file changes, make sure to also generate a visual of how architecturally that code change affect the project and provide an explanation on why it is made, (optional) what problem it's fixing, and what the alternatives are.
 
