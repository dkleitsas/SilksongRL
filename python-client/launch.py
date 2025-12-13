import asyncio
import json
import os
import sys

import uvicorn

DEFAULT_CONFIG = {
    "transport": "socket_sync",  # "api", "socket_sync", "socket_async"
    "host": "0.0.0.0",
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


def run_api(cfg):
    host = cfg.get("host", "0.0.0.0")
    port = int(cfg.get("port", 8000))
    print(f"[Launcher] Starting FastAPI on {host}:{port}")
    uvicorn.run("api:app", host=host, port=port, reload=True)


def run_socket_sync(cfg):
    from socket_server import RLSocketServer

    host = cfg.get("host", "0.0.0.0")
    port = int(cfg.get("port", 8000))
    print(f"[Launcher] Starting sync socket server on {host}:{port}")
    server = RLSocketServer(host=host, port=port)
    server.start()


def run_socket_async(cfg):
    from socket_server_async import AsyncRLSocketServer

    host = cfg.get("host", "0.0.0.0")
    port = int(cfg.get("port", 8000))
    print(f"[Launcher] Starting async socket server on {host}:{port}")
    server = AsyncRLSocketServer(host=host, port=port)
    asyncio.run(server.start())


def main():
    cfg = load_config()
    transport = cfg.get("transport", "api").lower()

    if transport == "api":
        run_api(cfg)
    elif transport == "socket_sync":
        run_socket_sync(cfg)
    elif transport == "socket_async":
        run_socket_async(cfg)
    else:
        print(f"[Launcher] Unknown transport '{transport}'. Expected one of api|socket_sync|socket_async.")
        sys.exit(1)


if __name__ == "__main__":
    main()

