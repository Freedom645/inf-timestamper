import pyperclip

from domain.entity.game import PlayData
from domain.entity.stream import StreamSession


class OutputUseCase:
    def copy_to_clipboard(self, stream_session: StreamSession[PlayData]) -> None:
        if stream_session.start_time is None:
            raise ValueError("配信が開始していません。")

        lines: list[str] = []
        for delta, timestamp in stream_session.get_timestamp_list():
            line = f"{delta} {timestamp.data.title} [Lv.{timestamp.data.level}]"
            lines.append(line)

        pyperclip.copy("\n".join(lines))
