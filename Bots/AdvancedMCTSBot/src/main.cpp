#include "Bot.h"
#include <iostream>
#include <csignal>
#include <memory>

// Global bot instance for signal handling
std::unique_ptr<Bot> g_bot;

void signalHandler(int signal) {
    if (g_bot) {
        std::cout << "\n[SIGNAL] Received signal " << signal << ", requesting shutdown..." << std::endl;
        g_bot->requestShutdown();
    }
}

int main() {
    // Set up signal handling for graceful shutdown
    std::signal(SIGINT, signalHandler);
    std::signal(SIGTERM, signalHandler);

    try {
        std::cout << "=== Advanced MCTS Bot for Zooscape ===" << std::endl;
        g_bot = std::make_unique<Bot>();
        g_bot->run();
    } catch (const std::exception& e) {
        std::cerr << "Fatal error: " << e.what() << std::endl;
        return 1;
    }

    std::cout << "Shutdown complete." << std::endl;
    return 0;
}
