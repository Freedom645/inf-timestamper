import logging
from uuid import UUID
from injector import inject
from typing import Any, Callable
import obsws_python as obsV5

from domain.port.stream_gateway import IStreamGateway
from domain.value.stream_value import StreamEventType

OBS_WEBSOCKET_OUTPUT_STARTING = "OBS_WEBSOCKET_OUTPUT_STARTING"
OBS_WEBSOCKET_OUTPUT_STARTED = "OBS_WEBSOCKET_OUTPUT_STARTED"
OBS_WEBSOCKET_OUTPUT_STOPPING = "OBS_WEBSOCKET_OUTPUT_STOPPING"
OBS_WEBSOCKET_OUTPUT_STOPPED = "OBS_WEBSOCKET_OUTPUT_STOPPED"

EVENT_MAP = {
    OBS_WEBSOCKET_OUTPUT_STARTED: StreamEventType.STREAM_STARTED,
    OBS_WEBSOCKET_OUTPUT_STOPPED: StreamEventType.STREAM_ENDED,
}


class OBSConnectorV5(IStreamGateway):
    @inject
    def __init__(self, logger: logging.Logger) -> None:
        self._callbacks: dict[UUID, Callable[[StreamEventType], None]] = {}
        self._logger = logger

        self.req_client: obsV5.ReqClient | None = None
        self.event_client: obsV5.EventClient | None = None

    def connect(self, host: str, port: int, password: str) -> None:
        self.req_client = obsV5.ReqClient(host=host, port=port, password=password)
        self.event_client = obsV5.EventClient(host=host, port=port, password=password)

        def on_stream_state_changed(status: Any) -> None:
            for state, event_enum in EVENT_MAP.items():
                if status.output_state == state:
                    self._notify(event_enum)

        def on_exit_started(data: Any) -> None:
            if self.event_client:
                self.event_client.unsubscribe()

        if self._is_streaming(self.req_client):
            self._notify(StreamEventType.STREAM_STARTED)

        self.event_client.callback.register([on_stream_state_changed, on_exit_started])  # type: ignore
        self.event_client.subscribe()

    def disconnect(self) -> None:
        if self.event_client:
            try:
                self.event_client.disconnect()
            finally:
                self.event_client = None

        if self.req_client:
            try:
                self.req_client.disconnect()
            finally:
                self.req_client = None

    def test_connect(self, host: str, port: int, password: str) -> tuple[str, str]:
        try:
            req_client = obsV5.ReqClient(host=host, port=port, password=password, timeout=5)

            res = req_client.get_current_program_scene()
            program_scene: Any = res.current_program_scene_name  # type: ignore

            res = req_client.get_version()
            obs_version: Any = res.obs_version  # type: ignore
            req_client.disconnect()

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

    def _notify(self, evt: StreamEventType) -> None:
        callbacks = list(self._callbacks.values())
        for callback in callbacks:
            callback(evt)

    def _is_streaming(self, req_client: obsV5.ReqClient) -> bool:
        status = req_client.get_stream_status()
        return status.output_active  # type: ignore
