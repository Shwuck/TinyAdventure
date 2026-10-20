**Coding Rules**



1\. **Prioritise clarity over cleverness.** Prefer simple, familiar, readable solutions over clever or unusual ones.



2\. **Use honest, human-readable names.** Classes, methods, variables, files, scenes, and other identifiers should clearly describe what they are or what they do.



3\. **Keep methods focused.** A method should perform one clear job, and its name should describe that job accurately.



4\. **Separate intent from implementation.** High-level code should describe what is happening while lower-level methods handle how it happens.



5\. **Do not repeat code unnecessarily.** Shared behaviour should have one reusable implementation.



6\. **Do not duplicate knowledge.** Important rules, values, calculations, and conditions should have one authoritative implementation.



7\. **Give important state one source of truth.** Avoid storing the same mutable fact in multiple places when it can be derived from authoritative state.



8\. **Use authoritative mutation pathways.** Important state changes should go through a clear method or system rather than being modified independently by multiple callers.



9\. **Reuse operations regardless of their source.** Multiple causes of the same change should use the same pathway, such as all damage using `TakeDamage()`.



10\. **Let state owners control their state.** The object or system responsible for data should normally own its validation, mutation rules, limits, and required side effects.



11\. **Callers should describe what happened, not reproduce how it happens.** Request an operation rather than directly editing the fields or collections behind it.



12\. **Make invalid states difficult to create.** Protect important mutable data behind controlled methods or properties where appropriate.



13\. **Keep related logic together.** Code that changes together should normally live together.



14\. **Keep dependencies controlled.** Core gameplay logic should not depend unnecessarily on UI, audio, input, scenes, or other presentation systems.



15\. **Do not make UI authoritative for gameplay state.** UI should display state or request changes through the appropriate gameplay systems.



16\. **Reuse existing architecture before creating new architecture.** Check for an established system, method, data structure, or pattern before introducing another one.



17\. **Do not create competing systems for the same responsibility.** Extend or replace the established pathway rather than building a parallel implementation.



18\. **When replacing a pathway, migrate its legitimate callers.** Do not leave old and new implementations competing unless there is a deliberate reason.



19\. **Understand a system before changing it.** Check its important callers, ownership, data flow, and side effects before modifying its behaviour.



20\. **Do not assume existing code is live simply because it exists.** Trace important behaviour to its actual callers where this matters.



21\. **Design for foreseeable change, not hypothetical change.** Do not add abstractions or extensibility solely because they might be useful someday.



22\. **Keep code modular where it provides real value.** Systems should be made from focused, reusable parts without unnecessary fragmentation.



23\. **Avoid speculative complexity.** Do not introduce interfaces, factories, managers, wrappers, or generic frameworks without a current architectural need.



24\. **Do not refactor unrelated code during a focused change.** Adjacent improvements are appropriate only when they directly support the requested work.



25\. **Preserve existing behaviour unless changing it is intentional.** Refactoring should not quietly alter gameplay or system behaviour.



26\. **Fix causes rather than symptoms where practical.** Prefer correcting the underlying rule, invariant, or authoritative pathway over patching every visible consequence.



27\. **Validate inputs and assumptions.** Check important nulls, invalid values, missing dependencies, and impossible states before they cause hidden failures.



28\. **Fail clearly during development.** Unexpected states should produce useful diagnostic information rather than being silently ignored.



29\. **Use `GameDebugger` for project debugging.** Use `GameDebugger.Instance.LogInfo()`, `LogWarning()`, or `LogError()` instead of standard Unity debug logging.



30\. **Use `MessageLogManager` for relevant player-facing events.** Important gameplay outcomes should be visible to the player where appropriate.



31\. **Keep debugging and player messaging separate.** Debug logs must not substitute for proper gameplay communication.



32\. **Use the project's established persistence systems.** Do not create competing ways of storing persistent state.



33\. **Use `PermaLists` for persistent lists where appropriate.** Avoid unmanaged public mutable lists when persistent shared data requires `PermaLists`.



34\. **Avoid magic numbers and unexplained literals.** Give meaningful values names or place them in appropriate configuration data.



35\. **Expose tuning values where useful.** Gameplay values that benefit from adjustment should be configurable through the Inspector or the project's data systems where appropriate.



36\. **Use the project's established randomisation pathway**. Use `UnityEngine.Random` for normal Unity gameplay randomness unless deterministic or specialised behaviour requires otherwise.



37\. **Use Game Seed for Randomness \& fully qualify `UnityEngine.Random` where ambiguity is possible.** Avoid accidental confusion with `System.Random`. When possible, lean on the user provided Seed for all randomisation.



38\. **Prefer explicitness where ambiguity is risky.** Make calls, values, ownership, and conditions obvious when misunderstanding could cause bugs.



39\. **Avoid hidden or surprising side effects.** A method's important consequences should follow naturally from its responsibility and naming.



40\. **Comment the reason, not the obvious.** Comments should explain intent, constraints, trade-offs, warnings, or unusual decisions.



41\. **Keep comments accurate.** Update or remove comments when the behaviour they describe changes.



42\. **Use `#region` and `#endregion` to improve navigation.** Regions should organise readable code, not conceal oversized or poorly structured scripts.



43\. **Avoid risky characters in project data.** Do not use emoji or unsafe non-standard symbols in code, identifiers, serialized data, save-related strings, or generated files unless compatibility is confirmed.



44\. **Prefer maintainable code over short code.** Slightly more code is acceptable when it makes behaviour clearer and safer.



45\. **MOST IMPORTANTLY - Leave the codebase no worse than you found it.** Improve what is relevant to the task without expanding the scope unnecessarily.



