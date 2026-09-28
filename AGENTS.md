# Agent instructions for this project

## Model usage
- Planning work (designing features, writing or refining plans, architecture decisions): use **Claude Opus 5.5 Medium** (`claude-opus-5-5-medium`).
- Implementation work (writing or editing scripts, building scenes through the Unity MCP, fixing bugs, playtesting): use **Grok 4.7 Extra High** (`grok-4.7-xhigh`).
- If the current chat is running on a different model, delegate the work to a subagent (Task tool) with the matching model slug above instead of doing it directly.

## Project
- Unity 6 (6000.6.3f1) project using URP, controlled through the Unity MCP (`user-unityMCP`).
- Game code and assets go in `Assets/Game/`.
