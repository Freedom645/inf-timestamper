from enum import StrEnum, unique


@unique
class StreamStatus(StrEnum):
    BEFORE = "before"
    """配信前"""
    LIVE = "live"
    """配信中"""
    ENDED = "ended"
    """配信終了"""


@unique
class StreamEventType(StrEnum):
    STREAM_STARTED = "stream_started"
    """配信開始"""
    STREAM_ENDED = "stream_ended"
    """配信終了"""
