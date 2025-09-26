import logging
from datetime import datetime
from injector import Module, provider, singleton

from domain.value.base_path import BasePath


class LoggerModule(Module):
    @singleton
    @provider
    def provide_logger(self, base_path: BasePath) -> logging.Logger:
        logs_dir = base_path / "logs"
        logs_dir.mkdir(exist_ok=True)

        timestamp = datetime.now().strftime("%Y-%m-%d_%H-%M-%S")
        app_log_file = logs_dir / f"app_{timestamp}.log"
        external_log_file = logs_dir / f"external_{timestamp}.log"

        # === アプリ用ロガー ===
        app_logger = logging.getLogger("app")
        app_logger.setLevel(logging.DEBUG)

        if not app_logger.handlers:
            # アプリ用 FileHandler
            app_file_handler = logging.FileHandler(app_log_file, encoding="utf-8")
            app_file_handler.setLevel(logging.INFO)

            # コンソール出力
            console_handler = logging.StreamHandler()
            console_handler.setLevel(logging.DEBUG)

            app_formatter = logging.Formatter(
                fmt="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
                datefmt="%Y-%m-%d %H:%M:%S",
            )
            app_file_handler.setFormatter(app_formatter)
            console_handler.setFormatter(app_formatter)

            app_logger.addHandler(app_file_handler)
            app_logger.addHandler(console_handler)

            # アプリのログを root に伝播させない
            app_logger.propagate = False

        # === 外部ライブラリ用ロガー (root) ===
        root_logger = logging.getLogger()
        root_logger.setLevel(logging.INFO)
        if not any(
            isinstance(h, logging.FileHandler)
            and h.baseFilename == str(external_log_file)
            for h in root_logger.handlers
        ):
            external_file_handler = logging.FileHandler(
                external_log_file, encoding="utf-8"
            )
            external_file_handler.setLevel(logging.DEBUG)

            external_formatter = logging.Formatter(
                fmt="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
                datefmt="%Y-%m-%d %H:%M:%S",
            )
            external_file_handler.setFormatter(external_formatter)

            root_logger.addHandler(external_file_handler)

        return app_logger
