import logging
from injector import inject
from datetime import datetime
from typing import Callable

from domain.entity.game_entity import PlayData
from domain.entity.settings_entity import Settings
from domain.entity.stream_entity import StreamSession, Timestamp
from domain.port.play_watcher import IPlayWatcher, WatchType
from domain.port.stream_gateway import IStreamGateway
from domain.value.stream_value import StreamEventType
from domain.value.stream_value import StreamStatus
from usecase.presenter.play_recording_presenter import PlayRecordingPresenter
from usecase.repository.current_stream_session_repository import CurrentStreamSessionRepository


class PlayRecordingUseCase:
    @inject
    def __init__(
        self,
        logger: logging.Logger,
        settings: Settings,
        current_session_repository: CurrentStreamSessionRepository[PlayData],
        play_watcher: IPlayWatcher,
        stream_gateway: IStreamGateway,
    ) -> None:
        self._logger = logger

        self._settings = settings
        self._current_session_repository = current_session_repository
        self._play_watcher = play_watcher
        self._stream_gateway = stream_gateway

        self._stream_gateway.observe_stream(self._on_stream_event)

    def _on_stream_event(self, event: StreamEventType) -> None:
        if event == StreamEventType.STREAM_STARTED:
            session = self._current_session_repository.get()
            if session is not None:
                session.start_recording(datetime.now())
        elif event == StreamEventType.STREAM_ENDED:
            session = self._current_session_repository.get()
            if session is not None and session.stream_status == StreamStatus.RECORDING:
                session.complete_recording()

    def start_recording(self, presenter: PlayRecordingPresenter) -> StreamSession[PlayData]:
        self._logger.info("プレイ記録を開始します")

        try:
            stream_session = self._current_session_repository.get()
            if stream_session is None:
                stream_session = StreamSession[PlayData]()

            if self._settings.obs.is_enabled:
                self._logger.info("配信接続します")
                self._stream_gateway.connect(
                    host=self._settings.obs.host,
                    port=self._settings.obs.port,
                    password=self._settings.obs.password,
                )
            else:
                self._logger.info("OBS Studio連携が無効のため、配信接続をスキップします")
                stream_session.start_recording(datetime.now())

            self._play_watcher.subscribe(
                stream_session.id, self._generate_timestamp_callback(stream_session, presenter)
            )
            self._play_watcher.start()
            self._logger.info(f"プレイ記録を開始しました ID: {stream_session.id}")

            self._current_session_repository.set(stream_session)
            return stream_session
        except Exception as e:
            self._logger.error("プレイ記録の開始に失敗しました")
            self._logger.exception(e)
            raise

    def _generate_timestamp_callback(
        self, stream_session: StreamSession[PlayData], presenter: PlayRecordingPresenter
    ) -> Callable[[WatchType, PlayData], None]:
        """タイムスタンプイベントのコールバック関数を生成する"""

        def on_timestamp_event(watch_type: WatchType, play_data: PlayData) -> None:
            self._logger.info(f"タイムスタンプイベント受信 {watch_type.name}: {play_data}")
            if stream_session.stream_status != StreamStatus.RECORDING:
                self._logger.warning("セッションが記録中ではないため、タイムスタンプの追加/更新をスキップしました")
                return

            if watch_type == WatchType.REGISTER:
                # タイムスタンプの新規登録
                timestamp = Timestamp[PlayData](data=play_data)
                stream_session.add_timestamp(timestamp)
                presenter.timestamp_added(stream_session, timestamp)

            elif watch_type == WatchType.MODIFY:
                # タイムスタンプのデータ更新
                latest_timestamp = stream_session.get_latest_timestamp()
                if latest_timestamp is None:
                    self._logger.warning("タイムスタンプの更新イベントを受信しましたが、タイムスタンプが存在しません")
                    return

                if latest_timestamp.data.equals_without_result(play_data):
                    latest_timestamp.data = play_data
                presenter.timestamp_updated(stream_session, latest_timestamp)

        return on_timestamp_event

    def stop_recording(self) -> StreamSession[PlayData] | None:
        self._logger.info("プレイ記録を停止します")
        stream_session = self._current_session_repository.get()
        if stream_session is None:
            self._logger.warning("セッションがなかったため、停止処理をスキップしました")
            return None

        try:
            self._play_watcher.stop()
            self._play_watcher.unsubscribe(stream_session.id)
        except Exception as e:
            self._logger.error("プレイ監視の停止に失敗しました")
            self._logger.exception(e)

        try:
            self._stream_gateway.disconnect()
        except Exception as e:
            self._logger.error("配信切断に失敗しました")
            self._logger.exception(e)

        return stream_session

    def edit_start_time(self, start_date_time: datetime | None) -> StreamSession[PlayData] | None:
        current_session = self._current_session_repository.get()
        if current_session is None:
            self._logger.warning("セッションがなかったため、開始時間の更新をスキップしました")
            return None

        self._logger.info(f"セッションの開始時間を更新します {current_session.start_time} -> {start_date_time}")
        current_session.start_time = start_date_time

        return current_session
