from abc import ABC, abstractmethod
from enum import StrEnum
from typing import Callable
from uuid import UUID

from domain.entity.stream_entity import TimestampData
from domain.value.stream_value import StreamKind


class WatchType(StrEnum):
    REGISTER = "register"
    MODIFY = "modify"


class IPlayWatcher(ABC):
    @abstractmethod
    def kind(self) -> StreamKind: ...

    @abstractmethod
    def start(self) -> None: ...

    @abstractmethod
    def stop(self) -> None: ...

    @abstractmethod
    def subscribe(self, id: UUID, callback: Callable[[WatchType, TimestampData], None]) -> None: ...

    @abstractmethod
    def unsubscribe(self, id: UUID) -> None: ...
