from abc import ABC, abstractmethod
from typing import Callable
from uuid import UUID

from domain.value.stream_value import StreamEventType


class IStreamGateway(ABC):
    @abstractmethod
    def connect(self, host: str, port: int, password: str) -> None: ...

    @abstractmethod
    def disconnect(self) -> None: ...

    @abstractmethod
    def subscribe(self, id: UUID, callback: Callable[[StreamEventType], None]) -> None: ...

    @abstractmethod
    def unsubscribe(self, id: UUID) -> None: ...
