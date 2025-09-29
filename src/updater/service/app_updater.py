import fnmatch
import zipfile
import requests
import shutil

from pathlib import Path
from datetime import datetime

from consts import Application as ConstsAppUpdater
from domain.value.progress_value import ProgressCallback


class AppUpdater:
    def __init__(self, app_dir: Path):
        self._app_dir = app_dir

    def backup_current_app(self, *, progress_callback: ProgressCallback | None = None) -> Path:
        """バックアップを作成し、バックアップファイルのパスを返す

        Returns:
            Path: バックアップファイルのパス
        """
        if progress_callback:
            progress_callback(0)
        backup_dir = self._app_dir / ConstsAppUpdater.BACKUP_DIR_NAME
        backup_dir.mkdir(exist_ok=True)

        timestamp = datetime.now().strftime("%Y%m%d-%H%M%S")
        backup_path: Path = backup_dir / f"{ConstsAppUpdater.NAME}-{timestamp}.zip"

        with zipfile.ZipFile(backup_path, "w", zipfile.ZIP_DEFLATED) as zf:
            rglob_list = list(self._app_dir.rglob("*"))
            total_files = len(rglob_list)
            for i, filepath in enumerate(rglob_list, start=1):
                if filepath.name in [ConstsAppUpdater.BACKUP_DIR_NAME, ConstsAppUpdater.TEMP_DIR_NAME]:
                    continue

                if filepath.is_file() and not filepath.is_relative_to(backup_dir):
                    arcname = filepath.relative_to(self._app_dir)
                    zf.write(filepath, arcname)
                if progress_callback:
                    progress_callback(int(i / total_files * 100))
        if progress_callback:
            progress_callback(100)

        # 過去のバックアップは最大5件までとし、古いものは削除
        backups = sorted(backup_dir.glob(f"{ConstsAppUpdater.NAME}-*.zip"))
        max_keep = 5
        for old_zip in backups[:-max_keep]:
            old_zip.unlink()

        return backup_path

    def download_app(self, url: str, *, progress_callback: ProgressCallback | None = None) -> Path:
        """アプリケーションを指定URLからダウンロードする"""
        temp_dir = self._app_dir / ConstsAppUpdater.TEMP_DIR_NAME
        temp_dir.mkdir(exist_ok=True)
        for child in temp_dir.iterdir():
            if child.is_file() or child.is_symlink():
                child.unlink()
            elif child.is_dir():
                shutil.rmtree(child)

        # ZIPダウンロード
        if progress_callback:
            progress_callback(0)

        zip_path: Path = temp_dir / f"downloaded_{datetime.now().strftime('%Y%m%d_%H%M%S')}.zip"
        with requests.get(url, stream=True) as r:
            r.raise_for_status()
            total_size = int(r.headers.get("Content-Length", 0))
            downloaded = 0
            chunk_size = 8192  # 8KBごと

            with open(zip_path, "wb") as f:
                for chunk in r.iter_content(chunk_size=chunk_size):
                    if chunk:
                        f.write(chunk)
                        downloaded += len(chunk)
                        if total_size > 0 and progress_callback:
                            progress_percent = int(downloaded / total_size * 100)
                            progress_callback(progress_percent)

        if progress_callback:
            progress_callback(100)

        return zip_path

    def extract_zip(self, zip_path: Path, *, progress_callback: ProgressCallback | None = None) -> Path:
        """ZIPファイルを展開する"""
        dist_path: Path = self._app_dir / ConstsAppUpdater.TEMP_DIR_NAME / "new_app"

        if progress_callback:
            progress_callback(0)

        dist_path.mkdir(exist_ok=True)

        with zipfile.ZipFile(zip_path, "r") as zf:
            files = zf.infolist()
            total_files = len(files)
            for i, f in enumerate(files, 1):
                zf.extract(f, dist_path)
                if progress_callback:
                    progress_percent = int(i / total_files * 100)
                    progress_callback(progress_percent)

        if progress_callback:
            progress_callback(100)

        return dist_path

    def copy_tree(
        self, src: Path, dst: Path, progress_callback: ProgressCallback | None = None, ignore: list[str] | None = None
    ):
        """指定したディレクトリを再帰的にコピーする"""
        if progress_callback:
            progress_callback(0)

        # コピー対象ファイルを列挙
        all_files = [p for p in src.rglob("*") if p.is_file()]
        total_files = len(all_files)

        for i, src_file in enumerate(all_files, 1):
            # コピー先パスを生成
            rel_path = src_file.relative_to(src)
            dst_file = dst / rel_path
            dst_file.parent.mkdir(parents=True, exist_ok=True)

            # 無視パターンがある場合
            if ignore and any(fnmatch.fnmatch(str(rel_path), pattern) for pattern in ignore):
                continue

            # コピー
            shutil.copy2(src_file, dst_file)

            # 進捗通知
            if progress_callback:
                progress_percent = int(i / total_files * 100)
                progress_callback(progress_percent)

        if progress_callback:
            progress_callback(100)

        return dst
