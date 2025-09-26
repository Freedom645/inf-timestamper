from injector import inject
from datetime import datetime
from typing import Callable, TypeVar, Generic

from domain.entity.stream_entity import StreamSession, TimestampData
from domain.port.stream_gateway import IStreamGateway
from domain.value.stream_value import StreamEventType

T = TypeVar("T", bound=TimestampData)


class StreamService(Generic[T]):

    @inject
    def __init__(self, stream_gateway: IStreamGateway) -> None:
        self._stream_gateway = stream_gateway
        self._stream_gateway.observe_stream(self._on_stream_event)

        self._session: StreamSession[T] | None = None
        self._callbacks: dict[
            StreamEventType, list[Callable[[StreamSession[T]], None]]
        ] = {type_: [] for type_ in StreamEventType}

    def connect(self, host: str, port: int, password: str) -> StreamSession[T]:
        if self._session is not None:
            raise RuntimeError("すでに配信セッションが存在します")

        try:
            self._session = StreamSession[T]()
            self._stream_gateway.connect(host, port, password)
        except:
            self._session = None
            raise

        return self._session

    def disconnect(self) -> None:
        if self._session is None:
            raise RuntimeError("配信セッションが存在しません")
        try:
            self._session.end(datetime.now())
            self._stream_gateway.disconnect()
        finally:
            self._session = None

    def subscribe(
        self,
        event_type: StreamEventType,
        callback: Callable[[StreamSession[T]], None],
    ) -> None:
        self._callbacks[event_type].append(callback)

    def unsubscribe(
        self,
        event_type: StreamEventType,
        callback: Callable[[StreamSession[T]], None],
    ) -> None:
        self._callbacks[event_type].remove(callback)

    def _on_stream_event(self, event: StreamEventType) -> None:
        if self._session is None:
            return

        if event == StreamEventType.STREAM_STARTED:
            self._session.start(datetime.now())
        elif event == StreamEventType.STREAM_ENDED:
            self._session.end(datetime.now())

        for callback in self._callbacks[event]:
            callback(self._session)
