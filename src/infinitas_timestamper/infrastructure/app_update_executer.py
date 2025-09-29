import logging
import subprocess
from injector import inject

from domain.entity.update_entity import ExecutionResult, UpdateExeCommand
from domain.port.app_updater import IAppUpdater
from domain.value.base_path import BasePath


class AppUpdateExecuter(IAppUpdater):
    @inject
    def __init__(self, logger: logging.Logger, base_path: BasePath, updater_exe: UpdateExeCommand) -> None:
        super().__init__()
        self._updater_exe_command = updater_exe
        self._base_path = base_path
        self._logger = logger

    def check(self) -> ExecutionResult:
        cmd = self._updater_exe_command.execution + ["--check"]

        self._logger.debug(f"Check command: {cmd}")
        result = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8")
        if result.returncode != 0:
            self._logger.error(f"Check failed: {result.stderr}")
        else:
            self._logger.debug(f"Check result: {result.stdout}")

        return ExecutionResult.model_validate_json(result.stdout.strip())

    def update(self) -> None:
        cmd = self._updater_exe_command.execution + ["--update", str(self._base_path)]

        self._logger.debug(f"Update command: {cmd}")
        subprocess.Popen(cmd)
