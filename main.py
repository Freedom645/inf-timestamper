import sys

from injector import Injector
from PySide6.QtWidgets import QApplication

from app_di import AppModule
from ui.views.main_window import MainWindow

if __name__ == "__main__":
    app = QApplication(sys.argv)

    injector = Injector([AppModule()])
    main_window = injector.get(MainWindow)
    main_window.show()

    sys.exit(app.exec())
