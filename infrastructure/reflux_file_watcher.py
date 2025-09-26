import logging
from injector import inject
from enum import StrEnum
from typing import Callable, TypedDict
from pathlib import Path
from uuid import UUID
from time import sleep
from watchdog.observers import Observer
from watchdog.events import FileSystemEventHandler, DirModifiedEvent, FileModifiedEvent

from domain.entity.game_entity import ChartDetail, PlayData, PlayResult
from domain.entity.settings_entity import Settings
from domain.port.play_watcher import IPlayWatcher, WatchType
from domain.value.game_value import DJ_LEVEL, ClearLamp
from infrastructure.file_accessor import FileAccessor


class LatestJson(TypedDict):
    apikey: str
    songid: str
    title: str
    title2: str
    bpm: str
    artist: str
    genre: str
    unlockType: str
    notecount: str
    diff: str
    level: str
    unlocked: str
    grade: str
    gaugepercent: str
    lamp: str
    exscore: str
    prematureend: str
    pgreat: str
    great: str
    good: str
    bad: str
    poor: str
    fast: str
    slow: str
    combobreak: str
    playtype: str
    style: str
    style2: str
    gauge: str
    assist: str
    range: str


def read_text(path: Path, default: str = "") -> str:
    try:
        with open(path, "r", encoding="utf-8") as f:
            return f.read().strip()
    except:
        return default


def parse_int(text: str, default: int = -1) -> int:
    try:
        return int(text)
    except ValueError:
        return default


KEY_MAP: dict[str, Callable[[Path], str | int]] = {
    "title": lambda path: read_text(path / "title.txt"),
    "level": lambda path: parse_int(read_text(path / "level.txt")),
}


class PlayState(StrEnum):
    OFF = "off"
    MENU = "menu"
    PLAY = "play"


class RefluxFileWatcher(FileSystemEventHandler, IPlayWatcher):

    @inject
    def __init__(
        self, settings: Settings, file_accessor: FileAccessor, logger: logging.Logger
    ) -> None:
        FileSystemEventHandler.__init__(self)
        self._settings = settings
        self._file_accessor = file_accessor
        self._logger = logger

        self._callbacks: dict[UUID, Callable[[WatchType, PlayData], None]] = {}
        self._last_status: str = PlayState.OFF.value

    def on_modified(self, event: DirModifiedEvent | FileModifiedEvent):
        status = self._last_status
        try:
            if isinstance(event.src_path, bytes):
                src_path = Path(event.src_path.decode())
            else:
                src_path = Path(event.src_path)

            if not src_path.is_file() or src_path.name != "playstate.txt":
                return

            status = read_text(src_path)
            if status == self._last_status:
                # Modifyイベントは多重で発生するため、状態が変化していなければ無視する
                return

            # FIXME: ファイル書き込みにラグがあるため、少し待つ
            sleep(0.3)

            if status == PlayState.PLAY.value:
                play_data = PlayData(
                    title=self._file_accessor.load_as_text(
                        src_path.parent / "title.txt", default=""
                    ),
                    level=self._file_accessor.load_as_integer(
                        src_path.parent / "level.txt"
                    ),
                )
                self._notify(WatchType.REGISTER, play_data)

            if (
                self._last_status == PlayState.PLAY.value
                and status != PlayState.PLAY.value
            ):
                play_data = self._read_latest_json(
                    self._settings.reflux.directory / "latest.json"
                )
                self._notify(WatchType.MODIFY, play_data)
        except Exception as e:
            self._logger.error(f"RefluxFileWatcherの処理に失敗しました")
            self._logger.exception(e)
        finally:
            self._last_status = status

    def _read_latest_json(self, directory: Path) -> PlayData:
        try:
            data: LatestJson = self._file_accessor.load_as_json(directory)  # type: ignore

            chart_detail = ChartDetail(
                artist=data.get("artist", ""),
                genre=data.get("genre", ""),
                bpm=int(data.get("bpm", -1)),
                difficulty=data.get("diff", ""),
                note_count=int(data.get("notecount", -1)),
            )
            play_result = PlayResult(
                dj_level=DJ_LEVEL(data.get("grade", "")),
                lamp=ClearLamp(data.get("lamp", "")),
                p_great=int(data.get("pgreat", -1)),
                great=int(data.get("great", -1)),
                good=int(data.get("good", -1)),
                bad=int(data.get("bad", -1)),
                poor=int(data.get("poor", -1)),
                fast=int(data.get("fast", -1)),
                slow=int(data.get("slow", -1)),
                combo_break=int(data.get("combobreak", -1)),
            )

            return PlayData(
                title=data.get("title", ""),
                level=int(data.get("level", "-1")),
                chart_detail=chart_detail,
                play_result=play_result,
            )
        except Exception as e:
            raise RuntimeError("latest.jsonの読み込みに失敗しました。") from e

    def start(self):
        self._last_status = PlayState.OFF.value
        self._observer = Observer()
        self._observer.schedule(self, str(self._settings.reflux.directory))
        self._observer.start()

    def stop(self):
        if self._observer is None:
            self._logger.warning("Observerが存在しません")
            return
        self._observer.stop()
        self._observer.unschedule_all()
        self._observer = None

    def subscribe(self, id: UUID, callback: Callable[[WatchType, PlayData], None]):
        self._callbacks[id] = callback

    def unsubscribe(self, id: UUID):
        if id in self._callbacks:
            del self._callbacks[id]

    def _notify(self, watch_type: WatchType, record: PlayData):
        for callback in self._callbacks.values():
            callback(watch_type, record)
