from PySide6.QtWidgets import QWidget, QVBoxLayout, QRadioButton, QButtonGroup

from domain.value.stream_value import StreamKind


class GameKindSelector(QWidget):
    def __init__(self, parent: QWidget | None = None) -> None:
        super().__init__(parent)

        layout = QVBoxLayout(self)

        self.raddio_map = {
            StreamKind.INF: QRadioButton("INFINITAS"),
            StreamKind.SDVX: QRadioButton("SDVX"),
        }

        self.group = QButtonGroup(self)
        for radio in self.raddio_map.values():
            self.group.addButton(radio)

        for radio in self.raddio_map.values():
            layout.addWidget(radio)

    def set_selected_kind(self, kind: StreamKind) -> None:
        for k, radio in self.raddio_map.items():
            radio.setChecked(k == kind)

    def get_selected_kind(self) -> StreamKind:
        for k, radio in self.raddio_map.items():
            if radio.isChecked():
                return k
        return StreamKind.INF
