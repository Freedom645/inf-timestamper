from abc import ABC, abstractmethod

from domain.entity.stream_entity import StreamSession, Timestamp
from domain.entity.inf_game_entity import InfPlayData
from domain.entity.sdvx_game_entity import SDVXPlayData
from domain.value.stream_value import StreamKind, StreamStatus
from infrastructure.mapper.mapper_mixin import DTOMapperMixin
from infrastructure.dto.stream_session_dto import StreamSessionDTO, TimestampDTO


class TimestampConverter(ABC, DTOMapperMixin[TimestampDTO, Timestamp]):
    @abstractmethod
    def kind(self) -> list[StreamKind | None]: ...


class InfTimestampConverter(TimestampConverter):
    def kind(self) -> list[StreamKind | None]:
        return [StreamKind.INF, None]

    def to_domain(self, dto: TimestampDTO) -> Timestamp:
        return Timestamp(id=dto.id, occurred_at=dto.occurred_at, data=InfPlayData.model_validate(dto.data))

    def from_domain(self, entity: Timestamp) -> TimestampDTO:
        return TimestampDTO(id=entity.id, occurred_at=entity.occurred_at, data=entity.data.model_dump())


class SDVXTimestampConverter(TimestampConverter):
    def kind(self) -> list[StreamKind | None]:
        return [StreamKind.SDVX]

    def to_domain(self, dto: TimestampDTO) -> Timestamp:
        return Timestamp(id=dto.id, occurred_at=dto.occurred_at, data=SDVXPlayData.model_validate(dto.data))

    def from_domain(self, entity: Timestamp) -> TimestampDTO:
        return TimestampDTO(id=entity.id, occurred_at=entity.occurred_at, data=entity.data.model_dump())


class StreamSessionMapper(DTOMapperMixin[StreamSessionDTO, StreamSession]):
    def __init__(self) -> None:
        super().__init__()
        self._converters: list[TimestampConverter] = [InfTimestampConverter(), SDVXTimestampConverter()]

    def to_domain(self, dto: StreamSessionDTO) -> StreamSession:
        converter = next((c for c in self._converters if dto.kind in c.kind()), None)
        if converter is None:
            supported = [k for c in self._converters for k in c.kind()]
            raise ValueError(f"不明な形式が指定されました。 kind={dto.kind}, サポート: {supported}")

        timestamps = [converter.to_domain(ts) for ts in dto.timestamps]

        return StreamSession(
            kind=dto.kind or StreamKind.INF,
            id=dto.id,
            stream_status=StreamStatus(dto.stream_status),
            start_time=dto.start_time,
            timestamps=timestamps,
        )

    def from_domain(self, entity: StreamSession) -> StreamSessionDTO:
        converter = next((c for c in self._converters if entity.kind in c.kind()), None)
        if converter is None:
            supported = [k for c in self._converters for k in c.kind()]
            raise ValueError(f"不明な形式が指定されました。 kind={entity.kind}, サポート: {supported}")

        timestamps_dto = [converter.from_domain(ts) for ts in entity.timestamps]

        return StreamSessionDTO(
            kind=entity.kind,
            id=entity.id,
            stream_status=entity.stream_status.value,
            start_time=entity.start_time,
            timestamps=timestamps_dto,
        )
