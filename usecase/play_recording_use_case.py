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
        settings: Settings,
        stream_service: StreamService,
        play_watcher: IPlayWatcher,
    ) -> None:
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
        self._stream_service.subscribe(
            StreamEventType.STREAM_STARTED, presenter.stream_started
        )
        self._stream_service.subscribe(
            StreamEventType.STREAM_ENDED, presenter.stream_ended
        )

        try:
            stream_session: StreamSession[PlayData] = self._stream_service.connect(
                host=self._settings.obs.host,
                port=self._settings.obs.port,
                password=self._settings.obs.password,
            )

            def callback(watch_type: WatchType, play_data: PlayData):
                if watch_type == WatchType.REGISTER:
                    # タイムスタンプの新規登録
                    timestamp = Timestamp[PlayData](data=play_data)
                    stream_session.add_timestamp(timestamp)
                    presenter.timestamp_added(stream_session, timestamp)

                elif watch_type == WatchType.MODIFY:
                    # タイムスタンプのデータ更新
                    latest_timestamp = stream_session.get_latest_timestamp()

                    if latest_timestamp.data.equals_without_result(play_data):
                        latest_timestamp.data = play_data
                    presenter.timestamp_updated(stream_session, latest_timestamp)

            self._play_watcher.subscribe(stream_session.id, callback)

            return stream_session
        except:
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
        self._play_watcher.unsubscribe(stream_session.id)
        try:
            self._stream_service.disconnect()
        finally:
            self._stream_service.unsubscribe(
                StreamEventType.STREAM_STARTED, presenter.stream_started
            )
            self._stream_service.unsubscribe(
                StreamEventType.STREAM_ENDED, presenter.stream_ended
            )
