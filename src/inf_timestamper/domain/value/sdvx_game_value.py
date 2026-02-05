from enum import StrEnum


class SDVXClearLamp(StrEnum):
    FAILED = "failed"
    CLEAR = "clear"
    HARD = "hard"
    EXH = "exh"
    UC = "uc"
    PUC = "puc"
    UNKNOWN = "unknown"

    @classmethod
    def from_str(cls, lamp_str: str | None) -> "SDVXClearLamp":
        if lamp_str is None:
            return SDVXClearLamp.UNKNOWN
        lamp_str = lamp_str.lower()
        try:
            return SDVXClearLamp(lamp_str)
        except ValueError:
            return SDVXClearLamp.UNKNOWN
