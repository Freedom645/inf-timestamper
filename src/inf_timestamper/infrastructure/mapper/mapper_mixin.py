from abc import abstractmethod
from typing import Generic, TypeVar

D = TypeVar("D")
E = TypeVar("E")


class DTOMapperMixin(Generic[D, E]):
    @abstractmethod
    def to_domain(self, dto: D) -> E: ...

    @abstractmethod
    def from_domain(self, entity: E) -> D: ...
