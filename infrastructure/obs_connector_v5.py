from typing import Callable
import obsws_python as obsV5

from domain.port.stream_gateway import IStreamGateway
from domain.service.stream_service import StreamEventType

OBS_WEBSOCKET_OUTPUT_STARTING = "OBS_WEBSOCKET_OUTPUT_STARTING"
OBS_WEBSOCKET_OUTPUT_STARTED = "OBS_WEBSOCKET_OUTPUT_STARTED"
OBS_WEBSOCKET_OUTPUT_STOPPING = "OBS_WEBSOCKET_OUTPUT_STOPPING"
OBS_WEBSOCKET_OUTPUT_STOPPED = "OBS_WEBSOCKET_OUTPUT_STOPPED"

EVENT_MAP = {
    OBS_WEBSOCKET_OUTPUT_STARTED: StreamEventType.STREAM_STARTED,
    OBS_WEBSOCKET_OUTPUT_STOPPED: StreamEventType.STREAM_ENDED,
}


class OBSConnectorV5(IStreamGateway):
    def __init__(self):
        self._callbacks: list[Callable[[StreamEventType], None]] = []

    def connect(self, host: str, port: int, password: str) -> None:
        self.req_client = obsV5.ReqClient(host=host, port=port, password=password)
        self.event_client = obsV5.EventClient(host=host, port=port, password=password)

        def on_stream_state_changed(status):
            for state, event_enum in EVENT_MAP.items():
                if status.output_state == state:
                    self._notify(event_enum)

        def on_exit_started(*args):
            self.event_client.unsubscribe()

        if self._is_streaming():
            self._notify(StreamEventType.STREAM_STARTED)

        self.event_client.callback.register([on_stream_state_changed, on_exit_started])
        self.event_client.subscribe()

    def disconnect(self) -> None:
        self.event_client.disconnect()
        self.req_client.disconnect()

    def observe_stream(self, callback: Callable[[StreamEventType], None]) -> None:
        self._callbacks.append(callback)

    def _notify(self, evt: StreamEventType):
        for callback in self._callbacks:
            callback(evt)

    def _is_streaming(self) -> bool:
        status = self.req_client.get_stream_status()
        return status.output_active
