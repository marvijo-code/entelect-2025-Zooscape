BOT LOGS:

[13:49:06 INF] Successfully connected on attempt 1.
[13:49:06 INF] SetBotId called. Received BotId: bbb82a7b-220d-4289-85a4-7b58bef3d0f7. Current BotId before set: 00000000-0000-0000-0000-000000000000
[13:49:06 INF] BotId after set: bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:12 INF] [PositionSync] BEFORE_ACTION T1 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(47,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:None LastAction:None LastTick:-1
[13:49:12 INF] [PositionSync] AFTER_ACTION T1 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Left CurrentPos:(47,3)
[13:49:12 INF] [PositionSync] EXPECTATION_SET T1 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Left ExpectedNextPos:(46,3)
[13:49:12 INF] T1 (47,3) Left 113ms 0pts
[13:49:12 INF] [PositionSync] BEFORE_ACTION T2 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(47,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(46, 3) LastAction:Left LastTick:1
[13:49:12 INF] [PositionSync] VERIFY T2 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(46,3) Actual:(47,3) Match:False AtSpawn:True
[13:49:12 INF] [PositionSync] AFTER_ACTION T2 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Left CurrentPos:(47,3)
[13:49:12 INF] [PositionSync] EXPECTATION_SET T2 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Left ExpectedNextPos:(46,3)
[13:49:12 INF] T2 (47,3) Left 50ms 0pts
[13:49:12 INF] [PositionSync] BEFORE_ACTION T3 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(46,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(46, 3) LastAction:Left LastTick:2
[13:49:12 INF] [PositionSync] VERIFY T3 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(46,3) Actual:(46,3) Match:True AtSpawn:False
[13:49:12 INF] [PositionSync] AFTER_ACTION T3 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Left CurrentPos:(46,3)
[13:49:12 INF] [PositionSync] EXPECTATION_SET T3 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Left ExpectedNextPos:(45,3)
[13:49:12 INF] T3 (46,3) Left 49ms 0pts
[13:49:12 INF] [PositionSync] BEFORE_ACTION T4 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(45,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(45, 3) LastAction:Left LastTick:3
[13:49:12 INF] [PositionSync] VERIFY T4 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(45,3) Actual:(45,3) Match:True AtSpawn:False
[13:49:12 INF] [PositionSync] AFTER_ACTION T4 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Down CurrentPos:(45,3)
[13:49:13 INF] [PositionSync] EXPECTATION_SET T4 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Down ExpectedNextPos:(45,4)
[13:49:13 WRN] [CRITICAL_TIMEOUT] T4 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 206ms - suppressing action
[13:49:13 WRN] SLOW ProcessState T4 Right 209ms - performance issue
[13:49:13 INF] T4 (45,3) Right 211ms 64pts
[13:49:13 WRN] SLOW T4 (45,3) Right 211ms - Performance issue!
[13:49:13 INF] [PositionSync] BEFORE_ACTION T5 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(44,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(46, 3) LastAction:Right LastTick:4
[13:49:13 INF] [PositionSync] VERIFY T5 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(46,3) Actual:(44,3) Match:False AtSpawn:False
[13:49:13 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Right on tick 4 expecting to move to (46, 3) but is actually at (44, 3) on tick 5
[13:49:13 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:13 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:13 INF] [PositionSync] AFTER_ACTION T5 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Left CurrentPos:(44,3)
[13:49:13 INF] [PositionSync] EXPECTATION_SET T5 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Left ExpectedNextPos:(43,3)
[13:49:13 INF] [TIMING_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 back under deadline (20ms < 180ms) - resuming normal heuristics
[13:49:13 INF] T5 (44,3) Left 22ms 198pts
[13:49:13 INF] [PositionSync] BEFORE_ACTION T6 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(43,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(43, 3) LastAction:Left LastTick:5
[13:49:13 INF] [PositionSync] VERIFY T6 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(43,3) Actual:(43,3) Match:True AtSpawn:False
[13:49:13 INF] [PositionSync] AFTER_ACTION T6 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Left CurrentPos:(43,3)
[13:49:13 INF] [PositionSync] EXPECTATION_SET T6 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Left ExpectedNextPos:(42,3)
[13:49:13 INF] T6 (43,3) Left 175ms 402pts
[13:49:13 INF] [PositionSync] BEFORE_ACTION T7 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(42,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(42, 3) LastAction:Left LastTick:6
[13:49:13 INF] [PositionSync] VERIFY T7 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(42,3) Actual:(42,3) Match:True AtSpawn:False
[13:49:13 INF] [PositionSync] AFTER_ACTION T7 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Left CurrentPos:(42,3)
[13:49:13 INF] [PositionSync] EXPECTATION_SET T7 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Left ExpectedNextPos:(41,3)
[13:49:13 WRN] [CRITICAL_TIMEOUT] T7 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 387ms - suppressing action
[13:49:14 WRN] SLOW ProcessState T7 Right 391ms - performance issue
[13:49:14 INF] T7 (42,3) Right 395ms 658pts
[13:49:14 WRN] SLOW T7 (42,3) Right 395ms - Performance issue!
[13:49:14 INF] [PositionSync] BEFORE_ACTION T8 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(41,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(43, 3) LastAction:Right LastTick:7
[13:49:14 INF] [PositionSync] VERIFY T8 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(43,3) Actual:(41,3) Match:False AtSpawn:False
[13:49:14 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Right on tick 7 expecting to move to (43, 3) but is actually at (41, 3) on tick 8
[13:49:14 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:14 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:14 INF] [PositionSync] AFTER_ACTION T8 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Left CurrentPos:(41,3)
[13:49:14 INF] [PositionSync] EXPECTATION_SET T8 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Left ExpectedNextPos:(40,3)
[13:49:14 WRN] [CRITICAL_TIMEOUT] T8 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 393ms - suppressing action
[13:49:14 WRN] SLOW ProcessState T8 Right 396ms - performance issue
[13:49:14 INF] T8 (41,3) Right 400ms 914pts
[13:49:14 WRN] SLOW T8 (41,3) Right 400ms - Performance issue!
[13:49:14 INF] [PositionSync] BEFORE_ACTION T9 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(40,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(42, 3) LastAction:Right LastTick:8
[13:49:14 INF] [PositionSync] VERIFY T9 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(42,3) Actual:(40,3) Match:False AtSpawn:False
[13:49:14 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Right on tick 8 expecting to move to (42, 3) but is actually at (40, 3) on tick 9
[13:49:14 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:14 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:14 INF] [PositionSync] AFTER_ACTION T9 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Left CurrentPos:(40,3)
[13:49:14 INF] [PositionSync] EXPECTATION_SET T9 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Left ExpectedNextPos:(39,3)
[13:49:14 WRN] [CRITICAL_TIMEOUT] T9 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 373ms - suppressing action
[13:49:14 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 3 consecutive late ticks - switching to essential heuristics only
[13:49:14 WRN] SLOW ProcessState T9 Right 379ms - performance issue
[13:49:14 INF] T9 (40,3) Right 383ms 1170pts
[13:49:14 WRN] SLOW T9 (40,3) Right 383ms - Performance issue!
[13:49:14 INF] [PositionSync] BEFORE_ACTION T10 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(39,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(41, 3) LastAction:Right LastTick:9
[13:49:14 INF] [PositionSync] VERIFY T10 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(41,3) Actual:(39,3) Match:False AtSpawn:False
[13:49:14 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Right on tick 9 expecting to move to (41, 3) but is actually at (39, 3) on tick 10
[13:49:15 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:15 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:15 INF] [PositionSync] AFTER_ACTION T10 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Left CurrentPos:(39,3)
[13:49:15 INF] [PositionSync] EXPECTATION_SET T10 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Left ExpectedNextPos:(38,3)
[13:49:15 WRN] [CRITICAL_TIMEOUT] T10 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 213ms - suppressing action
[13:49:15 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 4 consecutive late ticks - switching to essential heuristics only
[13:49:15 WRN] SLOW ProcessState T10 Right 218ms - performance issue
[13:49:15 INF] T10 (39,3) Right 224ms 1426pts
[13:49:15 WRN] SLOW T10 (39,3) Up 224ms - Performance issue!
[13:49:15 INF] [PositionSync] BEFORE_ACTION T11 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(40,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(40, 3) LastAction:Right LastTick:10
[13:49:15 INF] [PositionSync] VERIFY T11 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(40,3) Actual:(40,3) Match:True AtSpawn:False
[13:49:15 INF] [PositionSync] AFTER_ACTION T11 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Left CurrentPos:(40,3)
[13:49:15 INF] [PositionSync] EXPECTATION_SET T11 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Left ExpectedNextPos:(39,3)
[13:49:15 WRN] [CRITICAL_TIMEOUT] T11 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 206ms - suppressing action
[13:49:15 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 5 consecutive late ticks - switching to essential heuristics only
[13:49:15 WRN] SLOW ProcessState T11 Right 213ms - performance issue
[13:49:15 INF] T11 (40,3) Right 218ms 1426pts
[13:49:15 WRN] SLOW T11 (40,3) Up 218ms - Performance issue!
[13:49:15 INF] [PositionSync] BEFORE_ACTION T12 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(41,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(41, 3) LastAction:Right LastTick:11
[13:49:15 INF] [PositionSync] VERIFY T12 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(41,3) Actual:(41,3) Match:True AtSpawn:False
[13:49:15 INF] [PositionSync] AFTER_ACTION T12 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Up CurrentPos:(41,3)
[13:49:15 INF] [PositionSync] EXPECTATION_SET T12 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Up ExpectedNextPos:(41,2)
[13:49:15 WRN] [CRITICAL_TIMEOUT] T12 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 204ms - suppressing action
[13:49:15 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 6 consecutive late ticks - switching to essential heuristics only
[13:49:15 WRN] SLOW ProcessState T12 Right 211ms - performance issue
[13:49:15 INF] T12 (41,3) Right 214ms 1426pts
[13:49:15 WRN] SLOW T12 (41,3) Right 214ms - Performance issue!
[13:49:15 INF] [PositionSync] BEFORE_ACTION T13 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(42,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(42, 3) LastAction:Right LastTick:12
[13:49:16 INF] [PositionSync] VERIFY T13 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(42,3) Actual:(42,3) Match:True AtSpawn:False
[13:49:16 INF] [PositionSync] AFTER_ACTION T13 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Right CurrentPos:(42,3)
[13:49:16 INF] [PositionSync] EXPECTATION_SET T13 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Right ExpectedNextPos:(43,3)
[13:49:16 WRN] [CRITICAL_TIMEOUT] T13 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 216ms - suppressing action
[13:49:16 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 7 consecutive late ticks - switching to essential heuristics only
[13:49:16 WRN] SLOW ProcessState T13 Right 391ms - performance issue
[13:49:16 INF] T13 (42,3) Right 395ms 1426pts
[13:49:16 WRN] SLOW T13 (42,3) Right 395ms - Performance issue!
[13:49:16 INF] [PositionSync] BEFORE_ACTION T14 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(43,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(43, 3) LastAction:Right LastTick:13
[13:49:16 INF] [PositionSync] VERIFY T14 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(43,3) Actual:(43,3) Match:True AtSpawn:False
[13:49:16 INF] [PositionSync] AFTER_ACTION T14 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Right CurrentPos:(43,3)
[13:49:16 INF] [PositionSync] EXPECTATION_SET T14 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Right ExpectedNextPos:(44,3)
[13:49:16 WRN] [CRITICAL_TIMEOUT] T14 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 209ms - suppressing action
[13:49:16 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 8 consecutive late ticks - switching to essential heuristics only
[13:49:16 WRN] SLOW ProcessState T14 Right 216ms - performance issue
[13:49:16 INF] T14 (43,3) Right 220ms 1426pts
[13:49:16 WRN] SLOW T14 (43,3) Right 220ms - Performance issue!
[13:49:16 INF] [PositionSync] BEFORE_ACTION T15 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(44,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(44, 3) LastAction:Right LastTick:14
[13:49:16 INF] [PositionSync] VERIFY T15 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(44,3) Actual:(44,3) Match:True AtSpawn:False
[13:49:16 INF] [PositionSync] AFTER_ACTION T15 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Right CurrentPos:(44,3)
[13:49:16 INF] [PositionSync] EXPECTATION_SET T15 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Right ExpectedNextPos:(45,3)
[13:49:16 INF] [TIMING_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 back under deadline (29ms < 180ms) - resuming normal heuristics
[13:49:16 WRN] SLOW ProcessState T15 Right 207ms - performance issue
[13:49:16 INF] T15 (44,3) Right 210ms 1426pts
[13:49:16 WRN] SLOW T15 (44,3) Right 210ms - Performance issue!
[13:49:16 INF] [PositionSync] BEFORE_ACTION T16 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(45,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(45, 3) LastAction:Right LastTick:15
[13:49:16 INF] [PositionSync] VERIFY T16 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(45,3) Actual:(45,3) Match:True AtSpawn:False
[13:49:17 INF] [PositionSync] AFTER_ACTION T16 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Down CurrentPos:(45,3)
[13:49:17 INF] [PositionSync] EXPECTATION_SET T16 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Down ExpectedNextPos:(45,4)
[13:49:17 WRN] [CRITICAL_TIMEOUT] T16 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 207ms - suppressing action
[13:49:17 WRN] SLOW ProcessState T16 Right 210ms - performance issue
[13:49:17 INF] T16 (45,3) Right 212ms 1426pts
[13:49:17 WRN] SLOW T16 (45,3) Right 212ms - Performance issue!
[13:49:17 INF] [PositionSync] BEFORE_ACTION T17 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(46,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(46, 3) LastAction:Right LastTick:16
[13:49:17 INF] [PositionSync] VERIFY T17 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(46,3) Actual:(46,3) Match:True AtSpawn:False
[13:49:17 INF] [PositionSync] AFTER_ACTION T17 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Up CurrentPos:(46,3)
[13:49:17 INF] [PositionSync] EXPECTATION_SET T17 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Up ExpectedNextPos:(46,2)
[13:49:17 WRN] [CRITICAL_TIMEOUT] T17 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 183ms - suppressing action
[13:49:17 WRN] SLOW ProcessState T17 Right 186ms - performance issue
[13:49:17 INF] T17 (46,3) Right 189ms 1426pts
[13:49:17 WRN] SLOW T17 (46,3) Right 189ms - Performance issue!
[13:49:17 INF] [PositionSync] BEFORE_ACTION T18 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(46,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(47, 3) LastAction:Right LastTick:17
[13:49:17 INF] [PositionSync] VERIFY T18 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(47,3) Actual:(46,3) Match:False AtSpawn:False
[13:49:17 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Right on tick 17 expecting to move to (47, 3) but is actually at (46, 3) on tick 18
[13:49:17 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:17 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:17 INF] [PositionSync] AFTER_ACTION T18 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Up CurrentPos:(46,3)
[13:49:17 INF] [PositionSync] EXPECTATION_SET T18 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Up ExpectedNextPos:(46,2)
[13:49:17 WRN] [CRITICAL_TIMEOUT] T18 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 200ms - suppressing action
[13:49:17 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 3 consecutive late ticks - switching to essential heuristics only
[13:49:17 WRN] SLOW ProcessState T18 Right 206ms - performance issue
[13:49:17 INF] T18 (46,3) Right 208ms 1426pts
[13:49:17 WRN] SLOW T18 (46,3) Right 208ms - Performance issue!
[13:49:17 INF] [PositionSync] BEFORE_ACTION T19 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(46,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(47, 3) LastAction:Right LastTick:18
[13:49:17 INF] [PositionSync] VERIFY T19 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(47,3) Actual:(46,3) Match:False AtSpawn:False
[13:49:17 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Right on tick 18 expecting to move to (47, 3) but is actually at (46, 3) on tick 19
[13:49:17 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:17 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:17 INF] [PositionSync] AFTER_ACTION T19 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Up CurrentPos:(46,3)
[13:49:17 INF] [PositionSync] EXPECTATION_SET T19 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Up ExpectedNextPos:(46,2)
[13:49:17 INF] [TIMING_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 back under deadline (22ms < 180ms) - resuming normal heuristics
[13:49:17 INF] T19 (46,3) Up 51ms 1426pts
[13:49:17 INF] [PositionSync] BEFORE_ACTION T20 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(46,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(46, 2) LastAction:Up LastTick:19
[13:49:17 INF] [PositionSync] VERIFY T20 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(46,2) Actual:(46,3) Match:False AtSpawn:False
[13:49:17 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Up on tick 19 expecting to move to (46, 2) but is actually at (46, 3) on tick 20
[13:49:17 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:17 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:17 INF] [PositionSync] AFTER_ACTION T20 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Up CurrentPos:(46,3)
[13:49:17 INF] [PositionSync] EXPECTATION_SET T20 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Up ExpectedNextPos:(46,2)
[13:49:17 INF] T20 (46,3) Up 140ms 1426pts
[13:49:17 INF] [PositionSync] BEFORE_ACTION T21 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(46,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(46, 2) LastAction:Up LastTick:20
[13:49:17 INF] [PositionSync] VERIFY T21 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(46,2) Actual:(46,3) Match:False AtSpawn:False
[13:49:17 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Up on tick 20 expecting to move to (46, 2) but is actually at (46, 3) on tick 21
[13:49:17 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:17 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:17 INF] [PositionSync] AFTER_ACTION T21 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Up CurrentPos:(46,3)
[13:49:18 INF] [PositionSync] EXPECTATION_SET T21 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Up ExpectedNextPos:(46,2)
[13:49:18 WRN] [CRITICAL_TIMEOUT] T21 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 183ms - suppressing action
[13:49:18 WRN] SLOW ProcessState T21 Right 186ms - performance issue
[13:49:18 INF] T21 (46,3) Right 189ms 1426pts
[13:49:18 WRN] SLOW T21 (46,3) Right 189ms - Performance issue!
[13:49:18 INF] [PositionSync] BEFORE_ACTION T22 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(46,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(47, 3) LastAction:Right LastTick:21
[13:49:18 INF] [PositionSync] VERIFY T22 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(47,3) Actual:(46,3) Match:False AtSpawn:False
[13:49:18 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Right on tick 21 expecting to move to (47, 3) but is actually at (46, 3) on tick 22
[13:49:18 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:18 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:18 INF] [PositionSync] AFTER_ACTION T22 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Up CurrentPos:(46,3)
[13:49:18 INF] [PositionSync] EXPECTATION_SET T22 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Up ExpectedNextPos:(46,2)
[13:49:18 WRN] [CRITICAL_TIMEOUT] T22 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 374ms - suppressing action
[13:49:18 WRN] SLOW ProcessState T22 Right 378ms - performance issue
[13:49:18 INF] T22 (46,3) Right 381ms 1426pts
[13:49:18 WRN] SLOW T22 (46,3) Right 381ms - Performance issue!
[13:49:18 INF] [PositionSync] BEFORE_ACTION T23 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(46,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(47, 3) LastAction:Right LastTick:22
[13:49:18 INF] [PositionSync] VERIFY T23 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(47,3) Actual:(46,3) Match:False AtSpawn:False
[13:49:18 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Right on tick 22 expecting to move to (47, 3) but is actually at (46, 3) on tick 23
[13:49:18 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:18 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:18 INF] [PositionSync] AFTER_ACTION T23 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Up CurrentPos:(46,3)
[13:49:18 INF] [PositionSync] EXPECTATION_SET T23 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Up ExpectedNextPos:(46,2)
[13:49:18 WRN] [CRITICAL_TIMEOUT] T23 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 375ms - suppressing action
[13:49:18 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 3 consecutive late ticks - switching to essential heuristics only
[13:49:18 WRN] SLOW ProcessState T23 Right 381ms - performance issue
[13:49:18 INF] T23 (46,3) Right 384ms 1426pts
[13:49:18 WRN] SLOW T23 (46,3) Right 384ms - Performance issue!
[13:49:18 INF] [PositionSync] BEFORE_ACTION T24 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(46,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(47, 3) LastAction:Right LastTick:23
[13:49:19 INF] [PositionSync] VERIFY T24 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(47,3) Actual:(46,3) Match:False AtSpawn:False
[13:49:19 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Right on tick 23 expecting to move to (47, 3) but is actually at (46, 3) on tick 24
[13:49:19 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:19 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:19 INF] [PositionSync] AFTER_ACTION T24 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Up CurrentPos:(46,3)
[13:49:19 INF] [PositionSync] EXPECTATION_SET T24 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Up ExpectedNextPos:(46,2)
[13:49:19 WRN] [CRITICAL_TIMEOUT] T24 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 380ms - suppressing action
[13:49:19 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 4 consecutive late ticks - switching to essential heuristics only
[13:49:19 WRN] SLOW ProcessState T24 Right 386ms - performance issue
[13:49:19 INF] T24 (46,3) Right 390ms 1426pts
[13:49:19 WRN] SLOW T24 (46,3) Right 390ms - Performance issue!
[13:49:19 INF] [PositionSync] BEFORE_ACTION T25 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(46,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(47, 3) LastAction:Right LastTick:24
[13:49:19 INF] [PositionSync] VERIFY T25 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(47,3) Actual:(46,3) Match:False AtSpawn:False
[13:49:19 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Right on tick 24 expecting to move to (47, 3) but is actually at (46, 3) on tick 25
[13:49:19 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:19 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:19 INF] [PositionSync] AFTER_ACTION T25 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Up CurrentPos:(46,3)
[13:49:19 INF] [PositionSync] EXPECTATION_SET T25 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Up ExpectedNextPos:(46,2)
[13:49:19 WRN] [CRITICAL_TIMEOUT] T25 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 383ms - suppressing action
[13:49:19 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 5 consecutive late ticks - switching to essential heuristics only
[13:49:19 WRN] SLOW ProcessState T25 Right 389ms - performance issue
[13:49:19 INF] T25 (46,3) Right 392ms 1426pts
[13:49:19 WRN] SLOW T25 (46,3) Right 392ms - Performance issue!
[13:49:19 INF] [PositionSync] BEFORE_ACTION T26 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(46,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(47, 3) LastAction:Right LastTick:25
[13:49:19 INF] [PositionSync] VERIFY T26 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(47,3) Actual:(46,3) Match:False AtSpawn:False
[13:49:19 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Right on tick 25 expecting to move to (47, 3) but is actually at (46, 3) on tick 26
[13:49:19 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:19 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:19 INF] [PositionSync] AFTER_ACTION T26 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Up CurrentPos:(46,3)
[13:49:19 INF] [PositionSync] EXPECTATION_SET T26 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Up ExpectedNextPos:(46,2)
[13:49:19 WRN] [CRITICAL_TIMEOUT] T26 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 198ms - suppressing action
[13:49:19 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 6 consecutive late ticks - switching to essential heuristics only
[13:49:19 WRN] SLOW ProcessState T26 Right 333ms - performance issue
[13:49:20 INF] T26 (46,3) Right 382ms 1426pts
[13:49:20 WRN] SLOW T26 (46,3) Right 382ms - Performance issue!
[13:49:20 INF] [PositionSync] BEFORE_ACTION T27 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(46,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(47, 3) LastAction:Right LastTick:26
[13:49:20 INF] [PositionSync] VERIFY T27 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(47,3) Actual:(46,3) Match:False AtSpawn:False
[13:49:20 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Right on tick 26 expecting to move to (47, 3) but is actually at (46, 3) on tick 27
[13:49:20 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:20 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:20 INF] [PositionSync] AFTER_ACTION T27 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Up CurrentPos:(46,3)
[13:49:20 INF] [PositionSync] EXPECTATION_SET T27 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Up ExpectedNextPos:(46,2)
[13:49:20 WRN] [CRITICAL_TIMEOUT] T27 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 198ms - suppressing action
[13:49:20 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 7 consecutive late ticks - switching to essential heuristics only
[13:49:20 WRN] SLOW ProcessState T27 Right 204ms - performance issue
[13:49:20 INF] T27 (46,3) Right 207ms 1426pts
[13:49:20 WRN] SLOW T27 (46,3) Right 207ms - Performance issue!
[13:49:20 INF] [PositionSync] BEFORE_ACTION T28 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(46,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(47, 3) LastAction:Right LastTick:27
[13:49:20 INF] [PositionSync] VERIFY T28 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(47,3) Actual:(46,3) Match:False AtSpawn:False
[13:49:20 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Right on tick 27 expecting to move to (47, 3) but is actually at (46, 3) on tick 28
[13:49:20 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:20 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:20 INF] [PositionSync] AFTER_ACTION T28 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Up CurrentPos:(46,3)
[13:49:20 INF] [PositionSync] EXPECTATION_SET T28 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Up ExpectedNextPos:(46,2)
[13:49:20 WRN] [CRITICAL_TIMEOUT] T28 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 191ms - suppressing action
[13:49:20 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 8 consecutive late ticks - switching to essential heuristics only
[13:49:20 WRN] SLOW ProcessState T28 Right 366ms - performance issue
[13:49:20 INF] T28 (46,3) Right 369ms 1426pts
[13:49:20 WRN] SLOW T28 (46,3) Right 369ms - Performance issue!
[13:49:20 INF] [PositionSync] BEFORE_ACTION T29 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(46,3) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(47, 3) LastAction:Right LastTick:28
[13:49:20 INF] [PositionSync] VERIFY T29 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(47,3) Actual:(46,3) Match:False AtSpawn:False
[13:49:20 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Right on tick 28 expecting to move to (47, 3) but is actually at (46, 3) on tick 29
[13:49:20 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:20 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:20 INF] [PositionSync] AFTER_ACTION T29 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Up CurrentPos:(46,3)
[13:49:20 INF] [PositionSync] EXPECTATION_SET T29 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Up ExpectedNextPos:(46,2)
[13:49:20 WRN] [CRITICAL_TIMEOUT] T29 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 206ms - suppressing action
[13:49:21 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 9 consecutive late ticks - switching to essential heuristics only
[13:49:21 WRN] SLOW ProcessState T29 Right 387ms - performance issue
[13:49:21 INF] T29 (46,3) Right 390ms 1426pts
[13:49:21 WRN] SLOW T29 (46,3) Right 390ms - Performance issue!
[13:49:21 INF] [PositionSync] BEFORE_ACTION T30 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(46,2) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(47, 3) LastAction:Right LastTick:29
[13:49:21 INF] [PositionSync] VERIFY T30 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(47,3) Actual:(46,2) Match:False AtSpawn:False
[13:49:21 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Right on tick 29 expecting to move to (47, 3) but is actually at (46, 2) on tick 30
[13:49:21 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:21 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:21 INF] [PositionSync] AFTER_ACTION T30 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Up CurrentPos:(46,2)
[13:49:21 INF] [PositionSync] EXPECTATION_SET T30 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Up ExpectedNextPos:(46,1)
[13:49:21 WRN] [CRITICAL_TIMEOUT] T30 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 186ms - suppressing action
[13:49:21 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 10 consecutive late ticks - switching to essential heuristics only
[13:49:21 WRN] SLOW ProcessState T30 Right 191ms - performance issue
[13:49:21 INF] T30 (46,2) Right 193ms 1426pts
[13:49:21 WRN] SLOW T30 (46,2) Right 193ms - Performance issue!
[13:49:21 INF] [PositionSync] BEFORE_ACTION T31 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(46,1) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(47, 2) LastAction:Right LastTick:30
[13:49:21 INF] [PositionSync] VERIFY T31 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(47,2) Actual:(46,1) Match:False AtSpawn:False
[13:49:21 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Right on tick 30 expecting to move to (47, 2) but is actually at (46, 1) on tick 31
[13:49:21 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:21 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:21 INF] [PositionSync] AFTER_ACTION T31 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Left CurrentPos:(46,1)
[13:49:21 INF] [PositionSync] EXPECTATION_SET T31 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Left ExpectedNextPos:(45,1)
[13:49:21 WRN] [CRITICAL_TIMEOUT] T31 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 239ms - suppressing action
[13:49:21 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 11 consecutive late ticks - switching to essential heuristics only
[13:49:21 WRN] SLOW ProcessState T31 Right 246ms - performance issue
[13:49:21 INF] T31 (46,1) Right 248ms 1490pts
[13:49:21 WRN] SLOW T31 (46,1) Up 248ms - Performance issue!
[13:49:21 INF] [PositionSync] BEFORE_ACTION T32 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(47,1) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(47, 1) LastAction:Right LastTick:31
[13:49:21 INF] [PositionSync] VERIFY T32 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(47,1) Actual:(47,1) Match:True AtSpawn:False
[13:49:21 INF] [PositionSync] AFTER_ACTION T32 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Left CurrentPos:(47,1)
[13:49:21 INF] [PositionSync] EXPECTATION_SET T32 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Left ExpectedNextPos:(46,1)
[13:49:21 WRN] [CRITICAL_TIMEOUT] T32 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 190ms - suppressing action
[13:49:21 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 12 consecutive late ticks - switching to essential heuristics only
[13:49:21 WRN] SLOW ProcessState T32 Right 195ms - performance issue
[13:49:21 INF] T32 (47,1) Right 196ms 1624pts
[13:49:21 WRN] SLOW T32 (47,1) Up 196ms - Performance issue!
[13:49:21 INF] [PositionSync] BEFORE_ACTION T33 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(48,1) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(48, 1) LastAction:Right LastTick:32
[13:49:22 INF] [PositionSync] VERIFY T33 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(48,1) Actual:(48,1) Match:True AtSpawn:False
[13:49:22 INF] [PositionSync] AFTER_ACTION T33 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Right CurrentPos:(48,1)
[13:49:22 INF] [PositionSync] EXPECTATION_SET T33 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Right ExpectedNextPos:(49,1)
[13:49:22 INF] [TIMING_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 back under deadline (21ms < 180ms) - resuming normal heuristics
[13:49:22 WRN] SLOW ProcessState T33 Right 198ms - performance issue
[13:49:22 INF] T33 (48,1) Right 201ms 1828pts
[13:49:22 WRN] SLOW T33 (48,1) Right 201ms - Performance issue!
[13:49:22 INF] [PositionSync] BEFORE_ACTION T34 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(49,1) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(49, 1) LastAction:Right LastTick:33
[13:49:22 INF] [PositionSync] VERIFY T34 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(49,1) Actual:(49,1) Match:True AtSpawn:False
[13:49:22 INF] [PositionSync] AFTER_ACTION T34 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Down CurrentPos:(49,1)
[13:49:22 INF] [PositionSync] EXPECTATION_SET T34 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Down ExpectedNextPos:(49,2)
[13:49:22 WRN] [CRITICAL_TIMEOUT] T34 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 182ms - suppressing action
[13:49:22 WRN] SLOW ProcessState T34 Down 186ms - performance issue
[13:49:22 INF] T34 (49,1) Down 189ms 2084pts
[13:49:22 WRN] SLOW T34 (49,1) Down 189ms - Performance issue!
[13:49:22 INF] [PositionSync] BEFORE_ACTION T35 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(49,1) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(49, 2) LastAction:Down LastTick:34
[13:49:22 INF] [PositionSync] VERIFY T35 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(49,2) Actual:(49,1) Match:False AtSpawn:False
[13:49:22 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Down on tick 34 expecting to move to (49, 2) but is actually at (49, 1) on tick 35
[13:49:22 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:22 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:22 INF] [PositionSync] AFTER_ACTION T35 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Down CurrentPos:(49,1)
[13:49:22 INF] [PositionSync] EXPECTATION_SET T35 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Down ExpectedNextPos:(49,2)
[13:49:22 WRN] [CRITICAL_TIMEOUT] T35 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 375ms - suppressing action
[13:49:22 WRN] SLOW ProcessState T35 Down 378ms - performance issue
[13:49:22 INF] T35 (49,1) Down 382ms 2084pts
[13:49:22 WRN] SLOW T35 (49,1) Down 382ms - Performance issue!
[13:49:22 INF] [PositionSync] BEFORE_ACTION T36 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(49,1) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(49, 2) LastAction:Down LastTick:35
[13:49:22 INF] [PositionSync] VERIFY T36 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(49,2) Actual:(49,1) Match:False AtSpawn:False
[13:49:22 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Down on tick 35 expecting to move to (49, 2) but is actually at (49, 1) on tick 36
[13:49:22 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:22 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:22 INF] [PositionSync] AFTER_ACTION T36 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Down CurrentPos:(49,1)
[13:49:23 INF] [PositionSync] EXPECTATION_SET T36 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Down ExpectedNextPos:(49,2)
[13:49:23 WRN] [CRITICAL_TIMEOUT] T36 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 399ms - suppressing action
[13:49:23 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 3 consecutive late ticks - switching to essential heuristics only
[13:49:23 WRN] SLOW ProcessState T36 Down 405ms - performance issue
[13:49:23 INF] T36 (49,1) Down 407ms 2084pts
[13:49:23 WRN] SLOW T36 (49,1) Down 407ms - Performance issue!
[13:49:23 INF] [PositionSync] BEFORE_ACTION T37 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(49,1) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(49, 2) LastAction:Down LastTick:36
[13:49:23 INF] [PositionSync] VERIFY T37 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(49,2) Actual:(49,1) Match:False AtSpawn:False
[13:49:23 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Down on tick 36 expecting to move to (49, 2) but is actually at (49, 1) on tick 37
[13:49:23 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:23 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:23 INF] [PositionSync] AFTER_ACTION T37 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Down CurrentPos:(49,1)
[13:49:23 INF] [PositionSync] EXPECTATION_SET T37 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Down ExpectedNextPos:(49,2)
[13:49:23 WRN] [CRITICAL_TIMEOUT] T37 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 366ms - suppressing action
[13:49:23 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 4 consecutive late ticks - switching to essential heuristics only
[13:49:23 WRN] SLOW ProcessState T37 Down 373ms - performance issue
[13:49:23 INF] T37 (49,1) Down 376ms 2084pts
[13:49:23 WRN] SLOW T37 (49,1) Down 376ms - Performance issue!
[13:49:23 INF] [PositionSync] BEFORE_ACTION T38 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(49,1) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(49, 2) LastAction:Down LastTick:37
[13:49:23 INF] [PositionSync] VERIFY T38 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(49,2) Actual:(49,1) Match:False AtSpawn:False
[13:49:23 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Down on tick 37 expecting to move to (49, 2) but is actually at (49, 1) on tick 38
[13:49:23 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:23 INF] [PositionSync] RECOVERY_COMPLETE: Cleared visit counts (0), quadrants (0), and position history for bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7
[13:49:23 INF] [PositionSync] AFTER_ACTION T38 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 ChosenAction:Down CurrentPos:(49,1)
[13:49:23 INF] [PositionSync] EXPECTATION_SET T38 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Action:Down ExpectedNextPos:(49,2)
[13:49:23 WRN] [CRITICAL_TIMEOUT] T38 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 exceeded 180ms deadline with 188ms - suppressing action
[13:49:23 WRN] [LATE_TICK_RECOVERY] Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 had 5 consecutive late ticks - switching to essential heuristics only
[13:49:23 WRN] SLOW ProcessState T38 Down 194ms - performance issue
[13:49:23 INF] T38 (49,1) Down 196ms 2084pts
[13:49:23 WRN] SLOW T38 (49,1) Up 196ms - Performance issue!
[13:49:23 INF] [PositionSync] BEFORE_ACTION T39 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Pos:(49,1) Spawn:(47,3) HeldPowerUp:None ExpectedPos:(49, 2) LastAction:Down LastTick:38
[13:49:23 INF] [PositionSync] VERIFY T39 Bot:bbb82a7b-220d-4289-85a4-7b58bef3d0f7 Expected:(49,2) Actual:(49,1) Match:False AtSpawn:False
[13:49:23 WRN] [PositionSync] DISCREPANCY! Bot bbb82a7b-220d-4289-85a4-7b58bef3d0f7 sent Down on tick 38 expecting to move to (49, 2) but is actually at (49, 1) on tick 39
[13:49:23 INF] [PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state
[13:49:23 INF] [PositionS


Engine logs:
[13:49:05 INF] Zookeeper (Stupid Ol' Muppet) added to game world.
[13:49:06 INF] Bot connected: 2RBREQIX58nP98jNmDnq9g
[13:49:06 INF] Bot connected: jWNfrquLqvVQiO2rClpHBg
[13:49:06 INF] Bot connected: bdjLuWmnq6uTH8I4WzxpFQ
[13:49:06 INF] Animal (AdvancedMCTSBot) added to game world.
[13:49:06 INF] Animal (ClingyHeuroBot) added to game world.
[13:49:06 INF] Animal (StaticHeuro) added to game world.
[13:49:11 INF] Bot connected: V_ZLcKGLPDBNYZVNjYc-ng
[13:49:11 INF] Animal (ClingyHeuroBot2) added to game world.
[13:49:12 INF] Game tick 1, Duration = 170.14 / 200, Duty Cycle = 0.8507, Positions = [StaticHeuro@(47,3), ClingyHeuroBot@(3,47), ClingyHeuroBot2@(47,47), AdvancedMCTSBot@(3,3)]
[13:49:12 INF] Game tick 2, Duration = 78.70 / 200, Duty Cycle = 0.3935, Positions = [StaticHeuro@(47,3), ClingyHeuroBot@(3,47), ClingyHeuroBot2@(47,47), AdvancedMCTSBot@(3,3)]
[13:49:12 INF] Command (Right) enqueued for bot (ClingyHeuroBot2). Queue length: 1
[13:49:12 INF] Command (Right) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:12 INF] Command (Left) enqueued for bot (StaticHeuro). Queue length: 1
[13:49:12 INF] Command (Down) enqueued for bot (ClingyHeuroBot). Queue length: 1
[13:49:12 INF] Game tick 3, Duration = 48.41 / 200, Duty Cycle = 0.2420, Positions = [StaticHeuro@(46,3), ClingyHeuroBot@(3,48), ClingyHeuroBot2@(48,47), AdvancedMCTSBot@(4,3)]
[13:49:12 INF] Command (Down) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:12 INF] Respawning pellet at (49, 47) in 92 ticks
[13:49:12 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 1
[13:49:12 INF] Command (Up) enqueued for bot (ClingyHeuroBot2). Queue length: 1
[13:49:12 INF] Respawning pellet at (45, 3) in 113 ticks
[13:49:12 INF] Command (Right) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:12 INF] Respawning pellet at (3, 49) in 92 ticks
[13:49:12 INF] Game tick 4, Duration = 132.55 / 200, Duty Cycle = 0.6627, Positions = [StaticHeuro@(45,3), ClingyHeuroBot@(3,49), ClingyHeuroBot2@(49,47), AdvancedMCTSBot@(4,4)]
[13:49:13 INF] Respawning pellet at (44, 3) in 95 ticks
[13:49:13 INF] Command (Up) enqueued for bot (ClingyHeuroBot2). Queue length: 1
[13:49:13 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 1
[13:49:13 INF] Respawning pellet at (4, 49) in 107 ticks
[13:49:13 INF] Respawning pellet at (49, 46) in 102 ticks
[13:49:13 INF] Respawning pellet at (5, 4) in 109 ticks
[13:49:13 INF] Command (Right) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:13 INF] Game tick 5, Duration = 169.20 / 200, Duty Cycle = 0.8460, Positions = [StaticHeuro@(44,3), ClingyHeuroBot@(4,49), ClingyHeuroBot2@(49,46), AdvancedMCTSBot@(5,4)]
[13:49:13 INF] Respawning pellet at (43, 3) in 119 ticks
[13:49:13 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 1
[13:49:13 INF] Command (Up) enqueued for bot (ClingyHeuroBot2). Queue length: 1
[13:49:13 INF] Respawning pellet at (49, 45) in 108 ticks
[13:49:13 INF] Command (Down) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:13 INF] Respawning pellet at (5, 49) in 104 ticks
[13:49:13 WRN] Game tick 6, Duration = 206.17 / 200, Duty Cycle = 1.0308, Positions = [StaticHeuro@(43,3), ClingyHeuroBot@(5,49), ClingyHeuroBot2@(49,45), AdvancedMCTSBot@(5,4)]
[13:49:13 INF] Respawning pellet at (42, 3) in 105 ticks
[13:49:13 INF] Command (Up) enqueued for bot (ClingyHeuroBot). Queue length: 1
[13:49:13 INF] Command (Left) enqueued for bot (ClingyHeuroBot2). Queue length: 1
[13:49:13 INF] Respawning pellet at (6, 49) in 113 ticks
[13:49:13 INF] Respawning pellet at (5, 5) in 100 ticks
[13:49:13 INF] Command (Up) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:13 WRN] Game tick 7, Duration = 201.50 / 200, Duty Cycle = 1.0075, Positions = [StaticHeuro@(42,3), ClingyHeuroBot@(6,49), ClingyHeuroBot2@(49,45), AdvancedMCTSBot@(5,5)]
[13:49:13 INF] Respawning pellet at (41, 3) in 107 ticks
[13:49:13 INF] Command (Left) enqueued for bot (StaticHeuro). Queue length: 1
[13:49:13 INF] Command (Left) enqueued for bot (ClingyHeuroBot2). Queue length: 1
[13:49:13 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 1
[13:49:13 INF] Respawning pellet at (48, 45) in 99 ticks
[13:49:13 INF] Command (Right) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:13 INF] Game tick 8, Duration = 197.83 / 200, Duty Cycle = 0.9891, Positions = [StaticHeuro@(41,3), ClingyHeuroBot@(6,49), ClingyHeuroBot2@(48,45), AdvancedMCTSBot@(5,4)]
[13:49:13 INF] Respawning pellet at (40, 3) in 113 ticks
[13:49:13 INF] Respawning pellet at (47, 45) in 100 ticks
[13:49:13 INF] Respawning pellet at (7, 49) in 101 ticks
[13:49:13 INF] Game tick 9, Duration = 64.28 / 200, Duty Cycle = 0.3214, Positions = [StaticHeuro@(40,3), ClingyHeuroBot@(7,49), ClingyHeuroBot2@(47,45), AdvancedMCTSBot@(5,4)]
[13:49:13 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 1
[13:49:13 INF] Command (Down) enqueued for bot (ClingyHeuroBot2). Queue length: 1
[13:49:14 INF] Command (Up) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:14 INF] Respawning pellet at (39, 3) in 104 ticks
[13:49:14 INF] Respawning pellet at (8, 49) in 125 ticks
[13:49:14 INF] Respawning pellet at (5, 3) in 101 ticks
[13:49:14 INF] Command (Up) enqueued for bot (ClingyHeuroBot2). Queue length: 1
[13:49:14 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 1
[13:49:14 INF] Game tick 10, Duration = 62.59 / 200, Duty Cycle = 0.3130, Positions = [StaticHeuro@(39,3), ClingyHeuroBot@(8,49), ClingyHeuroBot2@(47,46), AdvancedMCTSBot@(5,3)]
[13:49:14 INF] Command (Right) enqueued for bot (StaticHeuro). Queue length: 1
[13:49:14 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:14 INF] Command (Down) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:14 INF] Respawning pellet at (9, 49) in 104 ticks
[13:49:14 INF] Game tick 11, Duration = 31.42 / 200, Duty Cycle = 0.1571, Positions = [StaticHeuro@(40,3), ClingyHeuroBot@(9,49), ClingyHeuroBot2@(47,45), AdvancedMCTSBot@(5,4)]
[13:49:14 INF] Command (Up) enqueued for bot (ClingyHeuroBot2). Queue length: 1
[13:49:14 INF] Command (Up) enqueued for bot (ClingyHeuroBot2). Queue length: 2
[13:49:14 INF] Command (Up) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:14 INF] Command (Up) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:14 INF] Respawning pellet at (10, 49) in 122 ticks
[13:49:14 INF] Respawning pellet at (47, 44) in 101 ticks
[13:49:14 INF] Game tick 12, Duration = 28.36 / 200, Duty Cycle = 0.1418, Positions = [StaticHeuro@(41,3), ClingyHeuroBot@(10,49), ClingyHeuroBot2@(47,44), AdvancedMCTSBot@(5,3)]
[13:49:14 INF] Command (Right) enqueued for bot (StaticHeuro). Queue length: 1
[13:49:14 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:14 INF] Command (Down) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:14 INF] Command (Up) enqueued for bot (ClingyHeuroBot2). Queue length: 2
[13:49:14 INF] Respawning pellet at (47, 43) in 119 ticks
[13:49:14 INF] Command (Up) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:14 INF] Game tick 13, Duration = 195.69 / 200, Duty Cycle = 0.9785, Positions = [StaticHeuro@(42,3), ClingyHeuroBot@(10,49), ClingyHeuroBot2@(47,43), AdvancedMCTSBot@(5,4)]
[13:49:14 INF] Respawning pellet at (11, 49) in 114 ticks
[13:49:14 INF] Respawning pellet at (47, 42) in 111 ticks
[13:49:14 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 1
[13:49:14 INF] Game tick 14, Duration = 62.83 / 200, Duty Cycle = 0.3142, Positions = [StaticHeuro@(43,3), ClingyHeuroBot@(11,49), ClingyHeuroBot2@(47,42), AdvancedMCTSBot@(5,3)]
[13:49:14 INF] Command (Right) enqueued for bot (StaticHeuro). Queue length: 1
[13:49:14 INF] Command (Up) enqueued for bot (ClingyHeuroBot2). Queue length: 1
[13:49:14 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:15 INF] Command (Down) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:15 INF] Respawning pellet at (12, 49) in 122 ticks
[13:49:15 INF] Respawning pellet at (47, 41) in 130 ticks
[13:49:15 INF] Game tick 15, Duration = 38.94 / 200, Duty Cycle = 0.1947, Positions = [StaticHeuro@(44,3), ClingyHeuroBot@(12,49), ClingyHeuroBot2@(47,41), AdvancedMCTSBot@(5,4)]
[13:49:15 INF] Command (Up) enqueued for bot (ClingyHeuroBot2). Queue length: 1
[13:49:15 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:15 INF] Command (Down) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:15 INF] Respawning pellet at (13, 49) in 112 ticks
[13:49:15 INF] Respawning pellet at (47, 40) in 113 ticks
[13:49:15 INF] Game tick 16, Duration = 75.30 / 200, Duty Cycle = 0.3765, Positions = [StaticHeuro@(45,3), ClingyHeuroBot@(13,49), ClingyHeuroBot2@(47,40), AdvancedMCTSBot@(5,5)]
[13:49:15 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:15 INF] Command (Up) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:15 INF] Respawning pellet at (47, 39) in 135 ticks
[13:49:15 INF] Respawning pellet at (14, 49) in 110 ticks
[13:49:15 INF] Game tick 17, Duration = 68.91 / 200, Duty Cycle = 0.3446, Positions = [StaticHeuro@(46,3), ClingyHeuroBot@(14,49), ClingyHeuroBot2@(47,39), AdvancedMCTSBot@(5,4)]
[13:49:15 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:15 INF] Command (Up) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:15 INF] Respawning pellet at (47, 38) in 128 ticks
[13:49:15 INF] Respawning pellet at (15, 49) in 119 ticks
[13:49:15 INF] Game tick 18, Duration = 68.39 / 200, Duty Cycle = 0.3420, Positions = [StaticHeuro@(46,3), ClingyHeuroBot@(15,49), ClingyHeuroBot2@(47,38), AdvancedMCTSBot@(5,3)]
[13:49:15 INF] Command (Up) enqueued for bot (ClingyHeuroBot2). Queue length: 1
[13:49:15 INF] Command (Up) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:15 INF] Respawning pellet at (5, 2) in 116 ticks
[13:49:15 INF] Command (Down) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:15 INF] Respawning pellet at (47, 37) in 101 ticks
[13:49:15 INF] Game tick 19, Duration = 36.69 / 200, Duty Cycle = 0.1835, Positions = [StaticHeuro@(46,3), ClingyHeuroBot@(15,49), ClingyHeuroBot2@(47,37), AdvancedMCTSBot@(5,2)]
[13:49:15 INF] Command (Up) enqueued for bot (ClingyHeuroBot2). Queue length: 1
[13:49:15 INF] Command (Up) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:15 INF] Command (Right) enqueued for bot (StaticHeuro). Queue length: 1
[13:49:16 INF] Respawning pellet at (15, 48) in 134 ticks
[13:49:16 INF] Game tick 20, Duration = 50.14 / 200, Duty Cycle = 0.2507, Positions = [StaticHeuro@(46,3), ClingyHeuroBot@(15,48), ClingyHeuroBot2@(47,37), AdvancedMCTSBot@(5,3)]
[13:49:16 INF] Command (Right) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:16 INF] Command (Right) enqueued for bot (ClingyHeuroBot2). Queue length: 1
[13:49:16 INF] Command (Up) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:16 INF] Respawning pellet at (15, 47) in 117 ticks
[13:49:16 INF] Command (Up) enqueued for bot (ClingyHeuroBot2). Queue length: 1
[13:49:16 INF] Respawning pellet at (6, 3) in 125 ticks
[13:49:16 INF] Command (Down) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:16 INF] Respawning pellet at (48, 37) in 129 ticks
[13:49:16 INF] Game tick 21, Duration = 76.65 / 200, Duty Cycle = 0.3832, Positions = [StaticHeuro@(46,3), ClingyHeuroBot@(15,47), ClingyHeuroBot2@(48,37), AdvancedMCTSBot@(6,3)]
[13:49:16 INF] Command (Right) enqueued for bot (StaticHeuro). Queue length: 1
[13:49:16 INF] Command (Right) enqueued for bot (ClingyHeuroBot2). Queue length: 2
[13:49:16 INF] Command (Right) enqueued for bot (AdvancedMCTSBot). Queue length: 2
[13:49:16 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:16 INF] Game tick 22, Duration = 25.33 / 200, Duty Cycle = 0.1267, Positions = [StaticHeuro@(46,3), ClingyHeuroBot@(15,47), ClingyHeuroBot2@(48,37), AdvancedMCTSBot@(6,3)]
[13:49:16 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:16 INF] Command (Right) enqueued for bot (ClingyHeuroBot2). Queue length: 2
[13:49:16 INF] Command (Right) enqueued for bot (AdvancedMCTSBot). Queue length: 2
[13:49:16 INF] Respawning pellet at (49, 37) in 119 ticks
[13:49:16 INF] Respawning pellet at (16, 47) in 127 ticks
[13:49:16 INF] Command (Right) enqueued for bot (ClingyHeuroBot2). Queue length: 2
[13:49:16 INF] Respawning pellet at (7, 3) in 123 ticks
[13:49:16 INF] Game tick 23, Duration = 41.50 / 200, Duty Cycle = 0.2075, Positions = [StaticHeuro@(46,3), ClingyHeuroBot@(16,47), ClingyHeuroBot2@(49,37), AdvancedMCTSBot@(7,3)]
[13:49:16 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:16 INF] Command (Right) enqueued for bot (ClingyHeuroBot2). Queue length: 3
[13:49:16 INF] Command (Right) enqueued for bot (AdvancedMCTSBot). Queue length: 2
[13:49:16 INF] Respawning pellet at (17, 47) in 136 ticks
[13:49:16 INF] Respawning pellet at (50, 37) in 128 ticks
[13:49:16 INF] Respawning pellet at (8, 3) in 129 ticks
[13:49:16 INF] Command (Right) enqueued for bot (StaticHeuro). Queue length: 1
[13:49:16 INF] Game tick 24, Duration = 66.78 / 200, Duty Cycle = 0.3339, Positions = [StaticHeuro@(46,3), ClingyHeuroBot@(17,47), ClingyHeuroBot2@(50,37), AdvancedMCTSBot@(8,3)]
[13:49:16 INF] Command (Up) enqueued for bot (ClingyHeuroBot2). Queue length: 3
[13:49:17 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:17 INF] Command (Right) enqueued for bot (AdvancedMCTSBot). Queue length: 2
[13:49:17 INF] Respawning pellet at (0, 37) in 114 ticks
[13:49:17 INF] Respawning pellet at (18, 47) in 123 ticks
[13:49:17 INF] Respawning pellet at (9, 3) in 129 ticks
[13:49:17 INF] Game tick 25, Duration = 29.46 / 200, Duty Cycle = 0.1473, Positions = [StaticHeuro@(46,3), ClingyHeuroBot@(18,47), ClingyHeuroBot2@(0,37), AdvancedMCTSBot@(9,3)]
[13:49:17 INF] Command (Right) enqueued for bot (StaticHeuro). Queue length: 1
[13:49:17 INF] Command (Right) enqueued for bot (ClingyHeuroBot2). Queue length: 3
[13:49:17 INF] Respawning pellet at (1, 37) in 135 ticks
[13:49:17 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 1
[13:49:17 INF] Respawning pellet at (19, 47) in 122 ticks
[13:49:17 INF] Respawning pellet at (10, 3) in 134 ticks
[13:49:17 INF] Command (Right) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:17 INF] Game tick 26, Duration = 107.50 / 200, Duty Cycle = 0.5375, Positions = [StaticHeuro@(46,3), ClingyHeuroBot@(19,47), ClingyHeuroBot2@(1,37), AdvancedMCTSBot@(10,3)]
[13:49:17 INF] Command (Up) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:17 INF] Command (Right) enqueued for bot (StaticHeuro). Queue length: 1
[13:49:17 INF] Command (Right) enqueued for bot (ClingyHeuroBot2). Queue length: 3
[13:49:17 INF] Respawning pellet at (1, 36) in 138 ticks
[13:49:17 INF] Respawning pellet at (20, 47) in 128 ticks
[13:49:17 INF] Command (Right) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:17 INF] Respawning pellet at (11, 3) in 142 ticks
[13:49:17 INF] Game tick 27, Duration = 111.65 / 200, Duty Cycle = 0.5583, Positions = [StaticHeuro@(46,3), ClingyHeuroBot@(20,47), ClingyHeuroBot2@(1,36), AdvancedMCTSBot@(11,3)]
[13:49:17 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:17 INF] Respawning pellet at (12, 3) in 127 ticks
[13:49:17 INF] Command (Up) enqueued for bot (ClingyHeuroBot2). Queue length: 2
[13:49:17 INF] Game tick 28, Duration = 30.62 / 200, Duty Cycle = 0.1531, Positions = [StaticHeuro@(46,3), ClingyHeuroBot@(20,47), ClingyHeuroBot2@(1,36), AdvancedMCTSBot@(12,3)]
[13:49:17 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:17 INF] Command (Left) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:17 INF] Command (Up) enqueued for bot (ClingyHeuroBot2). Queue length: 3
[13:49:17 INF] Respawning pellet at (21, 47) in 123 ticks
[13:49:17 INF] Command (Up) enqueued for bot (StaticHeuro). Queue length: 1
[13:49:17 INF] Game tick 29, Duration = 47.95 / 200, Duty Cycle = 0.2397, Positions = [StaticHeuro@(46,3), ClingyHeuroBot@(21,47), ClingyHeuroBot2@(1,36), AdvancedMCTSBot@(11,3)]
[13:49:17 INF] Command (Left) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:17 INF] Command (Up) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:17 INF] Command (Down) enqueued for bot (ClingyHeuroBot2). Queue length: 3
[13:49:17 INF] Command (Up) enqueued for bot (StaticHeuro). Queue length: 2
[13:49:18 INF] Respawning pellet at (1, 35) in 108 ticks
[13:49:18 INF] Game tick 30, Duration = 76.78 / 200, Duty Cycle = 0.3839, Positions = [StaticHeuro@(46,2), ClingyHeuroBot@(21,47), ClingyHeuroBot2@(1,35), AdvancedMCTSBot@(10,3)]
[13:49:18 INF] Command (Left) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:18 INF] Command (Up) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:18 INF] Command (Right) enqueued for bot (ClingyHeuroBot2). Queue length: 3
[13:49:18 INF] Command (Right) enqueued for bot (StaticHeuro). Queue length: 2
[13:49:18 INF] Respawning pellet at (1, 34) in 121 ticks
[13:49:18 INF] Command (Right) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:18 INF] Respawning pellet at (21, 46) in 135 ticks
[13:49:18 INF] Respawning pellet at (46, 1) in 125 ticks
[13:49:18 INF] Game tick 31, Duration = 88.12 / 200, Duty Cycle = 0.4406, Positions = [StaticHeuro@(46,1), ClingyHeuroBot@(21,46), ClingyHeuroBot2@(1,34), AdvancedMCTSBot@(9,3)]
[13:49:18 INF] Command (Up) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:18 INF] Command (Down) enqueued for bot (ClingyHeuroBot2). Queue length: 3
[13:49:18 INF] Command (Left) enqueued for bot (AdvancedMCTSBot). Queue length: 2
[13:49:18 INF] Respawning pellet at (21, 45) in 140 ticks
[13:49:18 INF] Respawning pellet at (47, 1) in 125 ticks
[13:49:18 INF] Game tick 32, Duration = 32.96 / 200, Duty Cycle = 0.1648, Positions = [StaticHeuro@(47,1), ClingyHeuroBot@(21,45), ClingyHeuroBot2@(1,35), AdvancedMCTSBot@(10,3)]
[13:49:18 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:18 INF] Command (Right) enqueued for bot (ClingyHeuroBot2). Queue length: 3
[13:49:18 INF] Command (Right) enqueued for bot (StaticHeuro). Queue length: 1
[13:49:18 INF] Respawning pellet at (2, 35) in 119 ticks
[13:49:18 INF] Respawning pellet at (48, 1) in 127 ticks
[13:49:18 INF] Command (Up) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:18 INF] Game tick 33, Duration = 49.96 / 200, Duty Cycle = 0.2498, Positions = [StaticHeuro@(48,1), ClingyHeuroBot@(21,45), ClingyHeuroBot2@(2,35), AdvancedMCTSBot@(9,3)]
[13:49:18 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:18 INF] Command (Right) enqueued for bot (ClingyHeuroBot2). Queue length: 3
[13:49:18 INF] Respawning pellet at (22, 45) in 139 ticks
[13:49:18 INF] Respawning pellet at (49, 1) in 126 ticks
[13:49:18 INF] Respawning pellet at (9, 2) in 122 ticks
[13:49:18 INF] Game tick 34, Duration = 33.46 / 200, Duty Cycle = 0.1673, Positions = [StaticHeuro@(49,1), ClingyHeuroBot@(22,45), ClingyHeuroBot2@(2,35), AdvancedMCTSBot@(9,2)]
[13:49:18 INF] Command (Left) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:18 INF] Command (Right) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:18 INF] Command (Right) enqueued for bot (ClingyHeuroBot2). Queue length: 3
[13:49:18 INF] Command (Right) enqueued for bot (StaticHeuro). Queue length: 1
[13:49:19 INF] Respawning pellet at (3, 35) in 151 ticks
[13:49:19 INF] Respawning pellet at (23, 45) in 152 ticks
[13:49:19 INF] Game tick 35, Duration = 34.18 / 200, Duty Cycle = 0.1709, Positions = [StaticHeuro@(49,1), ClingyHeuroBot@(23,45), ClingyHeuroBot2@(3,35), AdvancedMCTSBot@(9,2)]
[13:49:19 INF] Command (Right) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:19 INF] Command (Up) enqueued for bot (ClingyHeuroBot). Queue length: 2
[13:49:19 INF] Command (Right) enqueued for bot (ClingyHeuroBot2). Queue length: 3
[13:49:19 INF] Respawning pellet at (4, 35) in 132 ticks
[13:49:19 INF] Command (Up) enqueued for bot (AdvancedMCTSBot). Queue length: 1
[13:49:19 INF] Game tick 36, Duration = 41.40 / 200, Duty Cycle = 0.2070, Positions = [StaticHeuro@(49,1), ClingyHeuroBot@(23,45), ClingyHeuroBot2@(4,35), AdvancedMCTSBot@(9,2)]