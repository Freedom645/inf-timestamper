from abc import ABC, abstractmethod

from domain.entity.update_entity import ExecutionResult


class IAppUpdater(ABC):
    @abstractmethod
    def check(self) -> ExecutionResult: ...

    @abstractmethod
    def update(self) -> None: ...
