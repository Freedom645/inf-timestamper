import logging
from pathlib import Path
from injector import inject
import pyperclip

from domain.entity.timestamp_formatter import GameTimestampFormatter
from domain.entity.inf_game_entity import InfPlayData
from domain.entity.inf_game_format import InfGameTimestampFormatter
from domain.entity.sdvx_game_entity import SDVXPlayData
from domain.entity.sdvx_game_format import SDVXGameTimestampFormatter
from domain.entity.settings_entity import Settings
from domain.entity.stream_entity import StreamSession
from domain.repository.current_stream_session_repository import CurrentStreamSessionRepository
from domain.repository.stream_session_repository import StreamSessionRepository


class OutputUseCase:
    @inject
    def __init__(
        self,
        logger: logging.Logger,
        stream_session_repository: StreamSessionRepository,
        settings: Settings,
        current_session: CurrentStreamSessionRepository,
    ):
        self._logger = logger
        self._stream_session_repository = stream_session_repository
        self._current_session = current_session
        self.settings = settings

    def copy_to_clipboard(self) -> bool:
        stream_session = self._current_session.get()

        if stream_session.start_time is None:
            self._logger.error(f"配信開始時間が設定されていません ID: {stream_session.id}")
            return False

        try:
            if len(stream_session.timestamps) == 0:
                self._logger.warning(
                    f"タイムスタンプが存在しないため、クリップボードへのコピーをスキップします ID: {stream_session.id}, 開始時間: {stream_session.start_time}"
                )
                return False

            sample_data = stream_session.timestamps[0].data

            formatter: GameTimestampFormatter | None = None
            lines: list[str] = []
            if isinstance(sample_data, InfPlayData):
                formatter = InfGameTimestampFormatter(self.settings.timestamp.template)
                if self.settings.timestamp.include_start_label:
                    lines.append(self.settings.timestamp.start_label)
            elif isinstance(sample_data, SDVXPlayData):  # pyright: ignore[reportUnnecessaryIsInstance]
                formatter = SDVXGameTimestampFormatter(self.settings.sdvx.template)
                if self.settings.sdvx.include_start_label:
                    lines.append(self.settings.sdvx.start_label)
            else:
                self._logger.error(
                    f"不明なゲーム形式のタイムスタンプデータです ID: {stream_session.id}, 形式: {type(sample_data)}"
                )
                return False

            for timestamp in stream_session.timestamps:
                line = formatter.format(stream_session, timestamp)
                lines.append(line)

            pyperclip.copy("\n".join(lines))
            return True
        except Exception as e:
            self._logger.error(
                f"クリップボードへのコピーに失敗しました ID: {stream_session.id}, 開始時間: {stream_session.start_time}"
            )
            self._logger.exception(e)
            return False

    def save_stream_session(self, stream_session: StreamSession) -> None:
        try:
            self._stream_session_repository.save(stream_session)
        except Exception as e:
            self._logger.error("配信セッションの保存に失敗しました")
            self._logger.exception(e)

    def load_stream_session(self, file_path: Path) -> StreamSession:
        try:
            session = self._stream_session_repository.load(file_path)
        except Exception as e:
            self._logger.error("配信セッションの読み込みに失敗しました")
            self._logger.exception(e)
            raise e

        if session is None:
            raise RuntimeError(f"配信セッションの読み込みに失敗しました {file_path}")

        self._current_session.set(session)
        return session
