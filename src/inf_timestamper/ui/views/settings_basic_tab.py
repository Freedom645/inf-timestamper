from PySide6.QtWidgets import QWidget, QGridLayout, QLabel, QHBoxLayout

from domain.entity.settings_entity import SettingBasic
from ui.widgets.game_kind_selector import GameKindSelector


class SettingsBasicTab(QWidget):
    def __init__(self, parent: QWidget | None = None):
        super().__init__(parent)
        layout = QGridLayout()

        # ゲーム種別選択
        self.game_kind_selector = GameKindSelector()

        # レイアウトへ追加
        layout_set: list[list[QWidget | QHBoxLayout]] = [
            [QLabel("対象ゲーム"), self.game_kind_selector],
        ]
        for row, item in enumerate(layout_set):
            for col, widget in enumerate(item):
                row_span = 1
                column_span = 2 if len(item) == 1 else 1
                if isinstance(widget, QHBoxLayout):
                    layout.addLayout(widget, row, col, row_span, column_span)
                else:
                    layout.addWidget(widget, row, col, row_span, column_span)

        layout.setRowStretch(layout.rowCount(), 1)
        self.setLayout(layout)

    def set_settings(self, basic_settings: SettingBasic) -> None:
        self.game_kind_selector.set_selected_kind(basic_settings.stream_kind)

    def get_settings(self) -> SettingBasic:
        return SettingBasic(stream_kind=self.game_kind_selector.get_selected_kind())
