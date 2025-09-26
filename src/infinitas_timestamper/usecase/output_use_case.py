import logging
from pathlib import Path
from injector import inject
import pyperclip

from domain.entity.game_entity import PlayData
from domain.entity.game_format import GameTimestampFormatter
from domain.entity.settings_entity import Settings
from domain.entity.stream_entity import StreamSession
from usecase.repository.current_stream_session_repository import CurrentStreamSessionRepository
from usecase.repository.stream_session_repository import StreamSessionRepository


class OutputUseCase:
    @inject
    def __init__(
        self,
        logger: logging.Logger,
        stream_session_repository: StreamSessionRepository[PlayData],
        settings: Settings,
        current_session: CurrentStreamSessionRepository[PlayData],
    ):
        self._logger = logger
        self._stream_session_repository = stream_session_repository
        self._current_session = current_session
        self.settings = settings

    def copy_to_clipboard(self) -> bool:
        stream_session = self._current_session.get()
        if stream_session is None:
            self._logger.error("セッションがありません")
            return False

        if stream_session.start_time is None:
            self._logger.error(f"配信が開始されていません ID: {stream_session.id}")
            return False

        try:
            formatter = GameTimestampFormatter(self.settings.timestamp.template)

            lines: list[str] = []
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

    def save_stream_session(self, stream_session: StreamSession[PlayData]) -> None:
        try:
            self._stream_session_repository.save(stream_session)
        except Exception as e:
            self._logger.error("配信セッションの保存に失敗しました")
            self._logger.exception(e)

    def load_stream_session(self, file_path: Path) -> StreamSession[PlayData]:
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
