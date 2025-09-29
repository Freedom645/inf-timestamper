import subprocess
from pathlib import Path

from consts import Application, GitHub
from domain.entity.arguments import Arguments
from domain.entity.result import ExecutionResult, ExecutionStatus
from domain.value.progress_value import ProgressCallback, Step, StepProgressCallback
from service.app_updater import AppUpdater
from service.github_accessor import GithubRepositoryAccessor


class UpdateUseCase:
    def __init__(self) -> None:
        self.extracted_path: Path | None = None

    def update(self, args: Arguments, progress_callback: StepProgressCallback) -> ExecutionResult:
        try:
            if args.update_path is None:
                raise ValueError("UpdatePath is None")

            def callback(step: Step) -> ProgressCallback:
                return lambda progress: progress_callback(step, progress)

            accessor = GithubRepositoryAccessor(repo=GitHub.REPO)
            version, url = accessor.check_latest_version(
                asset_name=GitHub.ASSET_NAME, progress_callback=callback(Step.VERSION_CHECK)
            )

            if url is None:
                raise ValueError("No valid URL found for the latest release")

            app_updater = AppUpdater(app_dir=args.update_path)
            # バックアップ
            app_updater.backup_current_app(progress_callback=callback(Step.BACKUP))
            # ダウンロード
            downloaded_path = app_updater.download_app(url=url, progress_callback=callback(Step.DOWNLOAD))
            # 展開
            extracted_path = app_updater.extract_zip(zip_path=downloaded_path, progress_callback=callback(Step.EXTRACT))
            # コピー
            app_updater.copy_tree(
                src=extracted_path,
                dst=args.update_path,
                progress_callback=callback(Step.REPLACE),
                ignore=["updater.exe"],
            )

            self.extracted_path = extracted_path

            return ExecutionResult(
                status=ExecutionStatus.SUCCESS, message="Update completed", data={"version": version}
            )
        except Exception as e:
            print(e)
            return ExecutionResult(status=ExecutionStatus.ERROR, message="Update failed", data={"error": str(e)})

    def execute_app(self, args: Arguments) -> None:
        if args.update_path is None:
            raise ValueError("UpdatePath is None")
        if self.extracted_path is None:
            raise ValueError("ExtractedPath is None")

        callback_cmd = f"{args.update_path / Application.NAME}.exe --update-result success"
        subprocess.Popen(
            [
                str(args.update_path / "replacer.exe"),
                "--source_file",
                str(self.extracted_path / "updater.exe"),
                "--target_file",
                str(args.update_path / "updater.exe"),
                "--callback",
                callback_cmd,
            ]
        )

        return
