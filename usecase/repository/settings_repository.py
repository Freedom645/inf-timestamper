from abc import ABC, abstractmethod
from domain.entity.settings import Settings


class SettingsRepository(ABC):
    @abstractmethod
    def load(self) -> Settings: ...

    @abstractmethod
    def save(self, setting: Settings) -> None: ...
