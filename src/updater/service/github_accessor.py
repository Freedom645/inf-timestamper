import requests

from typing import TypedDict

from domain.value.progress_value import ProgressCallback


class LatestReleaseAsset(TypedDict):
    name: str
    browser_download_url: str


class LatestRelease(TypedDict):
    tag_name: str
    assets: list[LatestReleaseAsset]


class GithubRepositoryAccessor:
    def __init__(self, repo: str):
        self.repo = repo
        self.base_url = f"https://api.github.com/repos/{self.repo}"

    def get_latest_release(self) -> LatestRelease:
        url = f"{self.base_url}/releases/latest"
        resp = requests.get(url, timeout=10)
        resp.raise_for_status()

        data: LatestRelease = resp.json()
        return data

    def check_latest_version(
        self, asset_name: str, *, progress_callback: ProgressCallback | None = None
    ) -> tuple[str, str | None]:
        if progress_callback:
            progress_callback(0)

        latest_release = self.get_latest_release()
        latest = latest_release["tag_name"].lstrip("v")
        url = next((a["browser_download_url"] for a in latest_release["assets"] if a["name"] == asset_name), None)

        if progress_callback:
            progress_callback(100)

        return latest, url
