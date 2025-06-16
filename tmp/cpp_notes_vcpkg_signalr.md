## Package Management with vcpkg

-   **Purpose**: vcpkg is a C++ package manager that helps acquire and build third-party libraries.
-   **Integration with CMake**: 
    -   Typically integrated by setting the `CMAKE_TOOLCHAIN_FILE` variable when configuring CMake.
    -   Example: `cmake -B build -S . -DCMAKE_TOOLCHAIN_FILE=[path-to-vcpkg-root]/scripts/buildsystems/vcpkg.cmake`
    -   Replace `[path-to-vcpkg-root]` with the actual path to your vcpkg installation.
-   **Dependencies**: Libraries like `cpprestsdk` (a dependency for the SignalR C++ client) and `openssl` can be installed and managed via vcpkg.
    -   Example vcpkg install command: `vcpkg install cpprestsdk openssl signalrclient` (or `microsoft-signalr` depending on the exact package name in vcpkg).
-   **Triplets**: Ensure libraries are built for the correct target architecture and linkage (e.g., `x64-windows` for 64-bit dynamic linkage on Windows, `x64-windows-static` for static).

## SignalR C++ Client (`microsoft-signalr`)

-   **Library**: The official Microsoft SignalR C++ client.
-   **Dependencies**: Relies on `cpprestsdk` for HTTP and WebSocket communication.
-   **Core Object**: `signalr::hub_connection` represents the connection to a SignalR hub.
-   **Building Connection**:
    -   Use `signalr::hub_connection_builder`.
    -   Example: `auto connection = signalr::hub_connection_builder::create(hub_url).build();`
-   **Managing Connection Lifetime in Classes**:
    -   If a `signalr::hub_connection` is a class member and not initialized immediately in the constructor's initializer list, it can lead to issues if it lacks a suitable default constructor.
    -   A robust pattern is to use `std::optional<signalr::hub_connection>`:
        ```cpp
        // In your class header (e.g., Bot.h)
        #include <optional>
        #include "signalrclient/hub_connection.h"

        class MyClass {
        private:
            std::optional<signalr::hub_connection> m_connection;
            // ...
        };
        ```
        ```cpp
        // In your class implementation (e.g., Bot.cpp constructor)
        #include "signalrclient/hub_connection_builder.h"
        // ...
        MyClass::MyClass(const std::string& hub_url) {
            m_connection.emplace(signalr::hub_connection_builder::create(hub_url).build());
            // ... further setup ...
        }
        ```
    -   **Accessing Optional Connection**: Always check if the optional contains a value before using it:
        ```cpp
        if (m_connection.has_value()) {
            m_connection->start([](std::exception_ptr e) { /* handle start completion */ });
        }
        // or simply:
        if (m_connection) {
            m_connection->on("ReceiveMessage", [](const signalr::value& m) { /* handle message */ });
        }
        ```
-   **Registering Event Handlers**: Use the `.on()` method:
    ```cpp
    if (m_connection) {
        m_connection->on("MethodNameOnServer", [](const signalr::value& message_args) {
            // Process message_args, which is an array of arguments
            // e.g., if server sends a string and an int:
            // std::string str_arg = message_args.as_array()[0].as_string();
            // int int_arg = message_args.as_array()[1].as_double(); // SignalR often sends numbers as doubles
        });
    }
    ```
-   **Starting the Connection**: Asynchronously start the connection.
    ```cpp
    if (m_connection) {
        m_connection->start([this](std::exception_ptr ex) {
            if (ex) {
                try {
                    std::rethrow_exception(ex);
                } catch (const std::exception& e) {
                    // Log or handle error: e.what()
                }
            } else {
                // Connection started successfully
            }
        });
    }
    ```
-   **Stopping the Connection**: Asynchronously stop the connection.
    ```cpp
    if (m_connection && m_connection->get_connection_state() == signalr::connection_state::connected) {
        m_connection->stop([](std::exception_ptr ex) { /* handle stop completion */ });
    }
    ```
-   **Invoking Server Methods**: Use `.invoke()`.
    ```cpp
    if (m_connection) {
        std::vector<signalr::value> args = { signalr::value("myArgument") };
        m_connection->invoke("ServerMethodName", args, [](const signalr::value& result, std::exception_ptr ex) {
            if (ex) { /* handle error */ }
            else { /* process result if any */ }
        });
    }
    ```
-   **Logging**: The client supports configurable logging.
    ```cpp
    // Example: Set console logger with trace level info
    // auto console_logger = std::make_shared<signalr::console_logger>(signalr::trace_level::info);
    // if (m_connection) { m_connection->set_logger(console_logger); }
    ```
