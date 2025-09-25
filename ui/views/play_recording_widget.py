from uuid import UUID
from PySide6.QtWidgets import (
    QWidget,
    QLabel,
    QVBoxLayout,
    QListWidget,
    QListWidgetItem,
    QPushButton,
    QSizePolicy,
    QHBoxLayout,
)
from PySide6.QtCore import QThread
from datetime import datetime
from injector import inject

from domain.entity.game import PlayData
from domain.entity.stream import StreamSession, Timestamp
from ui.view_models.play_recording_view_model import PlayRecordingViewModel
from ui.views.utils import FunctionRunner


class PlayRecordingWidget(QWidget):

    @inject
    def __init__(self, play_recording_view_model: PlayRecordingViewModel, parent=None):
        super().__init__(parent)
        self._vm = play_recording_view_model

        self._thread: QThread | None = None
        self._timestamp_item_map: dict[UUID, QListWidgetItem] = {}

        self.start_btn = QPushButton("記録開始")
        self.start_btn.setMinimumSize(80, 40)
        self.start_btn.setSizePolicy(QSizePolicy.Policy.Fixed, QSizePolicy.Policy.Fixed)
        self.start_btn.clicked.connect(self.toggle_recording)

        self.copy_btn = QPushButton("コピー")
        self.copy_btn.setMinimumSize(80, 40)
        self.copy_btn.setSizePolicy(QSizePolicy.Policy.Fixed, QSizePolicy.Policy.Fixed)
        self.copy_btn.clicked.connect(self._vm.on_copy_timestamps_to_clipboard)

        btn_layout = QHBoxLayout()
        btn_layout.addWidget(self.start_btn)
        btn_layout.addWidget(self.copy_btn)

        self.status_label = QLabel("状態: 記録開始待ち")
        self.start_time_label = QLabel("配信開始: -")
        self.timestamp_count_label = QLabel("タイムスタンプ数: 0")
        self.list_widget = QListWidget()

        layout = QVBoxLayout()
        layout.addLayout(btn_layout)
        layout.addWidget(self.status_label)
        layout.addWidget(self.start_time_label)
        layout.addWidget(self.timestamp_count_label)
        layout.addWidget(self.list_widget)
        self.setLayout(layout)

        # Signal を接続
        self._vm.recording_button_changed.connect(self._on_recording_button_changed)
        self._vm.status_changed.connect(self._on_status_changed)
        self._vm.start_time_changed.connect(self._on_start_time_changed)
        self._vm.timestamp_count_changed.connect(self._on_timestamp_count_changed)
        self._vm.timestamp_upsert_signal.connect(self._on_timestamp_upsert_signal)

    def toggle_recording(self):
        if self.start_btn.text() == "記録開始":
            self._thread = QThread()
            self._worker = FunctionRunner(self._vm.on_start_recording_button)
            self._worker.moveToThread(self._thread)
            self._thread.started.connect(self._worker.run)
            self._worker.finished.connect(self._thread.quit)
            self._worker.finished.connect(self._worker.deleteLater)
            self._thread.finished.connect(self._thread.deleteLater)
            self._thread.start()
        else:
            self._vm.on_stop_recording_button()

    def _on_recording_button_changed(self, enabled: bool, text: str):
        self.start_btn.setEnabled(enabled)
        self.start_btn.setText(text)

    def _on_status_changed(self, status: str):
        self.status_label.setText(f"状態: {status}")

    def _on_start_time_changed(self, start_time: datetime | None):
        text = start_time.strftime("%Y-%m-%d %H:%M:%S") if start_time else "-"
        self.start_time_label.setText(f"配信開始: {text}")

    def _on_timestamp_count_changed(self, count: int):
        self.timestamp_count_label.setText(f"タイムスタンプ数: {count}")

    def _on_timestamp_upsert_signal(
        self, session: StreamSession[PlayData], timestamp: Timestamp[PlayData]
    ):
        delta = session.get_elapse(timestamp)
        label = f"{delta} {timestamp.data.title} [Lv.{timestamp.data.level}]"

        if res := timestamp.data.play_result:
            label += f" (DJ LEVEL: {res.dj_level.name}, EXスコア: {res.ex_score}, ランプ: {res.lamp.name})"

        if timestamp.id in self._timestamp_item_map:
            item = self._timestamp_item_map[timestamp.id]
            item.setText(label)
            return

        self._timestamp_item_map[timestamp.id] = QListWidgetItem(label)
        self.list_widget.addItem(self._timestamp_item_map[timestamp.id])

    def closeEvent(self, event):
        self._vm.on_close()
        return super().closeEvent(event)
