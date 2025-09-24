from typing import Callable
from obswebsocket import obsws, events, requests

from domain.port.stream_gateway import IStreamGateway
from domain.service.stream_service import StreamEventType

EVENT_MAP = {
    events.StreamStarted: StreamEventType.STREAM_STARTED,
    events.StreamStopped: StreamEventType.STREAM_ENDED,
}


class OBSConnectorV4(IStreamGateway):
    def __init__(self):
        self._callbacks: list[Callable[[StreamEventType], None]] = []

    def connect(self, host: str, port: int, password: str):
        self.ws = obsws(host, port, password)
        self.ws.connect()

        if self._is_streaming():
            self._notify(StreamEventType.STREAM_STARTED)
        self.ws.register(self._on_obs_event)

    def disconnect(self):
        self.ws.disconnect()

    def observe_stream(self, callback: Callable[[StreamEventType], None]) -> None:
        self._callbacks.append(callback)

    def _is_streaming(self) -> bool:
        status = self.ws.call(requests.GetStreamingStatus())
        return status.getStreaming()

    def _on_obs_event(self, event):
        for obs_event_class, event_enum in EVENT_MAP.items():
            if isinstance(event, obs_event_class):
                self._notify(event_enum)

    def _notify(self, evt: StreamEventType):
        for callback in self._callbacks[evt]:
            callback()
