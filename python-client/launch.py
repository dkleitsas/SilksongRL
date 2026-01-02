import asyncio
import json
import os
import sys

DEFAULT_CONFIG = {
    "transport": "socket_sync",  # "socket_sync", "socket_async"
    "host": "localhost",
    "port": 8000,
}


def load_config(path: str = "server_config.json"):
    if not os.path.exists(path):
        print(f"[Launcher] Config not found at {path}, using defaults.")
        return DEFAULT_CONFIG
    with open(path, "r", encoding="utf-8") as f:
        try:
            cfg = json.load(f)
        except json.JSONDecodeError as e:
            print(f"[Launcher] Failed to parse config: {e}. Using defaults.")
            return DEFAULT_CONFIG
    merged = DEFAULT_CONFIG.copy()
    merged.update(cfg)
    return merged


def run_socket_sync(host: str, port: int):
    from socket_server import RLSocketServer

    print(f"[Launcher] Starting sync socket server on {host}:{port}")
    server = RLSocketServer(host=host, port=port)
    server.start()


def run_socket_async(host: str, port: int):
    from socket_server_async import AsyncRLSocketServer

    print(f"[Launcher] Starting async socket server on {host}:{port}")
    server = AsyncRLSocketServer(host=host, port=port)
    asyncio.run(server.start())


def main():
    cfg = load_config()
    transport = cfg.get("transport", "socket_sync").lower()
    host = cfg.get("host", "localhost")
    port = int(cfg.get("port", 8000))

    if transport == "socket_sync":
        run_socket_sync(host, port)
    elif transport == "socket_async":
        run_socket_async(host, port)
    else:
        print(f"[Launcher] Unknown transport '{transport}'. Expected one of socket_sync|socket_async.")
        sys.exit(1)


if __name__ == "__main__":
    main()

