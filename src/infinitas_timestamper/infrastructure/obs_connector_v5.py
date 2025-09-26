import logging
from typing import Any, Callable
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
        self._logger = logging.getLogger("app")

    def connect(self, host: str, port: int, password: str) -> None:
        self.req_client = obsV5.ReqClient(host=host, port=port, password=password)
        self.event_client = obsV5.EventClient(host=host, port=port, password=password)

        def on_stream_state_changed(status: Any):
            for state, event_enum in EVENT_MAP.items():
                if status.output_state == state:
                    self._notify(event_enum)

        def on_exit_started(*_):
            self.event_client.unsubscribe()

        if self._is_streaming():
            self._notify(StreamEventType.STREAM_STARTED)

        self.event_client.callback.register([on_stream_state_changed, on_exit_started])  # type: ignore
        self.event_client.subscribe()

    def disconnect(self) -> None:
        self.event_client.disconnect()
        self.req_client.disconnect()

    def test_connect(self, host: str, port: int, password: str) -> tuple[str, str]:
        try:
            req_client = obsV5.ReqClient(
                host=host, port=port, password=password, timeout=5
            )

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

    def observe_stream(self, callback: Callable[[StreamEventType], None]) -> None:
        self._callbacks.append(callback)

    def _notify(self, evt: StreamEventType):
        for callback in self._callbacks:
            callback(evt)

    def _is_streaming(self) -> bool:
        status = self.req_client.get_stream_status()
        return status.output_active  # type: ignore
