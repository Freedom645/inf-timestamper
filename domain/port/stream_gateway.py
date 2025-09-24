from abc import ABC, abstractmethod
from typing import Callable

from domain.value.stream import StreamEventType


class IStreamGateway(ABC):
    @abstractmethod
    def connect(self, host: str, port: int, password: str) -> None: ...

    @abstractmethod
    def disconnect(self) -> None: ...

    @abstractmethod
    def observe_stream(self, callback: Callable[[StreamEventType], None]) -> None: ...
