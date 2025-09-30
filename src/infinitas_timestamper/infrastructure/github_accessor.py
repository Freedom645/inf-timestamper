import requests

from typing import TypedDict
from pydantic import HttpUrl

from core.consts import GitHub
from usecase.repository.app_version_provider import IVersionProvider
from usecase.dto.app_updating import VersionInfo


class LatestReleaseAsset(TypedDict):
    name: str
    browser_download_url: str


class LatestRelease(TypedDict):
    tag_name: str
    assets: list[LatestReleaseAsset]


class GithubRepositoryAccessor(IVersionProvider):
    def __init__(self) -> None:
        self.base_url = f"https://api.github.com/repos/{GitHub.REPOSITORY}"

    def get_latest_release(self) -> LatestRelease:
        url = f"{self.base_url}/releases/latest"
        resp = requests.get(url, timeout=10)
        resp.raise_for_status()

        data: LatestRelease = resp.json()
        return data

    def check_latest_version(self) -> VersionInfo:
        latest_release = self.get_latest_release()
        latest = latest_release["tag_name"].lstrip("v")
        url = next((a["browser_download_url"] for a in latest_release["assets"] if a["name"] == GitHub.ASSET_NAME))

        return VersionInfo(version_str=latest, asset_url=HttpUrl(url))
