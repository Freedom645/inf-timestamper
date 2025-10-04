from typing import Protocol

from ui.thread.updater_thread import UpdaterThread


class UpdaterThreadFactory(Protocol):
    def __call__(self) -> UpdaterThread: ...
