# infrastructure/dto/stream_session_dto.py
from uuid import UUID
from datetime import datetime
from pydantic import BaseModel

from domain.value.stream_value import StreamKind


class TimestampDTO(BaseModel):
    id: UUID
    occurred_at: datetime
    data: dict[str, str | dict[str, str | int | None] | None]


class StreamSessionDTO(BaseModel):
    kind: StreamKind | None = None
    id: UUID
    stream_status: str
    start_time: datetime | None
    timestamps: list[TimestampDTO]
