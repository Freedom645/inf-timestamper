from typing import Protocol
from PySide6.QtWidgets import QWidget

from ui.widgets.play_recording_widget import PlayRecordingWidget


class PlayRecordingWidgetFactory(Protocol):
    def __call__(self, parent: QWidget | None = None) -> PlayRecordingWidget: ...
