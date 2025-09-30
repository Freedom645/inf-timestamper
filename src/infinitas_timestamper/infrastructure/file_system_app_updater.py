import shutil
import subprocess
import zipfile
import requests

from injector import inject
from datetime import datetime
from pathlib import Path

from core.consts import Application
from domain.value.base_path import BasePath
from usecase.dto.app_updating import ProgressCallback, VersionInfo
from usecase.repository.app_updater import IAppUpdater


class FileSystemAppUpdater(IAppUpdater):
    @inject
    def __init__(self, app_dir: BasePath) -> None:
        self._app_dir = app_dir
        self._temp_dir = self._app_dir / Application.TEMP_DIR_NAME
        self._backup_dir = self._app_dir / Application.BACKUP_DIR_NAME
        self._zip_dir = self._temp_dir / "downloaded.zip"
        self._new_app_dir = self._temp_dir / "new_app"

    def backup(self, progress_callback: ProgressCallback) -> None:
        progress_callback(0)
        self._backup_dir.mkdir(exist_ok=True)

        timestamp = datetime.now().strftime("%Y%m%d-%H%M%S")
        backup_path: Path = self._backup_dir / f"{Application.NAME}-{timestamp}.zip"

        with zipfile.ZipFile(backup_path, "w", zipfile.ZIP_DEFLATED) as zf:
            rglob_list = list(self._app_dir.rglob("*"))
            total_files = len(rglob_list)
            for i, filepath in enumerate(rglob_list, start=1):
                if filepath.name in [Application.BACKUP_DIR_NAME, Application.TEMP_DIR_NAME]:
                    continue

                if filepath.is_file() and not filepath.is_relative_to(self._backup_dir):
                    arcname = filepath.relative_to(self._app_dir)
                    zf.write(filepath, arcname)
                progress_callback(int(i / total_files * 100))

        # 過去のバックアップは最大5件までとし、古いものは削除
        backups = sorted(self._backup_dir.glob(f"{Application.NAME}-*.zip"))
        max_keep = 5
        for old_zip in backups[:-max_keep]:
            old_zip.unlink()

        progress_callback(100)

        return

    def download(self, progress_callback: ProgressCallback, version_info: VersionInfo) -> None:
        self._temp_dir.mkdir(exist_ok=True)
        for child in self._temp_dir.iterdir():
            if child.is_file() or child.is_symlink():
                child.unlink()
            elif child.is_dir():
                shutil.rmtree(child)

        # ZIPダウンロード
        progress_callback(0)

        with requests.get(str(version_info.asset_url), stream=True) as r:
            r.raise_for_status()
            total_size = int(r.headers.get("Content-Length", 0))
            downloaded = 0
            chunk_size = 8192  # 8KBごと

            with open(self._zip_dir, "wb") as f:
                for chunk in r.iter_content(chunk_size=chunk_size):
                    if not chunk:
                        continue
                    f.write(chunk)

                    downloaded += len(chunk)
                    if total_size > 0:
                        progress_callback(int(downloaded / total_size * 100))

        progress_callback(100)

    def apply_update(self, progress_callback: ProgressCallback) -> None:
        progress_callback(0)

        # 展開処理

        self._new_app_dir.mkdir(exist_ok=True)
        with zipfile.ZipFile(self._zip_dir, "r") as zf:
            files = zf.infolist()
            total_files = len(files)
            for i, f in enumerate(files, 1):
                zf.extract(f, self._new_app_dir)
                progress_callback(int(i / total_files * 50))

        # コピー処理
        progress_callback(50)
        all_files = [p for p in self._new_app_dir.rglob("*") if p.is_file()]
        total_files = len(all_files)

        for i, src_file in enumerate(all_files, 1):
            rel_path = src_file.relative_to(self._new_app_dir)
            dst_file = self._app_dir / rel_path
            dst_file.parent.mkdir(parents=True, exist_ok=True)

            # "メインファイル"は上書きしない
            if rel_path.name == f"{Application.NAME}.exe":
                continue

            shutil.copy2(src_file, dst_file)
            progress_callback(int(i / total_files * 50) + 50)

        progress_callback(100)
        return

    def finalize(self) -> None:
        temp_dir = self._app_dir / Application.TEMP_DIR_NAME
        if temp_dir.exists() and temp_dir.is_dir():
            for child in temp_dir.iterdir():
                if child.is_file() or child.is_symlink():
                    child.unlink()
                elif child.is_dir():
                    shutil.rmtree(child)

        callback_cmd = f"{self._app_dir / Application.NAME}.exe --update-result success"
        subprocess.Popen(
            [
                str(self._app_dir / "replacer.exe"),
                "--source_file",
                str(self._new_app_dir / f"{Application.NAME}.exe"),
                "--target_file",
                str(self._app_dir / f"{Application.NAME}.exe"),
                "--callback",
                callback_cmd,
            ]
        )
        return
