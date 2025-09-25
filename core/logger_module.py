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

        log_file = logs_dir / f"{datetime.now().strftime('%Y-%m-%d_%H-%M-%S')}.log"

        logger = logging.getLogger("app")
        logger.setLevel(logging.DEBUG)

        # すでにハンドラが付いていれば重複回避
        if not logger.handlers:
            # --- FileHandler ---
            file_handler = logging.FileHandler(log_file, encoding="utf-8")
            file_handler.setLevel(logging.DEBUG)

            # --- StreamHandler (console) ---
            console_handler = logging.StreamHandler()
            console_handler.setLevel(logging.INFO)

            formatter = logging.Formatter(
                fmt="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
                datefmt="%Y-%m-%d %H:%M:%S",
            )
            file_handler.setFormatter(formatter)
            console_handler.setFormatter(formatter)

            logger.addHandler(file_handler)
            logger.addHandler(console_handler)

        return logger
