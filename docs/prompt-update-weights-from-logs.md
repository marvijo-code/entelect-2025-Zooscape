# Bot Weight Updates from Log Analysis

## 🚨 CRITICAL REQUIREMENT 🚨

**BEFORE STARTING ANY LOG ANALYSIS, DEBUGGING, OR TESTING WORK:**

**YOU MUST FIRST READ THE ENTIRE FILE:** `.github/chatmodes/AutomatedLogAnalysisTestCreation.chatmode.md`

This file contains the complete workflow for:
- Log analysis using CaptureAnalysis tool
- Game state inspection using GameStateInspector
- Proper test creation methodology
- Debugging loop for resolving test failures
- API usage patterns and troubleshooting

**Failure to follow this workflow will result in inefficient debugging and missed critical steps.**

---

## Overview

This document outlines the process for analyzing game logs to identify bot performance issues and update heuristic weights accordingly.

## Workflow

### 1. Log Analysis
- Use CaptureAnalysis tool to identify avoidable captures
- Use GameStateInspector to analyze specific game states
- Identify patterns in bot decision-making failures

### 2. Test Creation
- Create targeted functional tests for problematic scenarios
- Use `create_test.ps1` script for consistent test generation
- Focus on critical decision points and failure cases

### 3. Weight Adjustment
- Analyze heuristic scoring imbalances
- Adjust weights based on analysis findings
- Always restart API after weight changes

### 4. Verification
- Run functional tests to verify fixes
- Use API endpoints for individual test execution
- Follow debugging loop for any test failures

## Tools Required

- **CaptureAnalysis**: Identifies avoidable captures in match logs
- **GameStateInspector**: Analyzes specific game states and legal moves
- **create_test.ps1**: Creates functional tests from game states
- **TestController API**: Executes individual tests via REST endpoints

## Best Practices

1. Always follow the complete workflow in AutomatedLogAnalysisTestCreation.chatmode.md
2. Use GameStateInspector before creating tests to understand legal moves
3. Restart API after any code or weight changes
4. Create targeted tests for specific failure scenarios
5. Document all changes and findings