from enum import StrEnum
from pydantic import BaseModel


class UpdateExeCommand(BaseModel):
    class Config:
        frozen = True

    execution: list[str]


class ExecutionStatus(StrEnum):
    SUCCESS = "success"
    ERROR = "error"


class ExecutionResult(BaseModel):
    status: ExecutionStatus
    message: str | None
    data: dict[str, str] | None = None
