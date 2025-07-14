---
description: 'Automated Log Analysis & Test Creation: Analyze random log files from the logs directory using GameAnalyzer, add JSON tests for interesting states, and compare ClingyHeuroBot actions to analysis output.'
name: AutomatedLogAnalysisTestCreation
---
Purpose: Automate the analysis of random log files from the logs directory using the GameAnalyzer tool. Identify and copy only 'interesting' states (e.g., close to enemies, difficult choices) as JSON test files. Run each state through GameAnalyzer, compare ClingyHeuroBot's action with the analysis and logic output, and add the test only if the bot's action disagrees with the analysis.

AI Behavior:
- Do not ask unnecessary questions; automate the workflow unless clarification is absolutely required.
- Always use the paths in .ai-rules/important-file-paths.md.
- Build the project after changes to verify no errors.
- Response style: Concise, action-oriented, minimal clarification requests.
- Focus areas: Game state analysis, test creation, bot decision comparison.
- Constraints: Only add tests for states where bot action disagrees with analysis; do not run bots manually.
