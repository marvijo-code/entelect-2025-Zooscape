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
# Comprehensive Test Suite
# ----------------------------
enable_testing()

add_executable(AdvancedMCTSBotTests
    tests/AllTests.cpp
    tests/JsonGameStateLoader.cpp
    GameState.cpp
    MCTSEngine.cpp
    MctsService.cpp
    MCTSNode.cpp
    Heuristics.cpp
    Bot.cpp
)

# Include directories to access headers
target_include_directories(AdvancedMCTSBotTests PRIVATE 
    ${CMAKE_CURRENT_SOURCE_DIR} 
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

# Link libraries
target_link_libraries(AdvancedMCTSBotTests PRIVATE fmt::fmt microsoft-signalr::microsoft-signalr)

# Add the comprehensive test
add_test(NAME AdvancedMCTSBot_AllTests COMMAND AdvancedMCTSBotTests)
set_tests_properties(AdvancedMCTSBot_AllTests PROPERTIES WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/../..)

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
