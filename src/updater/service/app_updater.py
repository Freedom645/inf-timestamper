import zipfile
import requests
import shutil
import tempfile

from pathlib import Path
from datetime import datetime

from consts import Application as ConstsAppUpdater


class AppUpdater:
    def __init__(self, app_dir: Path):
        self._app_dir = app_dir

    def backup_current_app(self) -> Path:
        """バックアップを作成し、バックアップファイルのパスを返す

        Returns:
            Path: バックアップファイルのパス
        """
        backup_dir = self._app_dir / ConstsAppUpdater.BACKUP_DIR_NAME
        backup_dir.mkdir(exist_ok=True)

        timestamp = datetime.now().strftime("%Y%m%d-%H%M%S")
        backup_path: Path = backup_dir / f"{ConstsAppUpdater.NAME}-{timestamp}.zip"

        with zipfile.ZipFile(backup_path, "w", zipfile.ZIP_DEFLATED) as zf:
            for filepath in self._app_dir.rglob("*"):
                if filepath.is_file() and not filepath.is_relative_to(backup_dir):
                    arcname = filepath.relative_to(self._app_dir)
                    zf.write(filepath, arcname)

        # 過去のバックアップは最大5件までとし、古いものは削除
        backups = sorted(backup_dir.glob(f"{ConstsAppUpdater.NAME}-*.zip"))
        max_keep = 5
        for old_zip in backups[:-max_keep]:
            old_zip.unlink()

        return backup_path

    def update(self, url: str) -> None:
        """アプリケーションを指定URLからダウンロードして展開する

        Args:
            url (str): ダウンロードするアプリケーションのURL
        """
        with tempfile.TemporaryDirectory() as temp_dir_str:
            temp_dir = Path(temp_dir_str)
            zip_path = temp_dir / f"downloaded_{datetime.now().strftime('%Y%m%d_%H%M%S')}.zip"

            with requests.get(url, stream=True) as r:
                r.raise_for_status()
                with open(zip_path, "wb") as f:
                    shutil.copyfileobj(r.raw, f)

            with zipfile.ZipFile(zip_path, "r") as zf:
                zf.extractall(self._app_dir)
