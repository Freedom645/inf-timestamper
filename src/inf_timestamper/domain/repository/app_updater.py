from abc import ABC, abstractmethod

from usecase.dto.app_updating import ProgressCallback, VersionInfo


class IAppUpdater(ABC):
    @abstractmethod
    def backup(self, progress_callback: ProgressCallback) -> None: ...

    @abstractmethod
    def download(self, progress_callback: ProgressCallback, version_info: VersionInfo) -> None: ...

    @abstractmethod
    def apply_update(self, progress_callback: ProgressCallback) -> None: ...

    @abstractmethod
    def finalize(self) -> None: ...
