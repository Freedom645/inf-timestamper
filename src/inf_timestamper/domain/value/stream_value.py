from enum import StrEnum, unique


@unique
class StreamStatus(StrEnum):
    WAITING = "wait"
    """記録開始待ち"""
    BEFORE_STREAM = "beforeStream"
    """配信開始前"""
    RECORDING = "recording"
    """記録中"""
    COMPLETED = "ended"
    """記録完了"""


@unique
class StreamEventType(StrEnum):
    STREAM_STARTED = "stream_started"
    """配信開始"""
    STREAM_ENDED = "stream_ended"
    """配信終了"""
