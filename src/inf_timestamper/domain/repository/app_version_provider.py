from abc import ABC, abstractmethod

from usecase.dto.app_updating import VersionInfo


class IVersionProvider(ABC):
    @abstractmethod
    def check_latest_version(self) -> VersionInfo: ...
