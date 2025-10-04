from enum import StrEnum


class DJ_LEVEL(StrEnum):
    AAA = "AAA"
    AA = "AA"
    A = "A"
    B = "B"
    C = "C"
    D = "D"
    E = "E"
    F = "F"


class ClearLamp(StrEnum):
    NO_PLAY = "NP"
    FAILED = "F"
    ASSIST_CLEAR = "AC"
    EASY_CLEAR = "EC"
    CLEAR = "NC"
    HARD_CLEAR = "HC"
    EX_HARD_CLEAR = "EX"
    FULL_COMBO = "FC"
    PERFECT = "PFC"
