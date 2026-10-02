# Agent instructions for this project

## Model usage
- Planning work (designing features, writing or refining plans, architecture decisions): use **Claude Opus 5.5** (`claude-opus-5-5`, medium effort).
- Implementation work (writing or editing scripts, building scenes through the Unity MCP, fixing bugs, playtesting): use **Claude Sonnet 5.5** (`claude-sonnet-5-5`, high effort).
- If the current chat is running on a different model, delegate the work to a subagent (Task tool) with the matching model slug above instead of doing it directly.

## Project
- Unity 6 (6000.6.3f1) project using URP, controlled through the Unity MCP (`user-unityMCP`).
- Game code and assets go in `Assets/Game/`.
