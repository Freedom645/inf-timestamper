import logging
from injector import inject

from domain.entity.game import PlayData
from domain.entity.settings import Settings
from domain.entity.stream import StreamSession, Timestamp
from domain.port.play_watcher import IPlayWatcher, WatchType
from domain.service.stream_service import StreamEventType, StreamService
from usecase.presenter.play_recording_presenter import PlayRecordingPresenter


class PlayRecordingUseCase:

    @inject
    def __init__(
        self,
        logger: logging.Logger,
        settings: Settings,
        stream_service: StreamService[PlayData],
        play_watcher: IPlayWatcher,
    ) -> None:
        self._logger = logger
        self._stream_service = stream_service
        self._settings = settings
        self._play_watcher = play_watcher

        self._stream_service.subscribe(
            StreamEventType.STREAM_STARTED, lambda _: self._play_watcher.start()
        )
        self._stream_service.subscribe(
            StreamEventType.STREAM_ENDED, lambda _: self._play_watcher.stop()
        )

    def start_recording(
        self, presenter: PlayRecordingPresenter
    ) -> StreamSession[PlayData]:
        self._logger.info("プレイ記録を開始します")
        self._stream_service.subscribe(
            StreamEventType.STREAM_STARTED, presenter.stream_started
        )
        self._stream_service.subscribe(
            StreamEventType.STREAM_ENDED, presenter.stream_ended
        )

        try:
            self._logger.info("配信接続します")
            stream_session: StreamSession[PlayData] = self._stream_service.connect(
                host=self._settings.obs.host,
                port=self._settings.obs.port,
                password=self._settings.obs.password,
            )

            def callback(watch_type: WatchType, play_data: PlayData):
                self._logger.info("タイムスタンプイベント受信")
                if watch_type == WatchType.REGISTER:
                    # タイムスタンプの新規登録
                    self._logger.info("タイムスタンプイベント受信")
                    timestamp = Timestamp[PlayData](data=play_data)
                    stream_session.add_timestamp(timestamp)
                    presenter.timestamp_added(stream_session, timestamp)

                elif watch_type == WatchType.MODIFY:
                    # タイムスタンプのデータ更新
                    latest_timestamp = stream_session.get_latest_timestamp()
                    if latest_timestamp is None:
                        self._logger.warning(
                            "タイムスタンプの更新イベントを受信しましたが、タイムスタンプが存在しません "
                            f"play_data: {play_data.model_json_schema()}"
                        )
                        return

                    if latest_timestamp.data.equals_without_result(play_data):
                        latest_timestamp.data = play_data
                    presenter.timestamp_updated(stream_session, latest_timestamp)

            self._play_watcher.subscribe(stream_session.id, callback)
            self._logger.info(f"プレイ記録を開始しました ID: {stream_session.id}")
            return stream_session
        except Exception as e:
            self._logger.error("プレイ記録の開始に失敗しました")
            self._logger.exception(e)
            self._stream_service.unsubscribe(
                StreamEventType.STREAM_STARTED, presenter.stream_started
            )
            self._stream_service.unsubscribe(
                StreamEventType.STREAM_ENDED, presenter.stream_ended
            )
            raise

    def stop_recording(
        self, presenter: PlayRecordingPresenter, stream_session: StreamSession[PlayData]
    ) -> None:
        self._logger.info("プレイ記録を停止します")
        try:
            self._play_watcher.stop()
        except Exception as e:
            self._logger.error("プレイ監視の停止に失敗しました")
            self._logger.exception(e)
        finally:
            self._play_watcher.unsubscribe(stream_session.id)

        try:
            self._stream_service.disconnect()
        except Exception as e:
            self._logger.error("配信切断に失敗しました")
            self._logger.exception(e)
        finally:
            self._stream_service.unsubscribe(
                StreamEventType.STREAM_STARTED, presenter.stream_started
            )
            self._stream_service.unsubscribe(
                StreamEventType.STREAM_ENDED, presenter.stream_ended
            )
