cmake_minimum_required(VERSION 3.11)
cmake_policy(VERSION 3.11)
project(AdvancedMCTSBot LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED True)
set(CMAKE_CXX_EXTENSIONS OFF)

# Treat specific warnings as errors for MSVC
if(MSVC)
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} /we4244 /we4267")
endif()

# Enable Hot Reload for MSVC compilers if supported.
if (POLICY CMP0141)
  cmake_policy(SET CMP0141 NEW)
  set(CMAKE_MSVC_DEBUG_INFORMATION_FORMAT "$<IF:$<AND:$<C_COMPILER_ID:MSVC>,$<CXX_COMPILER_ID:MSVC>>,$<$<CONFIG:Debug,RelWithDebInfo>:EditAndContinue>,$<$<CONFIG:Debug,RelWithDebInfo>:ProgramDatabase>>")
endif()



add_executable(AdvancedMCTSBot
    main.cpp
    Bot.cpp
    GameState.cpp
    MCTSEngine.cpp
    MctsService.cpp
    Heuristics.cpp
    MCTSNode.cpp
)

find_package(fmt CONFIG REQUIRED)
find_package(microsoft-signalr CONFIG REQUIRED)

target_link_libraries(AdvancedMCTSBot PRIVATE
    fmt::fmt                      # or fmt::fmt-header-only
    microsoft-signalr::microsoft-signalr)

# ----------------------------
# Unit tests
# ----------------------------
enable_testing()

add_executable(AdvancedMCTSBotTest162
    tests/Test162.cpp
    tests/JsonGameStateLoader.cpp
    GameState.cpp
    MCTSEngine.cpp
    MctsService.cpp
    MCTSNode.cpp
    Heuristics.cpp
    Bot.cpp
)

# Link the same libraries the main bot uses (except signalr, which the test doesn't require)
# fmt is transitively used by some engine files for logging
find_package(fmt CONFIG REQUIRED)

# Include directories to access headers
target_include_directories(AdvancedMCTSBotTest162 PRIVATE 
    ${CMAKE_CURRENT_SOURCE_DIR} 
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_link_libraries(AdvancedMCTSBotTest162 PRIVATE fmt::fmt microsoft-signalr::microsoft-signalr)

add_test(NAME AdvancedMCTSBot_SmokeTest162 COMMAND AdvancedMCTSBotTest162)
set_tests_properties(AdvancedMCTSBot_SmokeTest162 PROPERTIES WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/../..)

find_package(fmt CONFIG REQUIRED)

add_executable(AdvancedMCTSBotTest34
    tests/Test34.cpp
    tests/JsonGameStateLoader.cpp
    GameState.cpp
    MCTSEngine.cpp
    MctsService.cpp
    MCTSNode.cpp
    Heuristics.cpp
    Bot.cpp
)

target_include_directories(AdvancedMCTSBotTest34 PRIVATE 
    ${CMAKE_CURRENT_SOURCE_DIR} 
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_link_libraries(AdvancedMCTSBotTest34 PRIVATE fmt::fmt microsoft-signalr::microsoft-signalr)

add_test(NAME AdvancedMCTSBot_SmokeTest34 COMMAND AdvancedMCTSBotTest34)
set_tests_properties(AdvancedMCTSBot_SmokeTest34 PROPERTIES WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/../..)

add_executable(AdvancedMCTSBotTest162_ClingyHeuroBot2
    tests/Test162_ClingyHeuroBot2.cpp
    tests/JsonGameStateLoader.cpp
    GameState.cpp
    MCTSEngine.cpp
    MctsService.cpp
    MCTSNode.cpp
    Heuristics.cpp
    Bot.cpp
)

target_include_directories(AdvancedMCTSBotTest162_ClingyHeuroBot2 PRIVATE 
    ${CMAKE_CURRENT_SOURCE_DIR} 
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_link_libraries(AdvancedMCTSBotTest162_ClingyHeuroBot2 PRIVATE fmt::fmt microsoft-signalr::microsoft-signalr)

add_test(NAME AdvancedMCTSBot_SmokeTest162_ClingyHeuroBot2 COMMAND AdvancedMCTSBotTest162_ClingyHeuroBot2)
set_tests_properties(AdvancedMCTSBot_SmokeTest162_ClingyHeuroBot2 PROPERTIES WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/../..)

# ----------------------------
# GameStateInspector utility
# ----------------------------
add_executable(GameStateInspector
    tools/GameStateInspector.cpp
    tests/JsonGameStateLoader.cpp
    GameState.cpp
)

target_include_directories(GameStateInspector PRIVATE 
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_link_libraries(GameStateInspector PRIVATE fmt::fmt)

# ----------------------------
# Functional test for GameState 805 (AdvancedMCTSBot)
# ----------------------------
add_executable(AdvancedMCTSBotTest805
    tests/Test805_AdvancedMCTSBot.cpp
    tests/JsonGameStateLoader.cpp
    GameState.cpp
    MCTSEngine.cpp
    MctsService.cpp
    MCTSNode.cpp
    Heuristics.cpp
    Bot.cpp
)

# Include directories
target_include_directories(AdvancedMCTSBotTest805 PRIVATE 
    ${CMAKE_CURRENT_SOURCE_DIR} 
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

# Link libraries
target_link_libraries(AdvancedMCTSBotTest805 PRIVATE fmt::fmt microsoft-signalr::microsoft-signalr)

add_test(NAME AdvancedMCTSBot_SmokeTest805 COMMAND AdvancedMCTSBotTest805)
set_tests_properties(AdvancedMCTSBot_SmokeTest805 PROPERTIES WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/../..)
