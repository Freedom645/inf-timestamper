import sys

from injector import Injector
from PySide6.QtWidgets import QApplication

from core.app_module import AppModule
from core.logger_module import LoggerModule
from ui.views.main_window import MainWindow

if __name__ == "__main__":
    app = QApplication(sys.argv)

    injector = Injector([AppModule(), LoggerModule()])
    main_window = injector.get(MainWindow)
    main_window.show()

    sys.exit(app.exec())
