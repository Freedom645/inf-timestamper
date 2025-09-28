from abc import ABC
from uuid import UUID, uuid4
from typing import Generic, TypeVar
from pydantic import BaseModel, Field
from datetime import datetime, timedelta

from domain.value.stream_value import StreamStatus


class TimestampData(ABC, BaseModel): ...


T = TypeVar("T", bound=TimestampData)


class Timestamp(BaseModel, Generic[T]):
    id: UUID = Field(default_factory=uuid4)
    """タイムスタンプID"""
    occurred_at: datetime = Field(default_factory=datetime.now)
    """タイムスタンプ時間"""
    data: T
    """タイムスタンプデータ"""

    def get_elapse(self, base_time: datetime) -> timedelta:
        """基準時間からの経過時間を取得する"""
        delta_seconds = (self.occurred_at - base_time).total_seconds()
        return timedelta(seconds=int(delta_seconds))


class StreamSession(BaseModel, Generic[T]):
    id: UUID = Field(default_factory=uuid4)
    """セッションID"""
    stream_status: StreamStatus = StreamStatus.WAITING
    """配信ステータス"""
    start_time: datetime | None = None
    """配信開始時間 (Noneの場合は配信前)"""
    timestamps: list[Timestamp[T]] = []
    """タイムスタンプのリスト"""

    def wait_stream(self) -> "StreamSession[T]":
        """配信待機状態にする"""
        if self.stream_status != StreamStatus.WAITING:
            raise ValueError(f"セッションは記録開始待ちではありません {self.stream_status}")
        self.stream_status = StreamStatus.BEFORE_STREAM
        return self

    def start_recording(self, start_time: datetime) -> "StreamSession[T]":
        """記録を開始する"""
        if self.stream_status not in [StreamStatus.WAITING, StreamStatus.BEFORE_STREAM]:
            raise ValueError(f"セッションはすでに開始しています {self.stream_status}")

        self.start_time = start_time
        self.stream_status = StreamStatus.RECORDING
        return self

    def resume_recording(self) -> "StreamSession[T]":
        """記録を再開する"""
        if self.stream_status != StreamStatus.COMPLETED:
            raise ValueError(f"セッションは記録完了ではありません {self.stream_status}")
        self.stream_status = StreamStatus.RECORDING
        return self

    def complete_recording(self) -> "StreamSession[T]":
        """記録を完了する"""
        if self.stream_status not in [StreamStatus.RECORDING, StreamStatus.BEFORE_STREAM]:
            raise ValueError(f"セッションは記録中ではありません {self.stream_status}")

        if self.stream_status == StreamStatus.BEFORE_STREAM:
            self.stream_status = StreamStatus.WAITING
        else:
            self.stream_status = StreamStatus.COMPLETED

        return self

    def add_timestamp(self, timestamp: Timestamp[T]) -> None:
        """タイムスタンプを追加する"""
        self.timestamps.append(timestamp)

    def get_timestamp_list(self, start_time: datetime | None = None) -> list[tuple[timedelta, Timestamp[T]]]:
        """タイムスタンプのリストを取得する"""
        if start_time is None:
            start_time = self.start_time
        if start_time is None:
            raise ValueError("セッションの開始時間が設定されていません")

        stamps: list[tuple[timedelta, Timestamp[T]]] = []
        for timestamp in self.timestamps:
            stamps.append((timestamp.get_elapse(start_time), timestamp))
        return stamps

    def get_latest_timestamp(self) -> Timestamp[T] | None:
        """最新のタイムスタンプを取得する"""
        if not self.timestamps:
            return None
        return max(self.timestamps, key=lambda t: t.occurred_at)

    def count_timestamp(self) -> int:
        """タイムスタンプの数を取得する"""
        return len(self.timestamps)
