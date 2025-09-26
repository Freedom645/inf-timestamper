from PySide6.QtWidgets import QLineEdit, QCompleter, QWidget
from PySide6.QtCore import QStringListModel, Qt


class DollarCompleterLineEdit(QLineEdit):
    def __init__(self, words: list[str], parent: QWidget | None = None) -> None:
        super().__init__(parent)

        self._model = QStringListModel(words)
        self._completer = QCompleter(self._model, parent=self)
        self._completer.setCaseSensitivity(Qt.CaseSensitivity.CaseInsensitive)
        self._completer.setCompletionMode(QCompleter.CompletionMode.PopupCompletion)
        self._completer.setWidget(self)

        self._completer.activated.connect(self.insert_completion)

        self.textEdited.connect(self.on_text_edited)

    def insert_completion(self, completion: str) -> None:
        cursor_pos = self.cursorPosition()
        text = self.text()

        dollar_pos = text.rfind("$", 0, cursor_pos)
        if dollar_pos == -1:
            return

        new_text = text[:dollar_pos] + completion + text[cursor_pos:]
        self.setText(new_text)
        self.setCursorPosition(dollar_pos + 1 + len(completion))

    def on_text_edited(self, text: str) -> None:
        cursor_pos = self.cursorPosition()
        prefix = text[:cursor_pos]
        dollar_pos = prefix.rfind("$")

        if dollar_pos == -1:
            return

        current_prefix = "$" + prefix[dollar_pos + 1 :]
        self._completer.setCompletionPrefix(current_prefix)

        if current_prefix or self._completer.completionCount() > 0:
            cr = self.cursorRect()
            cr.setWidth(
                self._completer.popup().sizeHintForColumn(0)
                + self._completer.popup().verticalScrollBar().sizeHint().width()
            )
            self._completer.complete(cr)
