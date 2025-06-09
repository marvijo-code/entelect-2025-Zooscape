cmake_minimum_required(VERSION 3.16)
project(AdvancedMCTSBot)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Find required packages
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
    src/GameState.cpp
    src/MCTSEngine.cpp
    src/MCTSNode.cpp
    src/Heuristics.cpp
    src/SignalRClient.cpp
)

# Create executable
add_executable(AdvancedMCTSBot ${SOURCES})

# Link libraries
target_link_libraries(AdvancedMCTSBot
    Threads::Threads
)

# Windows-specific libraries
if(WIN32)
    target_link_libraries(AdvancedMCTSBot
        ws2_32
        winhttp
    )
endif()

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