import logging
from injector import inject
from enum import StrEnum
from typing import Callable
from pathlib import Path
from uuid import UUID
from time import sleep
from watchdog.observers import Observer
from watchdog.observers.api import BaseObserver
from watchdog.events import FileSystemEventHandler, DirModifiedEvent, FileModifiedEvent

from domain.entity.sdvx_game_entity import SDVXChartDetail, SDVXPlayData, SDVXPlayResult
from domain.value.sdvx_game_value import SDVXClearLamp
from domain.entity.settings_entity import Settings
from domain.port.play_watcher import IPlayWatcher, WatchType
from infrastructure.file_accessor import FileAccessor


def read_text(path: Path, default: str = "") -> str:
    try:
        with open(path, "r", encoding="utf-8") as f:
            return f.read().strip()
    except Exception:
        return default


def parse_int(text: str, default: int = -1) -> int:
    try:
        return int(text)
    except ValueError:
        return default


class PlayState(StrEnum):
    SELECT = "select"
    PLAY = "play"


class SDVXHelperFileWatcher(FileSystemEventHandler, IPlayWatcher):
    @inject
    def __init__(self, settings: Settings, file_accessor: FileAccessor, logger: logging.Logger) -> None:
        FileSystemEventHandler.__init__(self)
        self._settings = settings
        self._file_accessor = file_accessor
        self._logger = logger

        self._observer: BaseObserver | None = None
        self._callbacks: dict[UUID, Callable[[WatchType, SDVXPlayData], None]] = {}
        self._last_status = PlayState.SELECT

    def on_modified(self, event: DirModifiedEvent | FileModifiedEvent) -> None:
        status = self._last_status
        try:
            if isinstance(event.src_path, bytes):
                src_path = Path(event.src_path.decode())
            else:
                src_path = Path(event.src_path)

            if not src_path.is_file() or src_path.name not in ["select_jacket.png", "history_cursong.xml"]:
                return

            # ステータス遷移
            if src_path.name == "select_jacket.png" and self._last_status == PlayState.SELECT:
                # 選曲中にジャケット更新：プレイ開始
                self._last_status = PlayState.PLAY
                # FIXME: ファイル書き込みにラグがあるため、少し待つ
                sleep(0.3)
            elif src_path.name == "history_cursong.xml" and self._last_status == PlayState.PLAY:
                # プレイ中に履歴更新：リザルト表示～選曲に戻る
                self._last_status = PlayState.SELECT
            else:
                return

            play_data = self._read_history_cursong_xml(
                self._settings.sdvx.sdvx_helper_directory / "history_cursong.xml"
            )
            if self._last_status == PlayState.PLAY:
                play_data.play_result = None
                self._notify(WatchType.REGISTER, play_data)
            elif self._last_status == PlayState.SELECT:
                self._notify(WatchType.MODIFY, play_data)

        except Exception as e:
            self._logger.error("RefluxFileWatcherの処理に失敗しました")
            self._logger.exception(e)
        finally:
            self._last_status = status

    def _read_history_cursong_xml(self, directory: Path) -> SDVXPlayData:
        try:
            data = self._file_accessor.load_as_xml(directory)
            if data is None:
                raise RuntimeError("history_cursong.xmlの読み込みに失敗しました。")

            root = data.getroot()
            if root is None:
                raise RuntimeError("history_cursong.xmlの解析に失敗しました。")

            title = root.findtext("title", "unknown")
            difficulty = root.findtext("difficulty", "")
            chart_detail = SDVXChartDetail(
                title=title, level=parse_int(root.findtext("lv", "-1")), difficulty=difficulty
            )

            if (latest_result := root.find("Result")) is not None:
                play_result = SDVXPlayResult(
                    score=parse_int(latest_result.findtext("score", "-1")),
                    ex_score=parse_int(latest_result.findtext("exscore", "-1")),
                    clear_lamp=SDVXClearLamp.from_str(latest_result.findtext("lamp")),
                )
            else:
                play_result = None

            return SDVXPlayData(key=f"{title}_{difficulty}", chart_detail=chart_detail, play_result=play_result)
        except Exception as e:
            raise RuntimeError("latest.jsonの読み込みに失敗しました。") from e

    def start(self) -> None:
        self._last_status = PlayState.SELECT
        self._observer = Observer()
        self._observer.schedule(self, str(self._settings.sdvx.sdvx_helper_directory / "out"), recursive=False)
        self._observer.start()

    def stop(self) -> None:
        if self._observer is None:
            self._logger.warning("Observerが存在しません")
            return
        self._observer.stop()
        self._observer.unschedule_all()
        self._observer = None

    def subscribe(self, id: UUID, callback: Callable[[WatchType, SDVXPlayData], None]) -> None:
        self._callbacks[id] = callback

    def unsubscribe(self, id: UUID) -> None:
        if id in self._callbacks:
            del self._callbacks[id]

    def _notify(self, watch_type: WatchType, record: SDVXPlayData) -> None:
        for callback in self._callbacks.values():
            callback(watch_type, record)
