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
    def start(self): ...

    @abstractmethod
    def stop(self): ...

    @abstractmethod
    def subscribe(self, id: UUID, callback: Callable[[WatchType, PlayData], None]): ...

    @abstractmethod
    def unsubscribe(self, id: UUID): ...
