# SimArch structural change rules

Arch best-practice reminder for this codebase:

- **Do not** add/remove components or create/destroy entities *while iterating* a query.
- Prefer creating entities with all required components up-front.
- If a system needs structural changes based on iteration results:
  - collect ids into a temporary list, then apply changes after the loop, **or**
  - use the documented Arch mechanism (e.g., a command buffer / deferred actions) if we adopt one.

We also follow the explicit rule from the Arch docs:
- Check `entity.Has<T>()` before adding a component `T`.

See also: `SimArch/ArchEntityExtensions.cs`.
