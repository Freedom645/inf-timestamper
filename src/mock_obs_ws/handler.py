import asyncio
import json
import websockets
from typing import Any

clients: set[websockets.ServerConnection] = set()  # 接続中クライアントを管理


def generate_success_response(
    request_type: str, data: dict[Any, Any]
) -> dict[Any, Any]:
    return {
        "d": {
            "requestStatus": {"result": True},
            "requestType": request_type,
            "responseData": data,
        }
    }


def generate_event_request(event_type: str, data: dict[Any, Any]) -> dict[Any, Any]:
    return {"d": {"eventType": event_type, "eventData": data}}


async def handle_client(websocket: websockets.ServerConnection) -> None:
    print(f"Client connected")
    clients.add(websocket)
    try:
        server_hello = {
            "op": 0,
            "d": {
                "rpcVersion": 1,
                "authentication": {
                    "challenge": "challenge",
                    "salt": "salt",
                },
            },
        }
        await websocket.send(json.dumps(server_hello))

        async for message in websocket:
            data = json.loads(message)
            print(f"Received: {data}")
            response = {}
            if data.get("op") == 1:
                response = {
                    "op": 2,
                    "d": {"negotiatedRpcVersion": "mock-dummy-version"},
                }
            if data.get("op") == 6:
                request_type = data.get("d").get("requestType")
                if request_type == "GetCurrentProgramScene":
                    response = generate_success_response(
                        request_type, {"CurrentProgramSceneName": "dummy scene"}
                    )
                if request_type == "GetVersion":
                    response = generate_success_response(
                        request_type, {"ObsVersion": "dummy version"}
                    )
                if request_type == "GetStreamStatus":
                    response = generate_success_response(
                        request_type, {"OutputActive": False}
                    )
            if response:
                print(f"Response: {response}")
                await websocket.send(json.dumps(response))
            else:
                print(f"Did'nt Response")

    except websockets.ConnectionClosed:
        pass
    finally:
        clients.remove(websocket)
        print("Client disconnected")


async def event_dispatcher() -> None:
    """コンソール入力でイベントを発行"""
    loop = asyncio.get_event_loop()
    while True:
        # コンソール入力を非同期で待つ
        user_input = await loop.run_in_executor(None, input, "Command> ")

        event = None
        if user_input == "start":
            event = generate_event_request(
                "StreamStateChanged", {"output_state": "OBS_WEBSOCKET_OUTPUT_STARTED"}
            )
        elif user_input == "stop":
            event = generate_event_request(
                "StreamStateChanged", {"output_state": "OBS_WEBSOCKET_OUTPUT_STOPPED"}
            )
        elif user_input == "exit":
            print("Exiting...")
            for client in clients:
                await client.close()
            break

        if event:
            # 接続中のすべてのクライアントに送信
            for client in clients:
                await client.send(json.dumps(event))
            print(f"Event sent: {event}")
        else:
            print("Unknown command. Use: start / stop / scene <name>")


async def main() -> None:
    await websockets.serve(handle_client, "localhost", 4455)
    print(f"Mock OBS WebSocket server running on ws://localhost:4455")
    await event_dispatcher()  # コンソール待機


if __name__ == "__main__":
    asyncio.run(main())
