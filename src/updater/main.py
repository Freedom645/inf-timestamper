import sys

from consts import GitHub
from entity.arguments import Arguments, Mode
from entity.result import ExecutionResult, ExecutionStatus
from service.app_updater import AppUpdater
from service.github_accessor import GithubRepositoryAccessor


def check() -> ExecutionResult:
    accessor = GithubRepositoryAccessor(repo=GitHub.REPO)
    version, _ = accessor.check_latest_version(asset_name=GitHub.ASSET_NAME)

    return ExecutionResult(status=ExecutionStatus.SUCCESS, message="Check completed", data={"version": version})


def update(args: Arguments) -> ExecutionResult:
    if args.update_path is None:
        raise ValueError("UpdatePath is None")

    accessor = GithubRepositoryAccessor(repo=GitHub.REPO)
    version, url = accessor.check_latest_version(asset_name=GitHub.ASSET_NAME)

    if url is None:
        raise ValueError("No valid URL found for the latest release")

    backuper = AppUpdater(app_dir=args.update_path)
    backuper.backup_current_app()

    backuper.update(url=url)
    return ExecutionResult(status=ExecutionStatus.SUCCESS, message="Update completed", data={"version": version})


def main():
    try:
        args = Arguments.load_from_sysargs()
        match args.mode:
            case Mode.CHECK:
                res = check()
            case Mode.UPDATE:
                res = update(args)
            case _:
                raise ValueError(f"Invalid mode: {args.mode}")

        print(res.model_dump_json())
        sys.exit(0)
    except Exception as e:
        res = ExecutionResult(status=ExecutionStatus.ERROR, message=str(e))
        print(res.model_dump_json())
        sys.exit(1)


if __name__ == "__main__":
    res = main()
