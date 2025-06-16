# C++ Development Notes and Best Practices

This document contains C++ specific learnings, common pitfalls, and best practices encountered during development.

## Standard Library & Features

### `std::atomic`

- **`std::atomic<double>`**:
  - The `fetch_add()` member function is generally **not** available for `std::atomic<double>` in C++17 (and earlier standards).
  - To perform atomic addition (or other arithmetic operations) on `std::atomic<double>`, use a compare-exchange loop (e.g., `compare_exchange_weak` or `compare_exchange_strong`).
  - Example of atomic addition for `std::atomic<double> myAtomicDouble; double valueToAdd;`:
    ```cpp
    double expected = myAtomicDouble.load(std::memory_order_relaxed);
    double desired;
    do {
        desired = expected + valueToAdd;
    } while (!myAtomicDouble.compare_exchange_weak(expected, desired, std::memory_order_release, std::memory_order_relaxed));
    ```
- **`std::atomic<int>` (and other integral types)**:
  - `fetch_add()` is available and is the preferred way to perform atomic addition.
  - Example: `myAtomicInt.fetch_add(1);`

### `std::optional`

- Requires C++17. Ensure your `CMakeLists.txt` is configured: `set(CMAKE_CXX_STANDARD 17)` and `set(CMAKE_CXX_STANDARD_REQUIRED ON)`.
- Include the header: `#include <optional>`.
- Initialization: Use `std::optional<MyType> optVar = MyType(...);` or `optVar.emplace(...);` if `optVar` was default-initialized.
- Checking for value: `if (optVar.has_value())` or `if (optVar)`.
- Accessing value: `optVar.value()` (throws if no value), or `*optVar` (undefined behavior if no value).

### `getenv` vs `_dupenv_s` (MSVC)

- The standard `getenv` is considered unsafe by MSVC and will result in a C4996 warning/error.
- Use `_dupenv_s` instead.
  - It allocates memory for the environment variable's value, which you are responsible for freeing using `free()`.
  - Signature: `errno_t _dupenv_s(char** buffer, size_t* numberOfElements, const char* varname);`
  - Example:
    ```cpp
    char* envValue = nullptr;
    size_t size = 0;
    if (_dupenv_s(&envValue, &size, "MY_ENV_VAR") == 0 && envValue != nullptr) {
        // Use envValue
        std::string valueStr(envValue);
        free(envValue); // Don't forget to free!
    }
    ```

## CMake & Build System

### Linker Errors (LNK2019, LNK2001, LNK1120 - MSVC)

- These "unresolved external symbol" errors most commonly occur when a `.cpp` source file (which contains the definitions for functions/classes) is **missing** from the `add_executable(...)` or `add_library(...)` command in `CMakeLists.txt`.
- Ensure that **all necessary `.cpp` files** are listed.
- Header files (`.h`, `.hpp`) should **not** be listed as sources in `add_executable` or `add_library`. They are included by `.cpp` files.
- After adding/removing source files in `CMakeLists.txt`, CMake needs to regenerate the build files. Often, simply re-running the build command (e.g., `cmake --build build`) will trigger this. If not, you might need to delete the build directory and re-run CMake configuration explicitly.

### C++ Standard

- To set the C++ standard (e.g., C++17):
  ```cmake
  set(CMAKE_CXX_STANDARD 17)
  set(CMAKE_CXX_STANDARD_REQUIRED ON)
  set(CMAKE_CXX_EXTENSIONS OFF) # Optional, but good practice
  ```
