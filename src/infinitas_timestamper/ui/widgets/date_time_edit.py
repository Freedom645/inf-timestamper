from datetime import datetime
from PySide6.QtWidgets import QLineEdit, QWidget, QPushButton, QCalendarWidget, QHBoxLayout, QWidgetAction, QMenu
from PySide6.QtCore import Signal, QDateTime, Qt, QDate
from PySide6.QtGui import QPalette, QColor


class DateTimeEdit(QWidget):
    datetime_changed = Signal(QDateTime)

    def __init__(self, parent: QWidget | None = None):
        super().__init__(parent)

        self._has_value = False
        self._datetime_value: QDateTime | None = None
        self._format = "yyyy/MM/dd HH:mm:ss"
        self._empty_string = "-"

        # --- 内部UI ---
        self.line_edit = QLineEdit()
        self.line_edit.setPlaceholderText(self._format)
        self.line_edit.setReadOnly(True)
        self.line_edit.setText(self._empty_string)
        self.line_edit.textChanged.connect(self._on_text_changed)

        # カレンダーボタン
        self.calendar_button = QPushButton("📅")
        self.calendar_button.setFixedWidth(28)
        self.calendar_button.setEnabled(False)
        self.calendar_button.clicked.connect(self._open_calendar)

        # 編集ボタン
        self.edit_button = QPushButton("✏️")
        self.edit_button.setFixedWidth(28)
        self.edit_button.clicked.connect(self._toggle_edit_mode)

        layout = QHBoxLayout(self)
        layout.setSpacing(0)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.addWidget(self.line_edit)
        layout.addWidget(self.calendar_button)
        layout.addWidget(self.edit_button)

        # 状態管理
        self._editing = False
        self._valid_input = False

    # --- 内部処理 ---

    def _set_empty(self):
        """内部データをクリアして - を表示"""
        self._datetime_value = None
        self.line_edit.setText(self._empty_string)
        self._set_valid_style(True)

    def _get_current_datetime(self) -> QDateTime:
        input_text = self.line_edit.text().strip()
        return QDateTime.fromString(input_text, self._format)

    def _on_text_changed(self, text: str) -> None:
        dt = self._get_current_datetime()
        if dt.isValid():
            self._has_value = True
            self._set_valid_style(True)
            self._valid_input = True
        elif text.strip() == self._empty_string:
            self._has_value = False
            self._set_valid_style(True)
            self._valid_input = False
        else:
            # フォーマットエラー
            self._has_value = False
            self._set_valid_style(False)
            self._valid_input = False

    def _set_valid_style(self, valid: bool):
        """赤枠表示の切り替え"""
        palette = self.line_edit.palette()
        if valid:
            palette.setColor(QPalette.ColorRole.Base, QColor(Qt.GlobalColor.white))
        else:
            palette.setColor(QPalette.ColorRole.Base, QColor(255, 220, 220))
        self.line_edit.setPalette(palette)

    def _open_calendar(self):
        """カレンダーをドロップダウン風に表示"""
        menu = QMenu(self)

        cal = QCalendarWidget(menu)
        cal.setGridVisible(True)

        # アクションをメニューに追加
        act = QWidgetAction(menu)
        act.setDefaultWidget(cal)
        menu.addAction(act)

        def on_date_selected(date: QDate):
            # 既存の時刻を保持（なければ現在時刻）
            time_part = datetime.now().strftime("%H:%M:%S")
            if self._has_value:
                dt = self._get_current_datetime()
                if dt.isValid():
                    time_part = dt.toString("HH:mm:ss")

            self.line_edit.setText(f"{date.toString('yyyy/MM/dd')} {time_part}")
            menu.close()

        cal.clicked.connect(on_date_selected)

        # ボタンの下に出す
        pos = self.calendar_button.mapToGlobal(self.calendar_button.rect().bottomLeft())
        menu.exec(pos)

    def _toggle_edit_mode(self):
        """編集モードの切り替え"""
        if not self._editing:
            # 編集開始
            self._editing = True
            self.line_edit.setReadOnly(False)
            self.edit_button.setText("✅")
            self.calendar_button.setEnabled(True)
            if self.line_edit.text() == "-":
                self.line_edit.clear()
        else:
            # 編集確定
            self._editing = False
            self.line_edit.setReadOnly(True)
            self.edit_button.setText("✏️")
            self.calendar_button.setEnabled(False)

            current_datetime = self._get_current_datetime()
            if current_datetime.isValid():
                self._datetime_value = current_datetime
            else:
                self._datetime_value = None
                self.line_edit.setText("-")

            self.datetime_changed.emit(self._datetime_value)

    # --- Public API ---

    def clear(self):
        """内部データをクリアする"""
        self._set_empty()

    def set_datetime(self, dt: QDateTime | datetime | None):
        """日時をセット"""
        if dt is None:
            self._set_empty()
            return

        if isinstance(dt, datetime):
            dt = QDateTime.fromSecsSinceEpoch(int(dt.timestamp()))

        self._datetime_value = dt
        self._has_value = True
        self.line_edit.setText(dt.toString(self._format))
        self._set_valid_style(True)

    def get_datetime(self) -> QDateTime | None:
        """現在の値を取得 (未入力なら None)"""
        if not self._has_value:
            return None
        dt = QDateTime.fromString(self.line_edit.text(), self._format)
        return dt if dt.isValid() else None
