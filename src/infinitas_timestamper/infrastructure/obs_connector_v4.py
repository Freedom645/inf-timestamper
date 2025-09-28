from typing import Callable, Any
from obswebsocket import obsws, events, requests

from domain.port.stream_gateway import IStreamGateway
from domain.value.stream_value import StreamEventType

EVENT_MAP: dict[Any, StreamEventType] = {
    events.StreamStarted: StreamEventType.STREAM_STARTED,  # type: ignore
    events.StreamStopped: StreamEventType.STREAM_ENDED,  # type: ignore
}


class OBSConnectorV4(IStreamGateway):
    def __init__(self) -> None:
        self._callbacks: list[Callable[[StreamEventType], None]] = []

    def connect(self, host: str, port: int, password: str) -> None:
        self.ws = obsws(host, port, password)
        self.ws.connect()

        if self._is_streaming():
            self._notify(StreamEventType.STREAM_STARTED)
        self.ws.register(self._on_obs_event)  # type: ignore

    def disconnect(self) -> None:
        self.ws.disconnect()

    def observe_stream(self, callback: Callable[[StreamEventType], None]) -> None:
        self._callbacks.append(callback)

    def _is_streaming(self) -> bool:
        return self.ws.call(requests.GetStreamingStatus()).getStreaming()  # type: ignore

    def _on_obs_event(self, event) -> None:  # type: ignore
        for obs_event_class, event_enum in EVENT_MAP.items():
            if isinstance(event, obs_event_class):
                self._notify(event_enum)

    def _notify(self, evt: StreamEventType) -> None:
        for callback in self._callbacks:
            callback(evt)
