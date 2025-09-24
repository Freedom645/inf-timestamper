from typing import Callable
from PySide6.QtCore import Signal, QObject


class FunctionRunner(QObject):
    finished = Signal(bool, str)

    def __init__(self, func: Callable[[], str]):
        super().__init__()
        self.func = func

    def run(self):
        try:
            self.finished.emit(True, self.func())
        except Exception as e:
            self.finished.emit(False, self.func())
