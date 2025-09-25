import logging
from pathlib import Path
from injector import inject
from PySide6.QtCore import Signal, QObject
from datetime import datetime

from domain.entity.game import PlayData
from domain.entity.stream import StreamSession, Timestamp
from domain.value.stream import StreamStatus
from usecase.output_use_case import OutputUseCase
from usecase.play_recording_use_case import PlayRecordingUseCase


class PlayRecordingViewModel(QObject):
    recording_button_changed = Signal(bool, str)
    status_changed = Signal(str)
    start_time_changed = Signal(object)
    timestamp_count_changed = Signal(int)
    timestamp_upsert_signal = Signal(StreamSession[PlayData], Timestamp[PlayData])
    play_record_overwrite_signal = Signal(StreamSession[PlayData])

    @inject
    def __init__(
        self,
        logger: logging.Logger,
        play_recording_use_case: PlayRecordingUseCase,
        output_use_case: OutputUseCase,
    ) -> None:
        QObject.__init__(self)
        self._logger = logger
        self._play_recording_use_case = play_recording_use_case
        self._output_use_case = output_use_case
        self._stream_session: StreamSession[PlayData] | None = None

    def stream_started(self, session: StreamSession[PlayData]) -> None:
        self._emit_status_changed(session.stream_status)
        self.start_time_changed.emit(session.start_time)

    def stream_ended(self, session: StreamSession[PlayData]) -> None:
        self._emit_status_changed(session.stream_status)

    def timestamp_added(
        self, session: StreamSession[PlayData], timestamp: Timestamp[PlayData]
    ) -> None:
        self.timestamp_count_changed.emit(session.count_timestamp())
        self.timestamp_upsert_signal.emit(session, timestamp)

    def timestamp_updated(
        self, session: StreamSession[PlayData], timestamp: Timestamp[PlayData]
    ) -> None:
        self.timestamp_upsert_signal.emit(session, timestamp)

    def on_open_recording(self, file_path: Path) -> None:
        """記録ファイルを開く"""
        try:
            session = self._output_use_case.load_stream_session(file_path)
            session.stream_status = StreamStatus.ENDED
        except Exception as e:
            self.status_changed.emit(f"記録ファイルの読み込みに失敗しました（{e}）")
            raise e

        self._stream_session = session
        self.recording_button_changed.emit(False, "記録開始")
        self.status_changed.emit("記録ファイル読み込み完了")

        self.play_record_overwrite_signal.emit(session)

    def on_start_recording_button(self) -> None:
        """記録開始ボタン押下"""
        try:
            self.recording_button_changed.emit(False, "記録開始（開始中...）")
            self.status_changed.emit("OBS接続中...")
            self.start_time_changed.emit(None)
            self.timestamp_count_changed.emit(0)

            self._stream_session = self._play_recording_use_case.start_recording(self)
        except Exception as e:
            self.recording_button_changed.emit(True, "記録開始")
            self.status_changed.emit(f"OBS接続失敗（{e}）")
            raise e

        self.recording_button_changed.emit(True, "記録停止")
        self._emit_status_changed(self._stream_session.stream_status)

    def on_stop_recording_button(self) -> None:
        """記録停止ボタン押下"""
        self.recording_button_changed.emit(False, "記録停止（停止中...）")
        self.status_changed.emit("停止中...")
        try:
            self._play_recording_use_case.stop_recording(self, self._stream_session)
        except Exception as e:
            self.status_changed.emit(f"停止失敗（{e}）")
        else:
            self.status_changed.emit("停止完了")

        self._output_use_case.save_stream_session(self._stream_session)

        self._stream_session = None
        self.recording_button_changed.emit(True, "記録開始")

    def on_copy_timestamps_to_clipboard(self) -> None:
        """タイムスタンプをクリップボードにコピー"""
        if self._stream_session is None:
            return
        self._output_use_case.copy_to_clipboard(self._stream_session)

    def _emit_status_changed(self, stream_status: StreamStatus) -> None:
        if stream_status == StreamStatus.BEFORE:
            self.status_changed.emit("OBS完了（配信開始待ち）")
        elif stream_status == StreamStatus.LIVE:
            self.status_changed.emit("OBS完了（記録中）")
        elif stream_status == StreamStatus.ENDED:
            self.status_changed.emit("OBS完了（配信終了）")
        else:
            self.status_changed.emit("-")

    def on_close(self) -> None:
        """ウィジェットが閉じられるときの処理"""
        if (
            self._stream_session
            and self._stream_session.stream_status == StreamStatus.LIVE
        ):
            self._play_recording_use_case.stop_recording(self, self._stream_session)
            self._output_use_case.save_stream_session(self._stream_session)
            self._stream_session = None
