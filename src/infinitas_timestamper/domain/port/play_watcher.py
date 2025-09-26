from abc import ABC, abstractmethod
from enum import StrEnum
from typing import Callable
from uuid import UUID

from domain.entity.game_entity import PlayData


class WatchType(StrEnum):
    REGISTER = "register"
    MODIFY = "modify"


class IPlayWatcher(ABC):
    @abstractmethod
    def start(self) -> None: ...

    @abstractmethod
    def stop(self) -> None: ...

    @abstractmethod
    def subscribe(self, id: UUID, callback: Callable[[WatchType, PlayData], None]) -> None: ...

    @abstractmethod
    def unsubscribe(self, id: UUID) -> None: ...
