from abc import ABC
from uuid import UUID, uuid4
from typing import Generic, TypeVar
from pydantic import BaseModel, Field
from datetime import datetime, timedelta

from domain.value.stream import StreamStatus


class TimestampData(ABC, BaseModel): ...


T = TypeVar("T", bound=TimestampData)


class Timestamp(BaseModel, Generic[T]):
    id: UUID = Field(default_factory=uuid4)
    """タイムスタンプID"""
    occurred_at: datetime = Field(default_factory=datetime.now)
    """タイムスタンプ時間"""
    data: T
    """タイムスタンプデータ"""


class StreamSession(BaseModel, Generic[T]):
    id: UUID = Field(default_factory=uuid4)
    """セッションID"""
    stream_status: StreamStatus = StreamStatus.BEFORE
    """配信ステータス"""
    start_time: datetime | None = None
    """配信開始時間 (Noneの場合は配信前)"""
    end_time: datetime | None = None
    """配信終了時間 (Noneの場合は配信前/配信中)"""
    timestamps: list[Timestamp[T]] = []
    """タイムスタンプのリスト"""

    def start(self, start_time: datetime) -> None:
        """配信を開始する"""
        if self.start_time is not None or self.stream_status != StreamStatus.BEFORE:
            raise ValueError("StreamSessionはすでに開始しています。")

        self.start_time = start_time
        self.stream_status = StreamStatus.LIVE

    def end(self, end_time: datetime) -> None:
        """配信を終了する"""
        if self.start_time is None or self.stream_status == StreamStatus.BEFORE:
            raise ValueError("StreamSessionはまだ開始されていません。")
        if self.end_time is not None or self.stream_status == StreamStatus.ENDED:
            raise ValueError("StreamSessionはすでに終了しています。")

        self.end_time = end_time
        self.stream_status = StreamStatus.ENDED

    def add_timestamp(self, timestamp: Timestamp[T]) -> None:
        """タイムスタンプを追加する"""
        if self.start_time is None:
            raise ValueError(
                "配信が開始していないため、タイムスタンプを追加できません。"
            )
        if self.end_time is not None:
            raise ValueError("終了した配信にはタイムスタンプを追加できません。")
        if timestamp.occurred_at < self.start_time:
            raise ValueError(
                f"配信開始前のタイムスタンプは登録できません。 開始時間: {self.start_time}, 登録時間: {timestamp.occurred_at}"
            )
        self.timestamps.append(timestamp)

    def get_elapse(self, timestamp: Timestamp[T]) -> timedelta:
        if self.start_time is None:
            raise ValueError("配信が開始していません")

        delta_seconds = (timestamp.occurred_at - self.start_time).total_seconds()
        return timedelta(seconds=int(delta_seconds))

    def get_timestamp_list(self) -> list[tuple[timedelta, Timestamp[T]]]:
        """タイムスタンプのリストを取得する"""
        stamps: list[tuple[timedelta, Timestamp[T]]] = []
        for timestamp in self.timestamps:
            stamps.append((self.get_elapse(timestamp), timestamp))
        return stamps

    def get_latest_timestamp(self) -> Timestamp[T] | None:
        """最新のタイムスタンプを取得する"""
        if not self.timestamps:
            return None
        return max(self.timestamps, key=lambda t: t.occurred_at)

    def count_timestamp(self) -> int:
        """タイムスタンプの数を取得する"""
        return len(self.timestamps)
