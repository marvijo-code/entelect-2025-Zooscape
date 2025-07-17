# 🚨 CRITICAL WORKFLOW REQUIREMENTS 🚨

## MANDATORY READING BEFORE ANY DEBUGGING/TESTING WORK

**BEFORE STARTING ANY DEBUGGING, TESTING, OR LOG ANALYSIS WORK:**

**YOU MUST FIRST READ THE ENTIRE FILE:** `.github/chatmodes/AutomatedLogAnalysisTestCreation.chatmode.md`

This file contains the complete workflow for:
- Log analysis using CaptureAnalysis tool
- Game state inspection using GameStateInspector
- Proper test creation methodology
- Debugging loop for resolving test failures
- API usage patterns and troubleshooting

**Failure to follow this workflow will result in inefficient debugging and missed critical steps.**

---

## Why This Workflow is Critical

### Real-World Example: StaticHeuro Bot Capture Avoidance Fix

The StaticHeuro bot capture avoidance fix demonstrates the importance of following the AutomatedLogAnalysisTestCreation.chatmode.md workflow:

#### Without the Workflow (Inefficient):
- Random debugging attempts
- Missing critical tools like CaptureAnalysis
- Creating tests without proper game state analysis
- Interface errors due to missing fallback mechanisms
- Wasted time on incorrect approaches

#### With the Workflow (Efficient):
1. **Step 1**: Used CaptureAnalysis tool to identify avoidable captures
2. **Step 2**: Used GameStateInspector to analyze specific game states
3. **Step 3**: Created targeted functional tests using proper methodology
4. **Step 4**: Applied debugging loop to resolve interface issues
5. **Step 5**: Verified fix with API-driven test execution

**Result**: Complete fix achieved efficiently with proper verification

---

## Key Tools and Their Purposes

### 1. CaptureAnalysis Tool
- **Purpose**: Identifies avoidable captures in match logs
- **Usage**: Analyzes entire match logs to find systematic bot failures
- **Critical for**: Root cause analysis of bot capture issues

### 2. GameStateInspector Tool
- **Purpose**: Analyzes specific game states and legal moves
- **Usage**: `cd tools/GameStateInspector && dotnet run -- <path-to-json-file> <bot-nickname>`
- **Critical for**: Understanding game context before creating tests

### 3. create_test.ps1 Script
- **Purpose**: Creates functional tests from game states
- **Usage**: Proper test creation with correct parameters
- **Critical for**: Consistent test generation and execution

### 4. TestController API
- **Purpose**: Executes individual tests via REST endpoints
- **Usage**: `POST /api/test/run/{testName}` for targeted test execution
- **Critical for**: Rapid test verification and debugging

### 5. Debugging Loop
- **Purpose**: Systematic approach to resolving test failures
- **Steps**: Analyze → Fix → Restart API → Re-run → Repeat
- **Critical for**: Efficient problem resolution

---

## Files Updated with This Requirement

The following files have been updated to emphasize this critical requirement:

### Documentation Files
- ✅ `README-TestCreation.md` - Added critical requirement section
- ✅ `docs/prompt-update-weights-from-logs.md` - Completely updated with workflow emphasis
- ✅ `CRITICAL-WORKFLOW-REQUIREMENTS.md` - This comprehensive reference file

### Memory Bank Files (.ai-rules/)
- ✅ `activeContext.md` - Updated with critical workflow requirement
- ✅ `systemPatterns.md` - Added critical requirement at top
- ✅ `important-file-paths.md` - Added AutomatedLogAnalysisTestCreation.chatmode.md as most important file

### Windsurf Rules
- ❌ `.windsurf/rules/.ai-rules.md` - Cannot edit due to access restrictions
- ❌ `.windsurf/rules/` directory - Cannot create new files due to access restrictions

---

## Success Metrics

Following the AutomatedLogAnalysisTestCreation.chatmode.md workflow results in:

1. **Efficient Problem Resolution**: Issues resolved systematically with proper tools
2. **Comprehensive Testing**: Targeted functional tests created with proper methodology
3. **Proper Verification**: API-driven test execution with debugging loop
4. **Complete Documentation**: All changes and findings properly documented
5. **Reproducible Results**: Consistent approach for future debugging tasks

---

## Remember: This is Not Optional

**This workflow requirement is MANDATORY for all bot debugging and testing tasks.**

The AutomatedLogAnalysisTestCreation.chatmode.md file contains battle-tested methodologies that have been proven effective through real-world debugging scenarios like the StaticHeuro bot capture avoidance fix.

**Always read the entire file before starting any related work.**
