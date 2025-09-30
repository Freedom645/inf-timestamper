from typing import Protocol
from PySide6.QtWidgets import QWidget

from ui.views.update_window import UpdateWindow


class UpdateWindowFactory(Protocol):
    def __call__(self, parent: QWidget | None = None) -> UpdateWindow: ...
