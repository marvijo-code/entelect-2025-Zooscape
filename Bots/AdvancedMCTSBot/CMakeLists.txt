cmake_minimum_required(VERSION 3.8)
project(AdvancedMCTSBot LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED True)
set(CMAKE_CXX_EXTENSIONS OFF)

# CMakeList.txt : Top-level CMake project file, do global configuration
# and include sub-projects here.
#
cmake_minimum_required (VERSION 3.8)

# Enable Hot Reload for MSVC compilers if supported.
if (POLICY CMP0141)
  cmake_policy(SET CMP0141 NEW)
  set(CMAKE_MSVC_DEBUG_INFORMATION_FORMAT "$<IF:$<AND:$<C_COMPILER_ID:MSVC>,$<CXX_COMPILER_ID:MSVC>>,$<$<CONFIG:Debug,RelWithDebInfo>:EditAndContinue>,$<$<CONFIG:Debug,RelWithDebInfo>:ProgramDatabase>>")
endif()

project ("AdvancedMCTSBot")

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
