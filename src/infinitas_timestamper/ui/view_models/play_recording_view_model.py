import logging
from pathlib import Path
from injector import inject
from datetime import datetime
from PySide6.QtCore import Signal, QObject, QTimer, QDateTime

from domain.entity.game_entity import PlayData
from domain.entity.stream_entity import StreamSession, Timestamp
from domain.value.stream_value import StreamStatus
from usecase.output_use_case import OutputUseCase
from usecase.play_recording_use_case import PlayRecordingUseCase


class PlayRecordingViewModel(QObject):
    recording_button_changed = Signal(bool, str)
    copy_button_changed = Signal(bool, str)

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

    def stream_started(self, session: StreamSession[PlayData]) -> None:
        self._emit_status_changed(session.stream_status)
        self.start_time_changed.emit(session.start_time)

    def stream_ended(self, session: StreamSession[PlayData]) -> None:
        self._emit_status_changed(session.stream_status)

    def timestamp_added(self, session: StreamSession[PlayData], timestamp: Timestamp[PlayData]) -> None:
        self.timestamp_count_changed.emit(session.count_timestamp())
        self.timestamp_upsert_signal.emit(session, timestamp)

    def timestamp_updated(self, session: StreamSession[PlayData], timestamp: Timestamp[PlayData]) -> None:
        self.timestamp_upsert_signal.emit(session, timestamp)

    def on_open_recording(self, file_path: Path) -> None:
        """記録ファイルを開く"""
        try:
            session = self._output_use_case.load_stream_session(file_path)
            session.stream_status = StreamStatus.COMPLETED
        except Exception as e:
            self._logger.error("記録ファイルの読み込みに失敗しました")
            self._logger.exception(e)
            self.status_changed.emit(f"記録ファイルの読み込みに失敗しました（{e}）")
            raise e

        self._logger.debug(f"読み込んだセッション: {session}")

        self.recording_button_changed.emit(False, "記録開始")
        self.status_changed.emit("記録ファイル読み込み完了")

        self.play_record_overwrite_signal.emit(session)

    def on_start_recording_button(self) -> str:
        """記録開始ボタン押下"""
        try:
            self.recording_button_changed.emit(False, "記録開始（開始中...）")
            self.status_changed.emit("OBS接続中...")
            self.start_time_changed.emit(None)
            self.timestamp_count_changed.emit(0)

            stream_session = self._play_recording_use_case.start_recording(self)
        except Exception as e:
            self._logger.error("OBS接続に失敗しました")
            self._logger.exception(e)
            self.recording_button_changed.emit(True, "記録開始")
            self.status_changed.emit(f"OBS接続失敗（{e}）")
            raise e

        self.recording_button_changed.emit(True, "記録停止")
        self._emit_status_changed(stream_session.stream_status)
        return "-"

    def on_stop_recording_button(self) -> None:
        """記録停止ボタン押下"""
        self.recording_button_changed.emit(False, "記録停止（停止中...）")
        self.status_changed.emit("停止中...")
        stream_session = None
        try:
            stream_session = self._play_recording_use_case.stop_recording()
        except Exception as e:
            self.status_changed.emit(f"停止失敗（{e}）")
        else:
            self.status_changed.emit("停止完了")

        if stream_session:
            self._output_use_case.save_stream_session(stream_session)

        self.recording_button_changed.emit(True, "記録開始")

    def on_copy_timestamps_to_clipboard(self) -> None:
        """タイムスタンプをクリップボードにコピー"""
        self.copy_button_changed.emit(False, "コピー中...")
        res = False
        try:
            res = self._output_use_case.copy_to_clipboard()
        except Exception as e:
            self._logger.error("クリップボードへのコピーに失敗しました")
            self._logger.exception(e)
            res = False
        finally:
            self.copy_button_changed.emit(False, "完了" if res else "失敗")
            QTimer.singleShot(1000, lambda: self.copy_button_changed.emit(True, "コピー"))

    def on_edit_start_time(self, new_start_time: QDateTime | None) -> None:
        """開始時間を編集"""
        try:
            st_datetime = datetime.fromtimestamp(new_start_time.toSecsSinceEpoch()) if new_start_time else None
            session = self._play_recording_use_case.edit_start_time(st_datetime)
            if session:
                self.play_record_overwrite_signal.emit(session)
        except Exception as e:
            self._logger.error("開始時間の編集に失敗しました")
            self._logger.exception(e)
            return

    def _emit_status_changed(self, stream_status: StreamStatus) -> None:
        if stream_status == StreamStatus.WAITING:
            self.status_changed.emit("記録開始待ち")
        elif stream_status == StreamStatus.RECORDING:
            self.status_changed.emit("記録中")
        elif stream_status == StreamStatus.COMPLETED:
            self.status_changed.emit("記録完了")
        else:
            self.status_changed.emit("-")

    def on_close(self) -> None:
        """ウィジェットが閉じられるときの処理"""
        self._logger.info("クローズイベント処理を開始します")
        self._logger.info("記録停止処理を実行します")
        stream_session = self._play_recording_use_case.stop_recording()
        if stream_session is not None:
            self._logger.info("記録保存処理を実行します")
            self._output_use_case.save_stream_session(stream_session)

        self._logger.info("クローズイベント処理が完了しました")
