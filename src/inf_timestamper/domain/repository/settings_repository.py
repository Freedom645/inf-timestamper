from abc import ABC, abstractmethod
from typing import Callable

from domain.entity.settings_entity import Settings

ChangedCallback = Callable[[Settings], None]


class SettingsRepository(ABC):
    @abstractmethod
    def load(self) -> Settings: ...

    @abstractmethod
    def save(self, setting: Settings) -> None: ...

    @abstractmethod
    def subscribe(self, callback: ChangedCallback) -> None: ...
