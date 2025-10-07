import logging
from injector import inject
from typing import Callable, Any
from uuid import UUID
from obswebsocket import obsws, events, requests

from domain.port.stream_gateway import IStreamGateway
from domain.value.stream_value import StreamEventType

OBS_WEBSOCKET_OUTPUT_STARTING = "OBS_WEBSOCKET_OUTPUT_STARTING"
OBS_WEBSOCKET_OUTPUT_STARTED = "OBS_WEBSOCKET_OUTPUT_STARTED"
OBS_WEBSOCKET_OUTPUT_STOPPING = "OBS_WEBSOCKET_OUTPUT_STOPPING"
OBS_WEBSOCKET_OUTPUT_STOPPED = "OBS_WEBSOCKET_OUTPUT_STOPPED"


class OBSConnectorV4(IStreamGateway):
    @inject
    def __init__(self, logger: logging.Logger) -> None:
        self._logger = logger
        self._callbacks: dict[UUID, Callable[[StreamEventType], None]] = {}

    def connect(self, host: str, port: int, password: str) -> None:
        self.ws = obsws(host, port, password)
        self.ws.connect()

        if self._is_streaming():
            self._notify(StreamEventType.STREAM_STARTED)
        self.ws.register(self._on_stream_changed_event, events.StreamStateChanged)  # type: ignore

    def disconnect(self) -> None:
        self.ws.disconnect()

    def test_connect(self, host: str, port: int, password: str) -> tuple[str, str]:
        try:
            ws = obsws(host, port, password)
            ws.connect()
            print(ws.call(requests.GetCurrentProgramScene()))  # type: ignore
            program_scene: Any = ws.call(requests.GetCurrentProgramScene()).getCurrentProgramSceneName()  # type: ignore
            obs_version: Any = ws.call(requests.GetVersion()).getObsVersion()  # type: ignore
            ws.disconnect()

            return obs_version, program_scene  # type: ignore
        except TimeoutError as e:
            raise ConnectionError(
                "[TimeoutError] OBSへの接続に失敗しました。OBSが起動しているか、ホスト・ポート・パスワードが正しいか確認してください。"
            ) from e
        except Exception as e:
            self._logger.error("テスト接続失敗")
            self._logger.exception(e)
            raise e

    def subscribe(self, id: UUID, callback: Callable[[StreamEventType], None]) -> None:
        self._callbacks[id] = callback

    def unsubscribe(self, id: UUID) -> None:
        if id in self._callbacks:
            del self._callbacks[id]

    def _is_streaming(self) -> bool:
        return self.ws.call(requests.GetStreamStatus()).getOutputActive()  # type: ignore

    def _on_stream_changed_event(self, event) -> None:  # type: ignore
        output_state: str = event.getOutputState()  # type: ignore
        if output_state == OBS_WEBSOCKET_OUTPUT_STARTED:
            self._notify(StreamEventType.STREAM_STARTED)
        elif output_state == OBS_WEBSOCKET_OUTPUT_STOPPED:
            self._notify(StreamEventType.STREAM_ENDED)

    def _notify(self, evt: StreamEventType) -> None:
        callbacks = list(self._callbacks.values())
        for callback in callbacks:
            callback(evt)
