from enum import StrEnum
from typing import Callable


class Step(StrEnum):
    VERSION_CHECK = "バージョンチェック"
    BACKUP = "バックアップ"
    DOWNLOAD = "ダウンロード"
    EXTRACT = "展開"
    REPLACE = "更新"


ProgressCallback = Callable[[int], None]
StepProgressCallback = Callable[[Step, int], None]
