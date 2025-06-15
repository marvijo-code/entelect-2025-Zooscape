cmake_minimum_required(VERSION 3.22)

# Add VCPKG cmake toolchain file
set(CMAKE_TOOLCHAIN_FILE "${CMAKE_CURRENT_SOURCE_DIR}/vcpkg/scripts/buildsystems/vcpkg.cmake"
        CACHE STRING "Vcpkg toolchain file")

project(AdvancedMCTSBot)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Find required packages
find_package(microsoft-signalr REQUIRED)
find_package(fmt REQUIRED)
find_package(Threads REQUIRED)

# Add compiler flags for optimization
if(MSVC)
    set(CMAKE_CXX_FLAGS_RELEASE "/O2 /DNDEBUG")
    set(CMAKE_CXX_FLAGS_DEBUG "/Od /DDEBUG")
else()
    set(CMAKE_CXX_FLAGS_RELEASE "-O3 -DNDEBUG -march=native -flto")
    set(CMAKE_CXX_FLAGS_DEBUG "-g -O0 -DDEBUG")
endif()

# Include directories
include_directories(src)
include_directories(include)

# Source files
set(SOURCES
    src/main.cpp
    src/Bot.cpp
    src/MctsService.cpp
    src/GameState.cpp
    src/MCTSEngine.cpp
    src/MCTSNode.cpp
    src/Heuristics.cpp
)

# Create executable
add_executable(AdvancedMCTSBot ${SOURCES})

# Link libraries
target_link_libraries(AdvancedMCTSBot
    Threads::Threads
    microsoft-signalr::microsoft-signalr
    fmt::fmt
)

# Compiler-specific optimizations
if(CMAKE_CXX_COMPILER_ID STREQUAL "GNU" OR CMAKE_CXX_COMPILER_ID STREQUAL "Clang")
    target_compile_options(AdvancedMCTSBot PRIVATE
        -Wall -Wextra -Wpedantic
        -ffast-math
        -funroll-loops
    )
elseif(MSVC)
    target_compile_options(AdvancedMCTSBot PRIVATE
        /W4
        /fp:fast
    )
endif()